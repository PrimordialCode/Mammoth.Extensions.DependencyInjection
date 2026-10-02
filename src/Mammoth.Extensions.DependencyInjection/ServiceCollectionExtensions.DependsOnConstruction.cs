using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Mammoth.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    internal static object CreateKeyedInstance(IServiceProvider provider, Type target, object? serviceKey, params object[] arguments)
    {
        // Keep ActivatorUtilities behavior for constructors without contextual parameters.
        if (!target.GetConstructors().Any(c => c.GetParameters().Any(p =>
            p.IsDefined(typeof(ServiceKeyAttribute), false) ||
            p.GetCustomAttribute<FromKeyedServicesAttribute>()?.LookupMode == ServiceKeyLookupMode.InheritKey)))
            return ActivatorUtilities.CreateInstance(provider, target, arguments);
        try
        {
            return CreateDependsOnInstance(provider, target, [], serviceKey, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static object CreateDependsOnInstance(IServiceProvider provider, Type target, Dependency[] map, object? serviceKey = null, params object[] arguments)
    {
        if (provider is not IKeyedServiceProvider keyed)
            throw new NotSupportedException($"ServiceProvider must be an {nameof(IKeyedServiceProvider)}");
        var ordinaryProbe = provider.GetService<IServiceProviderIsService>()
            ?? throw new NotSupportedException("DependsOn constructor selection requires IServiceProviderIsService.");
        var keyedProbe = provider.GetService<IServiceProviderIsKeyedService>()
            ?? throw new NotSupportedException("DependsOn constructor selection requires IServiceProviderIsKeyedService.");
        var constructors = target.GetConstructors();
        var preferred = constructors.Where(c => c.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), false)).ToArray();
        if (preferred.Length > 1)
            throw new InvalidOperationException($"Multiple preferred constructors on {target}.");
        var candidates = preferred.Length == 1 ? preferred : constructors;
        ConstructorInfo? selected = null;
        Dictionary<string, object>? selectedArguments = null;
        bool ambiguous = false;
        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            var supplied = new Dictionary<string, object>();
            foreach (var argument in arguments)
            {
                var parameter = parameters.FirstOrDefault(p => !supplied.ContainsKey(p.Name!) &&
                    !p.IsDefined(typeof(ServiceKeyAttribute), false) && AcceptsValue(p.ParameterType, argument));
                if (parameter == null) break;
                supplied.Add(parameter.Name!, argument);
            }
            if (supplied.Count != arguments.Length || !parameters.All(p => supplied.ContainsKey(p.Name!) || CanSupply(p))) continue;
            if (selected == null || parameters.Length > selected.GetParameters().Length)
            {
                selected = candidate;
                selectedArguments = supplied;
                ambiguous = false;
            }
            else if (parameters.Length == selected.GetParameters().Length)
                ambiguous = true;
        }
        if (selected == null)
            throw new InvalidOperationException($"No satisfiable public constructor on {target}.");
        if (ambiguous)
            throw new InvalidOperationException($"Multiple equally long satisfiable constructors on {target}.");
        return selected.Invoke(selected.GetParameters().Select(p =>
            selectedArguments!.TryGetValue(p.Name!, out var value) ? value : Resolve(p)).ToArray());

        bool CanSupply(ParameterInfo parameter)
        {
            var dependency = Array.Find(map, d => d.ParameterName == parameter.Name);
            if (dependency != null)
                return dependency.T == Dependency.DependencyType.KeyedServices
                    ? keyedProbe.IsKeyedService(parameter.ParameterType, dependency.Value)
                    : AcceptsValue(parameter.ParameterType, dependency.Value);
            if (parameter.IsDefined(typeof(ServiceKeyAttribute), false))
            {
                if (serviceKey != null && parameter.ParameterType != typeof(object) && parameter.ParameterType != serviceKey.GetType())
                    throw new InvalidOperationException("The ServiceKey parameter type must match the service key type or be object.");
                return AcceptsValue(parameter.ParameterType, serviceKey);
            }
            return IsRegistered(parameter) || parameter.HasDefaultValue;
        }

        bool IsRegistered(ParameterInfo parameter)
        {
            var fromKey = parameter.GetCustomAttribute<FromKeyedServicesAttribute>();
            var key = EffectiveKey(fromKey, serviceKey);
            return key != null
                ? keyedProbe.IsKeyedService(parameter.ParameterType, key)
                : ordinaryProbe.IsService(parameter.ParameterType);
        }

        object? Resolve(ParameterInfo parameter)
        {
            var dependency = Array.Find(map, d => d.ParameterName == parameter.Name);
            if (dependency != null)
                return dependency.T == Dependency.DependencyType.KeyedServices
                    ? keyed.GetRequiredKeyedService(parameter.ParameterType, dependency.Value)
                    : dependency.Value;
            if (parameter.IsDefined(typeof(ServiceKeyAttribute), false)) return serviceKey;
            if (!IsRegistered(parameter) && parameter.HasDefaultValue) return parameter.DefaultValue;
            var key = EffectiveKey(parameter.GetCustomAttribute<FromKeyedServicesAttribute>(), serviceKey);
            return key != null
                ? keyed.GetRequiredKeyedService(parameter.ParameterType, key)
                : provider.GetRequiredService(parameter.ParameterType);
        }
    }

    private static object? EffectiveKey(FromKeyedServicesAttribute? attribute, object? serviceKey) => attribute?.LookupMode switch
    {
        ServiceKeyLookupMode.InheritKey => serviceKey,
        ServiceKeyLookupMode.ExplicitKey => attribute.Key,
        _ => null
    };

    private static bool AcceptsValue(Type type, object? value) => value != null
        ? type.IsInstanceOfType(value)
        : !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
}
