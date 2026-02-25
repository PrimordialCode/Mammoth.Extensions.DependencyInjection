using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for IServiceCollection to add services with decorators.
	/// </summary>
	public static partial class ServiceCollectionExtensions
	{
		/// <summary>
		/// <para>Adds a decorator for a service implementation to the service collection.</para>
		/// <para>The decorator will be applied to closest service that match the registration (that is the one immediately above the Decorate call).</para>
		/// <para>Multiple decorators can be added for the same service implementation.</para>
		/// <para>The last decorator added will be the outer most decorator.</para>
		/// </summary>
		/// <remarks>
		/// <para><b>Algorithm overview:</b></para>
		/// <para>
		/// The decorator replaces the original service descriptor with a new one that wraps the
		/// original service in a decorator. The approach varies by registration style:
		/// </para>
		/// <list type="bullet">
		/// <item><description>
		/// <b>Factory registrations:</b> The original factory delegate is captured directly in the
		/// decorator's factory closure. The decorator factory invokes the original factory to obtain
		/// the inner service, then uses <see cref="ActivatorUtilities"/> to create the decorator
		/// with the inner service injected. No type swapping or proxy creation is needed.
		/// </description></item>
		/// <item><description>
		/// <b>Instance registrations:</b> The original instance is captured directly in the
		/// decorator's factory closure. The decorator is created via <see cref="ActivatorUtilities"/>
		/// with the captured instance injected.
		/// </description></item>
		/// <item><description>
		/// <b>Type registrations (interface-based, i.e. service type ≠ implementation type):</b>
		/// The original descriptor is re-registered under its implementation type (via
		/// <see cref="ServiceDescriptorExtensions.ChangeServiceType"/>), and the decorator factory
		/// resolves the inner service from the container by that implementation type.
		/// </description></item>
		/// <item><description>
		/// <b>Type registrations (class-based, i.e. service type == implementation type):</b>
		/// Since re-registering under the same type would cause infinite recursion, the decorator
		/// factory creates the inner service directly via <see cref="ActivatorUtilities.CreateInstance"/>
		/// and then wraps it in the decorator.
		/// </description></item>
		/// </list>
		/// <para>
		/// In all cases, the resulting <see cref="ServiceDescriptor"/> preserves the original lifetime
		/// (Transient/Scoped/Singleton). The DI container manages caching according to that lifetime.
		/// </para>
		/// </remarks>
		/// <typeparam name="TService">The type of the service (interface or class).</typeparam>
		/// <typeparam name="TDecorator">The type of the decorator.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <exception cref="InvalidOperationException">Thrown when the service type is not registered.</exception>
		public static void Decorate<TService, TDecorator>(this IServiceCollection services)
			where TService : class
			where TDecorator : class, TService
		{
			var originalServiceDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
				?? throw new InvalidOperationException($"Service type {typeof(TService).Name} not registered.");

			services.Remove(originalServiceDescriptor);

			if (!originalServiceDescriptor.IsKeyedService)
			{
				DecorateNonKeyed<TService, TDecorator>(services, originalServiceDescriptor);
			}
			else
			{
				DecorateKeyed<TService, TDecorator>(services, originalServiceDescriptor);
			}
		}

		private static void DecorateNonKeyed<TService, TDecorator>(IServiceCollection services, ServiceDescriptor originalServiceDescriptor)
			where TService : class
			where TDecorator : class, TService
		{
			// Factory registration: capture the original factory directly in the decorator closure.
			// The decorator factory invokes the original factory to get the inner service, then wraps it.
			if (originalServiceDescriptor.ImplementationFactory != null)
			{
				var originalFactory = originalServiceDescriptor.ImplementationFactory;
				services.Add(new ServiceDescriptor(
					typeof(TService),
					serviceProvider =>
					{
						TService originalService = (TService)originalFactory(serviceProvider);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				));
				return;
			}

			// Instance registration: capture the original instance directly in the decorator closure.
			if (originalServiceDescriptor.ImplementationInstance != null)
			{
				var originalInstance = originalServiceDescriptor.ImplementationInstance;
				services.Add(new ServiceDescriptor(
					typeof(TService),
					serviceProvider =>
					{
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalInstance);
					},
					originalServiceDescriptor.Lifetime
				));
				return;
			}

			// Type registration: behavior depends on whether service type differs from implementation type.
			var implementationType = originalServiceDescriptor.ImplementationType!;
			if (implementationType != typeof(TService))
			{
				// Interface-based (service type ≠ implementation type): re-register the original
				// under its implementation type, then resolve it in the decorator factory.
				var replacementDescriptor = originalServiceDescriptor.ChangeServiceType(implementationType);
				services.Add(replacementDescriptor);

				services.Add(new ServiceDescriptor(
					typeof(TService),
					serviceProvider =>
					{
						TService originalService = (TService)serviceProvider.GetRequiredService(replacementDescriptor.ServiceType);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				));
			}
			else
			{
				// Class-based (service type == implementation type): can't re-register under the
				// same type (would cause infinite recursion), so create the inner service directly.
				services.Add(new ServiceDescriptor(
					typeof(TService),
					serviceProvider =>
					{
						TService originalService = (TService)ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				));
			}
		}

		private static void DecorateKeyed<TService, TDecorator>(IServiceCollection services, ServiceDescriptor originalServiceDescriptor)
			where TService : class
			where TDecorator : class, TService
		{
			// Keyed factory registration: capture the original keyed factory directly in the decorator closure.
			if (originalServiceDescriptor.KeyedImplementationFactory != null)
			{
				var originalFactory = originalServiceDescriptor.KeyedImplementationFactory;
				var serviceKey = originalServiceDescriptor.ServiceKey;
				services.Add(new ServiceDescriptor(
					typeof(TService),
					serviceKey,
					(serviceProvider, key) =>
					{
						TService originalService = (TService)originalFactory(serviceProvider, key);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				));
				return;
			}

			// Keyed instance registration: capture the original keyed instance directly in the decorator closure.
			if (originalServiceDescriptor.KeyedImplementationInstance != null)
			{
				var originalInstance = originalServiceDescriptor.KeyedImplementationInstance;
				var serviceKey = originalServiceDescriptor.ServiceKey;
				services.Add(new ServiceDescriptor(
					typeof(TService),
					serviceKey,
					(serviceProvider, _) =>
					{
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalInstance);
					},
					originalServiceDescriptor.Lifetime
				));
				return;
			}

			// Keyed type registration: behavior depends on whether service type differs from implementation type.
			var implementationType = originalServiceDescriptor.KeyedImplementationType!;
			if (implementationType != typeof(TService))
			{
				// Interface-based (service type ≠ implementation type): re-register under the
				// implementation type, then resolve it by key in the decorator factory.
				var replacementDescriptor = originalServiceDescriptor.ChangeServiceType(implementationType);
				services.Add(replacementDescriptor);

				services.Add(new ServiceDescriptor(
					typeof(TService),
					originalServiceDescriptor.ServiceKey,
					(serviceProvider, _) =>
					{
						TService originalService = (TService)serviceProvider.GetRequiredKeyedService(replacementDescriptor.ServiceType, originalServiceDescriptor.ServiceKey);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				));
			}
			else
			{
				// Class-based (service type == implementation type): can't re-register under the
				// same type (would cause infinite recursion), so create the inner service directly.
				services.Add(new ServiceDescriptor(
					typeof(TService),
					originalServiceDescriptor.ServiceKey,
					(serviceProvider, _) =>
					{
						TService originalService = (TService)ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				));
			}
		}
	}
}
