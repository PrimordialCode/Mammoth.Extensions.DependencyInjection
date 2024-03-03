using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Mammoth.Cqrs.Infrastructure.Tests.Infrastructure.ServiceProvider
{
	/// <summary>
	/// Extension methods for IServiceCollection to check if a service is registered.
	/// </summary>
	public static partial class ServiceCollectionExtensions
	{
		/// <summary>
		/// Checks if a service of type <typeparamref name="TService"/> is registered in the <paramref name="services"/> collection.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <returns><c>true</c> if the service is registered; otherwise, <c>false</c>.</returns>
		public static bool IsServiceRegistered<TService>(this IServiceCollection services)
		{
			return IsServiceRegistered(services, typeof(TService));
		}

		/// <summary>
		/// Checks if a service of the specified <paramref name="targetType"/> is registered in the <paramref name="services"/> collection.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="targetType">The type of the service.</param>
		/// <returns><c>true</c> if the service is registered; otherwise, <c>false</c>.</returns>
		public static bool IsServiceRegistered(this IServiceCollection services, Type targetType)
		{
			var descriptors = services.GetServiceDescriptors(targetType);
			return descriptors.Length > 0;
		}

		/// <summary>
		/// Gets an array of <see cref="ServiceDescriptor"/> objects for services that are assignable to or from the specified <paramref name="targetType"/>.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="targetType">The type of the service.</param>
		/// <returns>An array of <see cref="ServiceDescriptor"/> objects.</returns>
		public static ServiceDescriptor[] GetServiceDescriptors(this IServiceCollection services, Type targetType)
		{
			return services.Where(serviceDescriptor =>
				targetType.IsAssignableFrom(serviceDescriptor.ServiceType)
				|| serviceDescriptor.ServiceType.IsAssignableFrom(targetType))
				.ToArray();
		}

		/// <summary>
		/// Checks if the last registration of a service of the specified <paramref name="type"/> is transient.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="type">The type of the service.</param>
		/// <returns><c>true</c> if the last registration of the service is transient; otherwise, <c>false</c>.</returns>
		/// <exception cref="Exception">Thrown when the service is not registered.</exception>
		public static bool IsTransientServiceRegistered(this IServiceCollection services, Type type)
		{
			var descriptors = GetServiceDescriptors(services, type);
			if (descriptors.Length == 0)
			{
				throw new Exception($"Service {type.FullName} is not registered.");
			}
			return descriptors.Last().Lifetime == ServiceLifetime.Transient;
		}

		/// <summary>
		/// Checks if the last registration of a service of type <typeparamref name="TService"/> is transient.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <returns><c>true</c> if the last registration of the service is transient; otherwise, <c>false</c>.</returns>
		public static bool IsTransientServiceRegistered<TService>(this IServiceCollection services)
		{
			return IsTransientServiceRegistered(services, typeof(TService));
		}

		/// <summary>
		/// Checks if the last registration of a service of the specified <paramref name="type"/> is singleton.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="type">The type of the service.</param>
		/// <returns><c>true</c> if the last registration of the service is singleton; otherwise, <c>false</c>.</returns>
		/// <exception cref="Exception">Thrown when the service is not registered.</exception>
		public static bool IsSingletonServiceRegistered(this IServiceCollection services, Type type)
		{
			var descriptors = GetServiceDescriptors(services, type);
			if (descriptors.Length == 0)
			{
				throw new Exception($"Service {type.FullName} is not registered.");
			}
			return descriptors.Last().Lifetime == ServiceLifetime.Singleton;
		}

		/// <summary>
		/// Checks if the last registration of a service of type <typeparamref name="TService"/> is singleton.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <returns><c>true</c> if the last registration of the service is singleton; otherwise, <c>false</c>.</returns>
		public static bool IsSingletonServiceRegistered<TService>(this IServiceCollection services)
		{
			return IsSingletonServiceRegistered(services, typeof(TService));
		}
	}
}
