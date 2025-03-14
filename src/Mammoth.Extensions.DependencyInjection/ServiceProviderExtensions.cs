using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

/// <summary>
/// <para>
/// Provides extension methods for <see cref="IServiceProvider"/>.
/// To use the following extensions, you need to build the ServiceProvider calling:
/// </para>
/// <![CDATA[
/// new HostBuilder().UseServiceProviderFactory(new ServiceProviderFactory());
/// - or -
/// var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);
/// ]]>
/// <para>in order to use these extensions.</para>
/// </summary>
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
	/// Determines whether the specified service type is registered in the service provider (keyed or non-keyed).
	/// </summary>
	public static bool IsServiceRegistered(this IServiceProvider serviceProvider, object serviceKey)
	{
		if (serviceKey is null)
		{
			throw new ArgumentNullException(nameof(serviceKey));
		}

		var serviceKeys = serviceProvider.GetService<Keys>()
			?? throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();

		return serviceKeys.Any(key => key.Equals(serviceKey));
	}

	/// <summary>
	/// Resolves all the services of the specified ServiceTye (both keyed and non-keyed) from the service provider.
	/// Order of services is not guaranteed to be the same as the order of registration:
	/// - first will be resolved non-keyed services (in the order of registration)
	/// - then all keyed services (keys will be sorted in ascending order, each key will be resolved, services inside the key will
	///   be resolved in the order of registration)
	/// </summary>
	/// <remarks>
	/// <para>WARNING: To use these extensions, you need to build the ServiceProvider using <see cref="ServiceProviderFactory"/>.</para>
	/// </remarks>
	public static IEnumerable<object?> GetAllServices(this IServiceProvider serviceProvider, Type serviceType)
	{
		var KeysType = typeof(Keys<>).MakeGenericType(serviceType);
		var serviceList = new List<object?>();
		// add null key to get all non-keyed services
		serviceList.AddRange(serviceProvider.GetServices(serviceType));
		if (serviceProvider.GetService(KeysType) is IEnumerable<object> keys)
		{
			foreach (var serviceKey in keys!)
			{
				var services = serviceProvider.GetKeyedServices(serviceType, serviceKey);
				if (services?.Any() == true)
				{
					serviceList.AddRange(services!);
				}
			}
		}
		return serviceList;
	}

	/// <summary>
	/// Resolves all the services of the specified ServiceTye (both keyed and non-keyed) from the service provider.
	/// Order of services is not guaranteed to be the same as the order of registration:
	/// - first will be resolved non-keyed services (in the order of registration)
	/// - then all keyed services (keys will be sorted in ascending order, each key will be resolved, services inside the key will
	///   be resolved in the order of registration)
	/// </summary>
	/// <remarks>
	/// <para>WARNING: To use these extensions, you need to build the ServiceProvider using <see cref="ServiceProviderFactory"/>.</para>
	/// </remarks>
	public static IEnumerable<TServiceType> GetAllServices<TServiceType>(this IServiceProvider serviceProvider)
	{
		var keys = serviceProvider.GetService<Keys<TServiceType>>();
		var serviceList = new List<TServiceType>();
		// add null key to get all non-keyed services
		serviceList.AddRange(serviceProvider.GetServices<TServiceType>());
		if (keys != null)
		{
			foreach (var serviceKey in keys)
			{
				var services = serviceProvider.GetKeyedServices<TServiceType>(serviceKey);
				if (services?.Any() == true)
				{
					serviceList.AddRange(services!);
				}
			}
		}
		return serviceList;
	}

	private static InvalidOperationException BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory(Exception? ex = null)
	{
		// throw an exception to inform the user he need to build the service provider the proper
		// way to use these extensions
		return new InvalidOperationException($"To use these extensions, you need to build the ServiceProvider using: {typeof(ServiceProviderFactory).FullName}.", ex);
	}
}
