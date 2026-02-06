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
			// If the original descriptor uses a factory function, capture the factory directly.
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

			// If the original descriptor uses an instance, capture it directly.
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

			// For type registrations, swap the service type to the implementation type
			// and resolve via the container (implementation type differs from service type for interfaces).
			var implementationType = originalServiceDescriptor.ImplementationType!;
			if (implementationType != typeof(TService))
			{
				// Interface-based: the implementation type is different from the service type,
				// so we can safely re-register under the implementation type.
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
				// Class-based: service type == implementation type, so we can't re-register
				// under the same type. Use a factory that creates the original directly.
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
			// If the original descriptor uses a keyed factory function, capture the factory directly.
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

			// If the original descriptor uses a keyed instance, capture it directly.
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

			// For keyed type registrations
			var implementationType = originalServiceDescriptor.KeyedImplementationType!;
			if (implementationType != typeof(TService))
			{
				// Interface-based: re-register under the implementation type.
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
				// Class-based: service type == implementation type, use factory creation.
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
