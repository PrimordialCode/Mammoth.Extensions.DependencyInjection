using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Mammoth.Extensions.DependencyInjection;

// Diagnostics replaces ordinary implementation-type descriptors with tracking factories.
// Keep native DI's constructor rules separate from ActivatorUtilities / DependsOn rules.
internal static class NativeConstructorActivator
{
    internal static object CreateInstance(IServiceProvider provider, Type implementationType, object? serviceKey = null)
    {
        var snapshot = provider.GetRequiredService<ServiceProviderRegistrationSnapshot>();
        var identity = new ServiceIdentifier(serviceKey, implementationType);
        if (!ResolutionActivationContext.TryGetPlan(snapshot, identity, out var plan))
        {
            plan = Plan(provider, implementationType, serviceKey);
            var plans = NativeConstructorGraphValidator.Validate(provider, implementationType, serviceKey, plan);
            ResolutionActivationContext.SetPlans(snapshot, plans);
        }
        // Graph planning does not know a dependency's effective native cached key.
        // Recheck injected key types here using the actual key passed to activation.
        plan.ValidateServiceKeyTypes(serviceKey);
        var values = plan.Arguments.Select(argument => argument.Resolve(provider)).ToArray();
        try { return plan.Constructor.Invoke(values); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    internal static ConstructorPlan Plan(IServiceProvider provider, Type implementationType, object? serviceKey, bool graphOnly = false)
    {
        var probe = provider.GetRequiredService<IServiceProviderIsService>();
        var keyedProbe = provider.GetRequiredService<IServiceProviderIsKeyedService>();
        var constructors = implementationType.GetConstructors();
        // Use the same arity ordering as native DI, including its tie ordering.
        Array.Sort(constructors, (left, right) => right.GetParameters().Length.CompareTo(left.GetParameters().Length));
        ConstructorInfo? selected = null;
        ConstructorArgument[]? arguments = null;
        HashSet<Type>? selectedTypes = null;
        var injectedKeyTypes = new List<Type>();
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var candidateArguments = PlanArguments(parameters);
            if (candidateArguments == null) continue;
            if (selected == null)
            {
                selected = constructor;
                arguments = candidateArguments;
            }
            else
            {
                selectedTypes ??= new HashSet<Type>(selected.GetParameters().Select(parameter => parameter.ParameterType));
                // Native ambiguity compares parameter TYPE sets, not arity alone: a
                // permutation/subset is valid; even a shorter non-subset is ambiguous.
                if (parameters.Any(parameter => !selectedTypes.Contains(parameter.ParameterType)))
                    throw new InvalidOperationException($"Unable to activate type '{implementationType}'. The following constructors are ambiguous:{Environment.NewLine}{selected}{Environment.NewLine}{constructor}");
            }
        }
        if (selected == null)
            throw new InvalidOperationException($"No satisfiable public constructor on {implementationType}.");

        return new ConstructorPlan(selected, arguments!, serviceKey, injectedKeyTypes.ToArray());

        ConstructorArgument[]? PlanArguments(ParameterInfo[] parameters)
        {
            var result = new ConstructorArgument[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                object? dependencyKey = null;
                bool injectKey = false;
                foreach (var attribute in parameter.GetCustomAttributes(true))
                {
                    if (serviceKey != null && attribute is ServiceKeyAttribute)
                    {
                        injectedKeyTypes.Add(parameter.ParameterType);
                        if (!graphOnly) ValidateServiceKeyType(parameter.ParameterType, serviceKey);
                        injectKey = true;
                        break;
                    }
                    if (attribute is FromKeyedServicesAttribute fromKey)
                    {
                        dependencyKey = fromKey.LookupMode switch
                        {
                            ServiceKeyLookupMode.InheritKey => serviceKey,
                            ServiceKeyLookupMode.ExplicitKey => fromKey.Key,
                            _ => null
                        };
                        if (dependencyKey != null) break;
                    }
                }
                if (injectKey)
                    result[index] = ConstructorArgument.Constant(serviceKey);
                else if (IsRegistered(parameter.ParameterType, dependencyKey))
                {
                    // A registered factory may return null. Defaults apply to missing
                    // registrations, never to the value returned by a registered service.
                    result[index] = ConstructorArgument.Service(parameter.ParameterType, dependencyKey);
                }
                else if (TryGetDefaultValue(parameter, out var value))
                    result[index] = ConstructorArgument.Constant(value);
                else
                {
                    if (constructors.Length == 1)
                        throw new InvalidOperationException($"Unable to resolve service for type '{parameter.ParameterType}' while attempting to activate '{implementationType}'.");
                    return null;
                }
            }
            return result;
        }

        bool IsRegistered(Type type, object? key)
        {
            if (type.IsConstructedGenericType)
                provider.GetRequiredService<ServiceProviderRegistrationSnapshot>().ValidateGenericConstraints(type, key);
            if (key == null) return probe.IsService(type);
            // Native's public keyed probe reports built-ins for every key, although
            // their implicit call sites are unkeyed. Explicit keyed registrations work.
            if (type == typeof(IServiceProvider) || type == typeof(IServiceScopeFactory)
                || type == typeof(IServiceProviderIsService) || type == typeof(IServiceProviderIsKeyedService))
            {
                var snapshot = provider.GetRequiredService<ServiceProviderRegistrationSnapshot>();
                return snapshot.GetLifetime(type, key) != null || snapshot.GetLifetime(type, KeyedService.AnyKey) != null;
            }
            // The DI 10 public probe does not check AnyKey open-generic fallback.
            return keyedProbe.IsKeyedService(type, key)
                || (type.IsConstructedGenericType && keyedProbe.IsKeyedService(type, KeyedService.AnyKey));
        }
    }

