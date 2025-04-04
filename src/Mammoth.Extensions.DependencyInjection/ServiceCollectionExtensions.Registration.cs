﻿using Microsoft.Extensions.DependencyInjection;

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
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}

			var descriptors = services.GetServiceDescriptors(serviceType);
			return descriptors.Length > 0;
		}

		/// <summary>
		/// Checks if a service with the specified service key is registered in the <paramref name="services"/> collection.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the service is registered; otherwise, <c>false</c>.</returns>
		public static bool IsKeyedServiceRegistered(this IServiceCollection services, object serviceKey)
		{
			if (serviceKey is null)
			{
				throw new ArgumentNullException(nameof(serviceKey));
			}
			return services.Any(s => s.IsKeyedService && s.ServiceKey == serviceKey);
		}

		/// <summary>
		/// Gets an array of <see cref="ServiceDescriptor"/> objects for services that are assignable to or from the specified <paramref name="serviceType"/>.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="isKeyedService">Indicates whether the service is keyed or not. If null both kinds of services will be returned</param>
		/// <returns>An array of <see cref="ServiceDescriptor"/> objects.</returns>
		public static ServiceDescriptor[] GetServiceDescriptors(this IServiceCollection services, Type serviceType, bool? isKeyedService = null)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}

			return services.Where(serviceDescriptor =>
				(isKeyedService == null || serviceDescriptor.IsKeyedService == isKeyedService)
				&& (serviceType.IsAssignableFrom(serviceDescriptor.ServiceType) || serviceDescriptor.ServiceType.IsAssignableFrom(serviceType)))
				.ToArray();
		}

		/// <summary>
		/// Checks if the last registration of a service of the specified <paramref name="serviceType"/> is transient.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <returns><c>true</c> if the last registration of the service is transient; otherwise, <c>false</c>.</returns>
		public static bool IsTransientServiceRegistered(this IServiceCollection services, Type serviceType)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}

			var descriptors = services.GetServiceDescriptors(serviceType, isKeyedService: false);
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
		public static bool IsScopedServiceRegistered(this IServiceCollection services, Type serviceType)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}

			var descriptors = services.GetServiceDescriptors(serviceType, isKeyedService: false);
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
		public static bool IsSingletonServiceRegistered(this IServiceCollection services, Type serviceType)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}

			var descriptors = services.GetServiceDescriptors(serviceType, isKeyedService: false);
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

		/// <summary>
		/// Checks if the last registration of a keyed service of the specified <paramref name="serviceType"/> and <paramref name="serviceKey"/> is singleton.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the last registration of the service is singleton; otherwise, <c>false</c>.</returns>
		public static bool IsKeyedSingletonServiceRegistered(this IServiceCollection services, Type serviceType, object serviceKey)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}

			if (serviceKey is null)
			{
				throw new ArgumentNullException(nameof(serviceKey));
			}

			var descriptors = services.GetServiceDescriptors(serviceType, isKeyedService: true)
				.Where(d => d.ServiceKey == serviceKey)
				.ToArray();
			return descriptors.Last().Lifetime == ServiceLifetime.Singleton;
		}

		/// <summary>
		/// Checks if the last registration of a keyed service of type <typeparamref name="TServiceType"/> and <paramref name="serviceKey"/> is singleton.
		/// </summary>
		/// <typeparam name="TServiceType">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the last registration of the service is singleton; otherwise, <c>false</c>.</returns>
		public static bool IsKeyedSingletonServiceRegistered<TServiceType>(this IServiceCollection services, object serviceKey)
		{
			return services.IsKeyedSingletonServiceRegistered(typeof(TServiceType), serviceKey);
		}

		/// <summary>
		/// Checks if the last registration of a keyed service of the specified <paramref name="serviceType"/> and <paramref name="serviceKey"/> is scoped.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the last registration of the service is scoped; otherwise, <c>false</c>.</returns>
		public static bool IsKeyedScopedServiceRegistered(this IServiceCollection services, Type serviceType, object serviceKey)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}
			if (serviceKey is null)
			{
				throw new ArgumentNullException(nameof(serviceKey));
			}

			var descriptors = services.GetServiceDescriptors(serviceType, isKeyedService: true)
				.Where(d => d.ServiceKey == serviceKey)
				.ToArray();
			return descriptors.Last().Lifetime == ServiceLifetime.Scoped;
		}

		/// <summary>
		/// Checks if the last registration of a keyed service of type <typeparamref name="TServiceType"/> and <paramref name="serviceKey"/> is scoped.
		/// </summary>
		/// <typeparam name="TServiceType">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the last registration of the service is scoped; otherwise, <c>false</c>.</returns>
		public static bool IsKeyedScopedServiceRegistered<TServiceType>(this IServiceCollection services, object serviceKey)
		{
			return services.IsKeyedScopedServiceRegistered(typeof(TServiceType), serviceKey);
		}

		/// <summary>
		/// Checks if the last registration of a keyed service of the specified <paramref name="serviceType"/> and <paramref name="serviceKey"/> is transient.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the last registration of the service is transient; otherwise, <c>false</c>.</returns>
		public static bool IsKeyedTransientServiceRegistered(this IServiceCollection services, Type serviceType, object serviceKey)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}
			if (serviceKey is null)
			{
				throw new ArgumentNullException(nameof(serviceKey));
			}

			var descriptors = services.GetServiceDescriptors(serviceType, isKeyedService: true)
				.Where(d => d.ServiceKey == serviceKey)
				.ToArray();
			return descriptors.Last().Lifetime == ServiceLifetime.Transient;
		}

		/// <summary>
		/// Checks if the last registration of a keyed service of type <typeparamref name="TServiceType"/> and <paramref name="serviceKey"/> is transient.
		/// </summary>
		/// <typeparam name="TServiceType">The type of the service.</typeparam>
		/// <param name="services">The service collection.</param>
		/// <param name="serviceKey">The service key.</param>
		/// <returns><c>true</c> if the last registration of the service is transient; otherwise, <c>false</c>.</returns>
		public static bool IsKeyedTransientServiceRegistered<TServiceType>(this IServiceCollection services, object serviceKey)
		{
			return services.IsKeyedTransientServiceRegistered(typeof(TServiceType), serviceKey);
		}
	}
}
