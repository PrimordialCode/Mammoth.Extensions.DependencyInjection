using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

public static partial class ServiceProviderExtensions
{
	/// <summary>
	/// Determines whether the specified service type is registered in the service provider (keyed or non-keyed).
	/// </summary>
	public static bool IsServiceRegistered(this IServiceProvider serviceProvider, Type serviceType)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}
		var serviceTypes = serviceProvider.GetService<ServiceTypes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return serviceTypes.Any(type => type == serviceType);
	}

	/// <summary>
	/// Determines whether the specified service type is registered in the service provider (keyed or non-keyed).
	/// </summary>
	public static bool IsServiceRegistered<TServiceType>(this IServiceProvider serviceProvider)
	{
		return IsServiceRegistered(serviceProvider, typeof(TServiceType));
	}

	/// <summary>
	/// Determines whether the specified service key is registered in the service provider.
	/// </summary>
	public static bool IsServiceRegistered(this IServiceProvider serviceProvider, object serviceKey)
	{
		if (serviceKey is null)
		{
			throw new ArgumentNullException(nameof(serviceKey));
		}

		var serviceKeys = serviceProvider.GetService<ServiceKeys>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return serviceKeys.Any(key => key.Equals(serviceKey));
	}

	/// <summary>
	/// Determines whether the specified service type is registered as a transient service in the service provider (non keyed).
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <returns><c>true</c> if the service type is registered as transient; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsTransientServiceRegistered(this IServiceProvider serviceProvider, Type serviceType)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}

		var lifetimes = serviceProvider.GetService<ServiceLifetimes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return lifetimes.GetLifetime(serviceType) == ServiceLifetime.Transient;
	}

	/// <summary>
	/// Determines whether the specified service type is registered as a transient service in the service provider (non keyed).
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service.</typeparam>
	/// <param name="serviceProvider">The service provider.</param>
	/// <returns><c>true</c> if the service type is registered as transient; otherwise, <c>false</c>.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsTransientServiceRegistered<TServiceType>(this IServiceProvider serviceProvider)
	{
		return IsTransientServiceRegistered(serviceProvider, typeof(TServiceType));
	}

	/// <summary>
	/// Determines whether the specified service type with the given key is registered as a transient service in the service provider (keyed).
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as transient; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType or serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsKeyedTransientServiceRegistered(this IServiceProvider serviceProvider, Type serviceType, object serviceKey)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}

		if (serviceKey is null)
		{
			throw new ArgumentNullException(nameof(serviceKey));
		}

		var lifetimes = serviceProvider.GetService<ServiceLifetimes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return lifetimes.GetLifetime(serviceType, serviceKey) == ServiceLifetime.Transient;
	}

	/// <summary>
	/// Determines whether the specified service type with the given key is registered as a transient service in the service provider (keyed).
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service.</typeparam>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as transient; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsKeyedTransientServiceRegistered<TServiceType>(this IServiceProvider serviceProvider, object serviceKey)
	{
		return IsKeyedTransientServiceRegistered(serviceProvider, typeof(TServiceType), serviceKey);
	}

	/// <summary>
	/// Determines whether the specified service type is registered as a singleton service in the service provider (non keyed).
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <returns><c>true</c> if the service type is registered as singleton; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsSingletonServiceRegistered(this IServiceProvider serviceProvider, Type serviceType)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}

		var lifetimes = serviceProvider.GetService<ServiceLifetimes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return lifetimes.GetLifetime(serviceType) == ServiceLifetime.Singleton;
	}

	/// <summary>
	/// Determines whether the specified service type is registered as a singleton service in the service provider (non keyed).
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service.</typeparam>
	/// <param name="serviceProvider">The service provider.</param>
	/// <returns><c>true</c> if the service type is registered as singleton; otherwise, <c>false</c>.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsSingletonServiceRegistered<TServiceType>(this IServiceProvider serviceProvider)
	{
		return IsSingletonServiceRegistered(serviceProvider, typeof(TServiceType));
	}

	/// <summary>
	/// Determines whether the specified service type with the given key is registered as a singleton service in the service provider (keyed).
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as singleton; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType or serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsKeyedSingletonServiceRegistered(this IServiceProvider serviceProvider, Type serviceType, object serviceKey)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}

		if (serviceKey is null)
		{
			throw new ArgumentNullException(nameof(serviceKey));
		}

		var lifetimes = serviceProvider.GetService<ServiceLifetimes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return lifetimes.GetLifetime(serviceType, serviceKey) == ServiceLifetime.Singleton;
	}

	/// <summary>
	/// Determines whether the specified service type with the given key is registered as a singleton service in the service provider (keyed).
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service.</typeparam>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as singleton; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsKeyedSingletonServiceRegistered<TServiceType>(this IServiceProvider serviceProvider, object serviceKey)
	{
		return IsKeyedSingletonServiceRegistered(serviceProvider, typeof(TServiceType), serviceKey);
	}

	/*
	 * TODO
	 * 
	/// <summary>
	/// Determines whether the specified service type with the given key is registered as a singleton service in the service provider (keyed).
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as singleton; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType or serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsKeyedSingletonServiceRegistered(this IServiceProvider serviceProvider, Type serviceType)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}

		var lifetimes = serviceProvider.GetService<ServiceLifetimes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return lifetimes.GetLifetime(serviceType, serviceKey) == ServiceLifetime.Singleton;
	}
	*/

	/// <summary>
	/// Determines whether the specified service type is registered as a scoped service in the service provider (non keyed).
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <returns><c>true</c> if the service type is registered as scoped; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsScopedServiceRegistered(this IServiceProvider serviceProvider, Type serviceType)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}

		var lifetimes = serviceProvider.GetService<ServiceLifetimes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return lifetimes.GetLifetime(serviceType) == ServiceLifetime.Scoped;
	}

	/// <summary>
	/// Determines whether the specified service type is registered as a scoped service in the service provider (non keyed).
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service.</typeparam>
	/// <param name="serviceProvider">The service provider.</param>
	/// <returns><c>true</c> if the service type is registered as scoped; otherwise, <c>false</c>.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsScopedServiceRegistered<TServiceType>(this IServiceProvider serviceProvider)
	{
		return IsScopedServiceRegistered(serviceProvider, typeof(TServiceType));
	}

	/// <summary>
	/// Determines whether the specified service type with the given key is registered as a scoped service in the service provider (keyed).
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as scoped; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType or serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsKeyedScopedServiceRegistered(this IServiceProvider serviceProvider, Type serviceType, object serviceKey)
	{
		if (serviceType is null)
		{
			throw new ArgumentNullException(nameof(serviceType));
		}

		if (serviceKey is null)
		{
			throw new ArgumentNullException(nameof(serviceKey));
		}

		var lifetimes = serviceProvider.GetService<ServiceLifetimes>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return lifetimes.GetLifetime(serviceType, serviceKey) == ServiceLifetime.Scoped;
	}

	/// <summary>
	/// Determines whether the specified service type with the given key is registered as a scoped service in the service provider (keyed).
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service.</typeparam>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as scoped; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsKeyedScopedServiceRegistered<TServiceType>(this IServiceProvider serviceProvider, object serviceKey)
	{
		return IsKeyedScopedServiceRegistered(serviceProvider, typeof(TServiceType), serviceKey);
	}

}