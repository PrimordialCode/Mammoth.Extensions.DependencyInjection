using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for IServiceCollection to check if a service is registered.
	/// </summary>
	public static partial class ServiceCollectionExtensions
	{
		/// <summary>
		/// Checks if a service of type <typeparamref name="TServiceType"/> is registered in the <paramref name="services"/> collection.
		/// </summary>
		/// <typeparam name="TServiceType">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <returns><c>true</c> if the service type is registered; otherwise, <c>false</c>.</returns>
		public static bool IsServiceRegistered<TServiceType>(this IServiceCollection services)
		{
			return services.IsServiceRegistered(typeof(TServiceType));
		}

		/// <summary>
		/// Checks if a service of the specified <paramref name="serviceType"/> is registered in the <paramref name="services"/> collection.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <returns><c>true</c> if the service type is registered; otherwise, <c>false</c>.</returns>
		public static bool IsServiceRegistered(this IServiceCollection services, Type serviceType)
		{
			var descriptors = services.GetServiceDescriptors(serviceType);
			return descriptors.Length > 0;
		}

		/// <summary>
		/// Checks if a service with the specified service key is registered in the <paramref name="services"/> collection.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the service is registered; otherwise, <c>false</c>.</returns>
		public static bool IsServiceRegistered(this IServiceCollection services, object? serviceKey)
		{
			return services.Any(s => s.IsKeyedService && s.ServiceKey == serviceKey);
		}

		/// <summary>
		/// Gets an array of <see cref="ServiceDescriptor"/> objects for services that are assignable to or from the specified <paramref name="serviceType"/>.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <returns>An array of <see cref="ServiceDescriptor"/> objects.</returns>
		public static ServiceDescriptor[] GetServiceDescriptors(this IServiceCollection services, Type serviceType)
		{
			return services.Where(serviceDescriptor =>
				serviceType.IsAssignableFrom(serviceDescriptor.ServiceType)
				|| serviceDescriptor.ServiceType.IsAssignableFrom(serviceType))
				.ToArray();
		}

		/// <summary>
		/// Checks if the last registration of a service of the specified <paramref name="serviceType"/> is transient.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <returns><c>true</c> if the last registration of the service is transient; otherwise, <c>false</c>.</returns>
		/// <exception cref="Exception">Thrown when the service is not registered.</exception>
		public static bool IsTransientServiceRegistered(this IServiceCollection services, Type serviceType)
		{
			var descriptors = services.GetServiceDescriptors(serviceType);
			if (descriptors.Length == 0)
			{
				throw new Exception($"Service {serviceType.FullName} is not registered.");
			}
			return descriptors.Last().Lifetime == ServiceLifetime.Transient;
		}

		/// <summary>
		/// Checks if the last registration of a service of type <typeparamref name="TServiceType"/> is transient.
		/// </summary>
		/// <typeparam name="TServiceType">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <returns><c>true</c> if the last registration of the service is transient; otherwise, <c>false</c>.</returns>
		public static bool IsTransientServiceRegistered<TServiceType>(this IServiceCollection services)
		{
			return services.IsTransientServiceRegistered(typeof(TServiceType));
		}

		/// <summary>
		/// Checks if the last registration of a service of the specified <paramref name="serviceType"/> is scoped.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <returns><c>true</c> if the last registration of the service is scoped; otherwise, <c>false</c>.</returns>
		/// <exception cref="Exception">Thrown when the service is not registered.</exception>
		public static bool IsScopedServiceRegistered(this IServiceCollection services, Type serviceType)
		{
			var descriptors = services.GetServiceDescriptors(serviceType);
			if (descriptors.Length == 0)
			{
				throw new Exception($"Service {serviceType.FullName} is not registered.");
			}
			return descriptors.Last().Lifetime == ServiceLifetime.Scoped;
		}

		/// <summary>
		/// Checks if the last registration of a service of type <typeparamref name="TServiceType"/> is scoped.
		/// </summary>
		/// <typeparam name="TServiceType">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <returns><c>true</c> if the last registration of the service is scoped; otherwise, <c>false</c>.</returns>
		public static bool IsScopedServiceRegistered<TServiceType>(this IServiceCollection services)
		{
			return services.IsScopedServiceRegistered(typeof(TServiceType));
		}

		/// <summary>
		/// Checks if the last registration of a service of the specified <paramref name="serviceType"/> is singleton.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <returns><c>true</c> if the last registration of the service is singleton; otherwise, <c>false</c>.</returns>
		/// <exception cref="Exception">Thrown when the service is not registered.</exception>
		public static bool IsSingletonServiceRegistered(this IServiceCollection services, Type serviceType)
		{
			var descriptors = services.GetServiceDescriptors(serviceType);
			if (descriptors.Length == 0)
			{
				throw new Exception($"Service {serviceType.FullName} is not registered.");
			}
			return descriptors.Last().Lifetime == ServiceLifetime.Singleton;
		}

		/// <summary>
		/// Checks if the last registration of a service of type <typeparamref name="TServiceType"/> is singleton.
		/// </summary>
		/// <typeparam name="TServiceType">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <returns><c>true</c> if the last registration of the service is singleton; otherwise, <c>false</c>.</returns>
		public static bool IsSingletonServiceRegistered<TServiceType>(this IServiceCollection services)
		{
			return services.IsSingletonServiceRegistered(typeof(TServiceType));
		}
	}
}
