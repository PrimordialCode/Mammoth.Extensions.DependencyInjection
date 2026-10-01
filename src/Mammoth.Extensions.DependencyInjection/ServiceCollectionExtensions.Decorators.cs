using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for IServiceCollection to add services with decorators.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a decorator to the last registration of the service, preserving its lifetime and key.
    /// Repeated calls wrap the preceding decorator, with the last decorator outermost.
    /// </summary>
    /// <remarks>
    /// <para>Each container-created service and decorator is independently tracked and disposed
    /// by its owning scope or provider. Decorators must not dispose their injected inner service.
    /// Instances supplied by the caller remain caller-owned.</para>
    /// <para>Private registrations preserve each layer without colliding with other registrations
    /// or exposing inner layers through service enumeration.</para>
    /// </remarks>
    /// <typeparam name="TService">The service interface or class.</typeparam>
    /// <typeparam name="TDecorator">The decorator implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <exception cref="InvalidOperationException">The service is not registered.</exception>
    public static void Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"Service type {typeof(TService).Name} not registered.");
        var identity = new object();
        var slotType = typeof(DecorationSlot<TService>);
        var keyed = original.IsKeyedService;
        var instance = keyed ? original.KeyedImplementationInstance : original.ImplementationInstance;
        if (instance == null)
        {
            // Factory results are tracked directly by DI, even though the private service type
            // is only a resolution identity. Resolve by Type to avoid casting to the marker.
            services.Add(ServiceDescriptor.DescribeKeyed(slotType, identity, (provider, _) =>
            {
                if (keyed && original.KeyedImplementationFactory != null)
                    return original.KeyedImplementationFactory(provider, original.ServiceKey);
                if (!keyed && original.ImplementationFactory != null)
                    return original.ImplementationFactory(provider);
                var implementation = keyed ? original.KeyedImplementationType! : original.ImplementationType!;
                return ActivatorUtilities.CreateInstance(provider, implementation);
            }, original.Lifetime));
        }
        object CreateDecorator(IServiceProvider provider)
        {
            var inner = instance ?? provider.GetRequiredKeyedService(slotType, identity);
            return ActivatorUtilities.CreateInstance<TDecorator>(provider, inner);
        }
        // Replace in place so IEnumerable<TService> retains registration order.
        services[services.IndexOf(original)] = keyed
            ? ServiceDescriptor.DescribeKeyed(typeof(TService), original.ServiceKey,
                (provider, _) => CreateDecorator(provider), original.Lifetime)
            : ServiceDescriptor.Describe(typeof(TService), CreateDecorator, original.Lifetime);
    }

    internal static bool IsDecorationSlot(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DecorationSlot<>);

    private sealed class DecorationSlot<TService>;
}
