using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Mammoth.Extensions.DependencyInjection;

internal static class ConstructorActivator
{
    internal static object CreateKeyed(IServiceProvider provider, Type target, object? serviceKey, object? inner = null)
    {
        // Keep ActivatorUtilities behavior for constructors without contextual parameters.
        if (!target.GetConstructors().Any(c => c.GetParameters().Any(p =>
            p.IsDefined(typeof(ServiceKeyAttribute), false) ||
            p.GetCustomAttribute<FromKeyedServicesAttribute>()?.LookupMode == ServiceKeyLookupMode.InheritKey)))
            return inner == null ? ActivatorUtilities.CreateInstance(provider, target)
                : ActivatorUtilities.CreateInstance(provider, target, inner);
        try
        {
            return CreateInstance(provider, target, [], serviceKey, inner);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    internal static object CreateDependsOn(IServiceProvider provider, Type target, Dependency[] map, object? serviceKey = null)
        => CreateInstance(provider, target, map, serviceKey);

    private static object CreateInstance(IServiceProvider provider, Type target, Dependency[] map, object? serviceKey, object? inner = null)
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
        int selectedInnerIndex = -1;
        bool ambiguous = false;
        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            // Only decorators supply an inner service. ServiceKey belongs to the key context,
            // even when an object-typed key parameter appears before the inner parameter.
            var innerIndex = inner == null ? -1 : Array.FindIndex(parameters, p =>
                !p.IsDefined(typeof(ServiceKeyAttribute), false) && AcceptsValue(p.ParameterType, inner));
            if (inner != null && innerIndex < 0) continue;
            if (!parameters.Where((_, index) => index != innerIndex).All(CanSupply)) continue;
            if (selected == null || parameters.Length > selected.GetParameters().Length)
            {
                selected = candidate;
                selectedInnerIndex = innerIndex;
                ambiguous = false;
            }
            else if (parameters.Length == selected.GetParameters().Length)
                ambiguous = true;
        }
        if (selected == null)
            throw new InvalidOperationException($"No satisfiable public constructor on {target}.");
        if (ambiguous)
            throw new InvalidOperationException($"Multiple equally long satisfiable constructors on {target}.");
        return selected.Invoke(selected.GetParameters().Select((parameter, index) =>
            index == selectedInnerIndex ? inner : Resolve(parameter)).ToArray());

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
