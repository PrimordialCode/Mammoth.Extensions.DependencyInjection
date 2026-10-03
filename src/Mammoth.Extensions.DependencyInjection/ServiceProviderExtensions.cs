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
	/// Resolves all the services of the specified ServiceTye (both keyed and non-keyed) from the service provider.
	/// Order of services is not guaranteed to be the same as the order of registration:
	/// - first will be resolved non-keyed services (in the order of registration)
	/// - then all keyed services (keys will be sorted in ascending order, each key will be resolved, services inside the key will
	///   be resolved in the order of registration)
	/// </summary>
	/// <remarks>
	/// <para>Closed service keys and keys from its generic definition are merged once per key.
	/// Each key uses the native container's enumeration and closed-registration precedence.</para>
	/// <para>Unkeyed results use native IEnumerable resolution, including explicit enumerable registrations.</para>
	/// <para>The AnyKey sentinel remains wildcard metadata and is not enumerated as a concrete key.</para>
	/// <para>WARNING: To use these extensions, you need to build the ServiceProvider using <see cref="ServiceProviderFactory"/>.</para>
	/// </remarks>
	public static IEnumerable<object?> GetAllServices(this IServiceProvider serviceProvider, Type serviceType)
	{
		var snapshot = serviceProvider.GetService<ServiceProviderRegistrationSnapshot>();
		var serviceList = new List<object?>();
		if (snapshot == null || !ServiceCollectionExtensions.IsDecorationSlot(serviceType))
			serviceList.AddRange(serviceProvider.GetServices(serviceType));
		// Factory providers use authoritative metadata. Preserve the legacy behavior for
		// providers without the factory, including explicitly supplied key metadata.
		var keys = snapshot?.GetKeys(serviceType) ??
			(serviceProvider.GetService(typeof(ServiceKeys<>).MakeGenericType(serviceType)) as IEnumerable<object> ?? []);
		foreach (var serviceKey in keys)
			if (!ReferenceEquals(serviceKey, KeyedService.AnyKey))
				serviceList.AddRange(serviceProvider.GetKeyedServices(serviceType, serviceKey));
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
	/// <para>Closed service keys and keys from its generic definition are merged once per key.
	/// Each key uses the native container's enumeration and closed-registration precedence.</para>
	/// <para>Unkeyed results use native IEnumerable resolution, including explicit enumerable registrations.</para>
	/// <para>The AnyKey sentinel remains wildcard metadata and is not enumerated as a concrete key.</para>
	/// <para>WARNING: To use these extensions, you need to build the ServiceProvider using <see cref="ServiceProviderFactory"/>.</para>
	/// </remarks>
	public static IEnumerable<TServiceType> GetAllServices<TServiceType>(this IServiceProvider serviceProvider)
	{
		return serviceProvider.GetAllServices(typeof(TServiceType)).Cast<TServiceType>();
	}

	private static InvalidOperationException BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory(Exception? ex = null)
	{
		// throw an exception to inform the user he need to build the service provider the proper
		// way to use these extensions
		return new InvalidOperationException($"To use these extensions, you need to build the ServiceProvider using: {typeof(ServiceProviderFactory).FullName}.", ex);
	}
}
