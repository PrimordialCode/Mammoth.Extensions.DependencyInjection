using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Mammoth.Extensions.DependencyInjection;

// Diagnostics and decoration replace implementation-type descriptors with factories.
// Keep native DI's constructor rules separate from ActivatorUtilities / DependsOn rules.
internal static class NativeConstructorActivator
{
    internal static object CreateInstance(IServiceProvider provider, Type implementationType, object? serviceKey = null)
    {
        var probe = provider.GetRequiredService<IServiceProviderIsService>();
        // Ordinary original activation needs only the ordinary probe. Acquire the
        // keyed probe when a non-null dependency key actually requires planning it.
        IServiceProviderIsKeyedService? keyedProbe = null;
        var constructors = implementationType.GetConstructors();
        // Use the same arity ordering as native DI, including its tie ordering.
        Array.Sort(constructors, (left, right) => right.GetParameters().Length.CompareTo(left.GetParameters().Length));
        ConstructorInfo? selected = null;
        Func<object?>[]? arguments = null;
        HashSet<Type>? selectedTypes = null;
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

        // No service is activated until all constructor candidates have been checked.
        var values = arguments!.Select(resolve => resolve()).ToArray();
        try
        {
            return selected.Invoke(values);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }

        Func<object?>[]? PlanArguments(ParameterInfo[] parameters)
        {
            var result = new Func<object?>[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                object? dependencyKey = null;
                bool injectKey = false;
                foreach (var attribute in parameter.GetCustomAttributes(true))
                {
                    if (serviceKey != null && attribute is ServiceKeyAttribute)
                    {
                        if (serviceKey != KeyedService.AnyKey && parameter.ParameterType != typeof(object)
                            && parameter.ParameterType != serviceKey.GetType())
                            throw new InvalidOperationException("The ServiceKey parameter type must match the service key type or be object.");
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
                    result[index] = () => serviceKey;
                else if (IsRegistered(parameter.ParameterType, dependencyKey))
                {
                    // A registered factory may return null. Defaults apply to missing
                    // registrations, never to the value returned by a registered service.
                    result[index] = dependencyKey == null
                        ? () => provider.GetService(parameter.ParameterType)
                        : () => ((IKeyedServiceProvider)provider).GetKeyedService(parameter.ParameterType, dependencyKey);
                }
                // Keep diagnostic defaults consistent with mapped/contextual keyed activation.
                else if (ParameterDefaultValue.TryGetDefaultValue(parameter, out var value))
                    result[index] = () => value;
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
            if (key == null)
            {
                if (type.IsConstructedGenericType)
                    ConstructorActivator.GetRegistrationSnapshot(provider, probe)?.ValidateGenericConstraints(type, key);
                return probe.IsService(type);
            }
            keyedProbe ??= provider.GetRequiredService<IServiceProviderIsKeyedService>();
            if (type.IsConstructedGenericType)
                ConstructorActivator.GetRegistrationSnapshot(provider, keyedProbe)?.ValidateGenericConstraints(type, key);
            // Native's public keyed probe reports built-ins for every key, although
            // their implicit call sites are unkeyed. Explicit keyed registrations work.
            if (type == typeof(IServiceProvider) || type == typeof(IServiceScopeFactory)
                || type == typeof(IServiceProviderIsService) || type == typeof(IServiceProviderIsKeyedService))
            {
                return ConstructorActivator.IsKeyedBuiltInRegistered(provider, keyedProbe, type, key);
            }
            // The DI 10 public probe does not check AnyKey open-generic fallback.
            return keyedProbe.IsKeyedService(type, key)
                || (type.IsConstructedGenericType && keyedProbe.IsKeyedService(type, KeyedService.AnyKey));
        }
    }
}
