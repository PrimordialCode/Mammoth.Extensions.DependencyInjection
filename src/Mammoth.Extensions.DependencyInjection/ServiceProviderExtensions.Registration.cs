using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

public static partial class ServiceProviderExtensions
{
	/// <summary>
	/// Determines whether the specified service type is registered as a transient service in the service provider.
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
	/// Determines whether the specified service type is registered as a transient service in the service provider.
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
	/// Determines whether the specified service type with the given key is registered as a transient service in the service provider.
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceType">The type of the service.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as transient; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceType or serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsTransientServiceRegistered(this IServiceProvider serviceProvider, Type serviceType, object serviceKey)
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
	/// Determines whether the specified service type with the given key is registered as a transient service in the service provider.
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service.</typeparam>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="serviceKey">The key of the service.</param>
	/// <returns><c>true</c> if the service type with the specified key is registered as transient; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when serviceKey is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the service provider was not built using the ServiceProviderFactory.</exception>
	public static bool IsTransientServiceRegistered<TServiceType>(this IServiceProvider serviceProvider, object serviceKey)
	{
		return IsTransientServiceRegistered(serviceProvider, typeof(TServiceType), serviceKey);
	}
}