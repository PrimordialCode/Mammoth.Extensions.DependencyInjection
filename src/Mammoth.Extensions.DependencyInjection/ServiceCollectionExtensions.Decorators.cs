using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

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

			// Throw exception if TInterface is not an interface
			if (!typeof(TInterface).IsInterface)
			{
				throw new InvalidOperationException($"Service type {typeof(TInterface).Name} is not an interface.");
			}

			// Remove the original service descriptor that has the interface as the service type
			// Create a new descriptor that has the implementation type as new service type.
			// If the original descriptor was registered with a factory function, a new proxy type will be created
			// and used as the service type to replace the original descriptor.
			// A new ServiceDescriptor will be created with the interface as the service type and the decorator as the implementation type.

			services.Remove(originalServiceDescriptor);
			var implementationType = originalServiceDescriptor.GetImplementationType()
				?? throw new InvalidOperationException($"Service type {typeof(TInterface).Name} does not have an implementation type.");
			var originalServiceDescriptorReplacement = originalServiceDescriptor.ChangeServiceType(implementationType);
			services.Add(originalServiceDescriptorReplacement);

			// Create a new service descriptor for the decorator
			ServiceDescriptor newServiceDescriptor;
			if (!originalServiceDescriptor.IsKeyedService)
			{
				newServiceDescriptor = new ServiceDescriptor(
					typeof(TInterface),
					serviceProvider =>
					{
						TInterface originalService = (TInterface)serviceProvider.GetRequiredService(originalServiceDescriptorReplacement.ServiceType);
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
					(serviceProvider, key) =>
					{
						TInterface originalService = (TInterface)serviceProvider.GetRequiredKeyedService(originalServiceDescriptorReplacement.ServiceType, originalServiceDescriptor.ServiceKey);
						return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, originalService);
					},
					originalServiceDescriptor.Lifetime
				);
			}

			// Replace or insert the service descriptor
			services.Add(newServiceDescriptor);
		}
	}
}
