using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Mammoth.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static object CreateDependsOnInstance(IServiceProvider provider, Type target, Dependency[] map, object? serviceKey = null)
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
        bool ambiguous = false;
        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            if (!parameters.All(CanSupply)) continue;
            if (selected == null || parameters.Length > selected.GetParameters().Length)
            {
                selected = candidate;
                ambiguous = false;
            }
            else if (parameters.Length == selected.GetParameters().Length)
                ambiguous = true;
        }
        if (selected == null)
            throw new InvalidOperationException($"No satisfiable public constructor on {target}.");
        if (ambiguous)
            throw new InvalidOperationException($"Multiple equally long satisfiable constructors on {target}.");
        return selected.Invoke(selected.GetParameters().Select(Resolve).ToArray());

        bool CanSupply(ParameterInfo parameter)
        {
            var dependency = Array.Find(map, d => d.ParameterName == parameter.Name);
            if (dependency != null)
                return dependency.T == Dependency.DependencyType.KeyedServices
                    ? keyedProbe.IsKeyedService(parameter.ParameterType, dependency.Value)
                    : AcceptsValue(parameter.ParameterType, dependency.Value);
            if (parameter.IsDefined(typeof(ServiceKeyAttribute), false))
                return AcceptsValue(parameter.ParameterType, serviceKey);
            var fromKey = parameter.GetCustomAttribute<FromKeyedServicesAttribute>();
            return (fromKey != null
                ? keyedProbe.IsKeyedService(parameter.ParameterType, fromKey.Key)
                : ordinaryProbe.IsService(parameter.ParameterType)) || parameter.HasDefaultValue;
        }

        object? Resolve(ParameterInfo parameter)
        {
            var dependency = Array.Find(map, d => d.ParameterName == parameter.Name);
            if (dependency != null)
                return dependency.T == Dependency.DependencyType.KeyedServices
                    ? keyed.GetRequiredKeyedService(parameter.ParameterType, dependency.Value)
                    : dependency.Value;
            if (parameter.IsDefined(typeof(ServiceKeyAttribute), false)) return serviceKey;
            var fromKey = parameter.GetCustomAttribute<FromKeyedServicesAttribute>();
            var registered = fromKey != null
                ? keyedProbe.IsKeyedService(parameter.ParameterType, fromKey.Key)
                : ordinaryProbe.IsService(parameter.ParameterType);
            if (!registered && parameter.HasDefaultValue) return parameter.DefaultValue;
            return fromKey != null
                ? keyed.GetRequiredKeyedService(parameter.ParameterType, fromKey.Key)
                : provider.GetRequiredService(parameter.ParameterType);
        }
    }

    private static bool AcceptsValue(Type type, object? value) => value != null
        ? type.IsInstanceOfType(value)
        : !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
}
