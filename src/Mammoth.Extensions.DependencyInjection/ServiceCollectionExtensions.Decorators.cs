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
    /// <exception cref="ArgumentNullException">The service collection is null.</exception>
    /// <exception cref="InvalidOperationException">The service is not registered.</exception>
    public static void Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        // Keep the occurrence index: the same descriptor reference can appear more than once.
        var index = services.Count - 1;
        while (index >= 0 && services[index].ServiceType != typeof(TService))
            index--;
        if (index < 0)
            throw new InvalidOperationException($"Service type {typeof(TService).Name} not registered.");
        var original = services[index];
        var resolveInner = RegisterInnerLayer<TService>(services, original);
        object CreateDecorator(IServiceProvider provider, object? requestedKey)
        {
            var inner = resolveInner(provider, requestedKey);
            return original.IsKeyedService ? ConstructorActivator.CreateKeyed(provider, typeof(TDecorator), requestedKey, inner)
                : ActivatorUtilities.CreateInstance<TDecorator>(provider, inner);
        }
        // Replace in place so IEnumerable<TService> retains registration order.
        services[index] = original.IsKeyedService
            ? ServiceDescriptor.DescribeKeyed(typeof(TService), original.ServiceKey,
                (provider, requestedKey) => CreateDecorator(provider, requestedKey), original.Lifetime)
            : ServiceDescriptor.Describe(typeof(TService), provider => CreateDecorator(provider, null), original.Lifetime);
    }

    private static Func<IServiceProvider, object?, object> RegisterInnerLayer<TService>(IServiceCollection services, ServiceDescriptor original)
        where TService : class
    {
        var keyed = original.IsKeyedService;
        var instance = keyed ? original.KeyedImplementationInstance : original.ImplementationInstance;
        if (instance != null) return (_, _) => instance;
        var identity = new object();
        var slotType = typeof(DecorationSlot<TService>);
        if (keyed)
        {
            // Give each keyed layer its own service type, so native DI can cache it
            // by the actual requested key without colliding with another layer.
            while (services.Any(d => d.ServiceType == slotType))
                slotType = typeof(DecorationSlot<>).MakeGenericType(slotType);
        }
        var slotKey = keyed ? KeyedService.AnyKey : identity;
        // Factory results are tracked directly by DI, even though the private service type
        // is only a resolution identity. Resolve by Type to avoid casting to the marker.
        services.Add(ServiceDescriptor.DescribeKeyed(slotType, slotKey,
            (provider, requestedKey) => CreateRegisteredInstance(provider, original, requestedKey), original.Lifetime));
        return (provider, requestedKey) => provider.GetRequiredKeyedService(slotType, keyed ? requestedKey : identity);
    }

    private static object CreateRegisteredInstance(IServiceProvider provider, ServiceDescriptor descriptor, object? requestedKey)
    {
        var keyed = descriptor.IsKeyedService;
        if (keyed && descriptor.KeyedImplementationFactory != null)
            return descriptor.KeyedImplementationFactory(provider, requestedKey);
        if (!keyed && descriptor.ImplementationFactory != null)
            return descriptor.ImplementationFactory(provider);
        var implementation = keyed ? descriptor.KeyedImplementationType! : descriptor.ImplementationType!;
        return keyed ? ConstructorActivator.CreateKeyed(provider, implementation, requestedKey)
            : ActivatorUtilities.CreateInstance(provider, implementation);
    }

    internal static bool IsDecorationSlot(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DecorationSlot<>);

    private sealed class DecorationSlot<TService>;
}