    internal sealed class ConstructorPlan(ConstructorInfo constructor, ConstructorArgument[] arguments, object? serviceKey, Type[] injectedKeyTypes)
    {
        internal object? ServiceKey { get; } = serviceKey;
        internal ConstructorInfo Constructor { get; } = constructor;
        internal ConstructorArgument[] Arguments { get; } = arguments;
        internal void ValidateServiceKeyTypes(object? key)
        {
            foreach (var type in injectedKeyTypes) ValidateServiceKeyType(type, key);
        }
    }

    internal readonly struct ConstructorArgument
    {
        internal Type? ServiceType { get; }
        internal object? Key { get; }
        private object? Value { get; }

        private ConstructorArgument(Type? serviceType, object? key, object? value)
        {
            ServiceType = serviceType;
            Key = key;
            Value = value;
        }

        internal static ConstructorArgument Service(Type type, object? key) => new(type, key, null);
        internal static ConstructorArgument Constant(object? value) => new(null, null, value);
        internal object? Resolve(IServiceProvider provider) => ServiceType == null ? Value
            : Key == null ? provider.GetService(ServiceType)
            : ((IKeyedServiceProvider)provider).GetKeyedService(ServiceType, Key);
    }

    private static void ValidateServiceKeyType(Type type, object? key)
    {
        if (key != null && key != KeyedService.AnyKey && type != typeof(object) && type != key.GetType())
            throw new InvalidOperationException("The ServiceKey parameter type must match the service key type or be object.");
    }

    private static bool TryGetDefaultValue(ParameterInfo parameter, out object? value)
    {
        value = null;
        bool hasDefault;
        try { hasDefault = parameter.HasDefaultValue; }
        catch (FormatException) when (parameter.ParameterType == typeof(DateTime))
        {
            // .NET Framework reflection cannot read a default(DateTime) constant.
            value = default(DateTime);
            return true;
        }
        if (!hasDefault) return false;
        value = parameter.DefaultValue;
        var underlying = Nullable.GetUnderlyingType(parameter.ParameterType);
        if (value == null && parameter.ParameterType.IsValueType && underlying == null)
            // Array elements are zero-initialized even for structs with a public
            // parameterless constructor; optional defaults must not run user code.
            value = Array.CreateInstance(parameter.ParameterType, 1).GetValue(0);
        if (value != null && underlying?.IsEnum == true)
            value = Enum.ToObject(underlying, value);
        return true;
    }
}
