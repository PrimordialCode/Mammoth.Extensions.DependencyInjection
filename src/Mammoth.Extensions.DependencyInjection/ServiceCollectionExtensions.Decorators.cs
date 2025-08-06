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
		/// <typeparam name="TInterface">The type of the service interface.</typeparam>
		/// <typeparam name="TDecorator">The type of the decorator.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <exception cref="InvalidOperationException">Thrown when the service type is not registered.</exception>
		public static void Decorate<TInterface, TDecorator>(this IServiceCollection services)
			where TInterface : class
			where TDecorator : class, TInterface
		{
			var originalServiceDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(TInterface))
				?? throw new InvalidOperationException($"Service type {typeof(TInterface).Name} not registered.");

			// Remove the original service descriptor that has the service type
			services.Remove(originalServiceDescriptor);

			// Create a new service descriptor for the decorator that wraps the original service.
			// This works by preserving the original registration information (type, instance, or factory)
			// and creating a new factory that first creates the original service, then passes it to the decorator.
			// This approach eliminates the need for reflection-based proxy creation while supporting
			// both interfaces and concrete classes.
			ServiceDescriptor newServiceDescriptor;
			if (!originalServiceDescriptor.IsKeyedService)
			{
				newServiceDescriptor = new ServiceDescriptor(
					typeof(TInterface),
					serviceProvider =>
					{
						TInterface originalService = CreateOriginalService<TInterface>(originalServiceDescriptor, serviceProvider);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				);
			}
			else
			{
				newServiceDescriptor = new ServiceDescriptor(
					typeof(TInterface),
					originalServiceDescriptor.ServiceKey,
					(serviceProvider, _) =>
					{
						TInterface originalService = CreateOriginalKeyedService<TInterface>(originalServiceDescriptor, serviceProvider, originalServiceDescriptor.ServiceKey);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				);
			}

			// Add the decorator service descriptor
			services.Add(newServiceDescriptor);
		}

		/// <summary>
		/// Creates the original service instance based on the type of registration in the ServiceDescriptor.
		/// This method handles all three registration types without requiring reflection-based proxies:
		/// - Type registrations: Uses ActivatorUtilities.CreateInstance to create the implementation type
		/// - Instance registrations: Returns the pre-registered instance directly  
		/// - Factory registrations: Calls the original factory function directly
		/// This approach preserves the original registration behavior while enabling decoration.
		/// </summary>
		private static T CreateOriginalService<T>(ServiceDescriptor originalDescriptor, IServiceProvider serviceProvider) where T : class
		{
			if (originalDescriptor.ImplementationType != null)
			{
				return (T)ActivatorUtilities.CreateInstance(serviceProvider, originalDescriptor.ImplementationType);
			}

			if (originalDescriptor.ImplementationInstance != null)
			{
				return (T)originalDescriptor.ImplementationInstance;
			}

			if (originalDescriptor.ImplementationFactory != null)
			{
				return (T)originalDescriptor.ImplementationFactory(serviceProvider);
			}

			throw new InvalidOperationException($"Unable to create original service for type {typeof(T).Name}.");
		}

		/// <summary>
		/// Creates the original keyed service instance, handling the same three registration types as CreateOriginalService
		/// but for keyed services. The service key is passed through to maintain proper keyed service resolution.
		/// </summary>
		private static T CreateOriginalKeyedService<T>(ServiceDescriptor originalDescriptor, IServiceProvider serviceProvider, object? serviceKey) where T : class
		{
			if (originalDescriptor.KeyedImplementationType != null)
			{
				return (T)ActivatorUtilities.CreateInstance(serviceProvider, originalDescriptor.KeyedImplementationType);
			}

			if (originalDescriptor.KeyedImplementationInstance != null)
			{
				return (T)originalDescriptor.KeyedImplementationInstance;
			}

			if (originalDescriptor.KeyedImplementationFactory != null)
			{
				return (T)originalDescriptor.KeyedImplementationFactory(serviceProvider, serviceKey);
			}

			throw new InvalidOperationException($"Unable to create original keyed service for type {typeof(T).Name}.");
		}
	}
}
