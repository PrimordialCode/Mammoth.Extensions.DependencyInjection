using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mammoth.Extensions.DependencyInjection
{
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
	public static class ServiceProviderExtensions
	{
		/// <summary>
		/// Determines whether the specified service type is registered in the service provider (keyed or non-keyed).
		/// </summary>
		/// <remarks>
		/// WARNING: it uses reflection to access internal properties of the service provider, so it may break in future versions of the framework.
		/// </remarks>
		public static bool IsServiceRegistered(this IServiceProvider serviceProvider, Type serviceType)
		{
			if (serviceType is null)
			{
				throw new ArgumentNullException(nameof(serviceType));
			}
			var serviceTypes = serviceProvider.GetService<ServiceTypes>();
			if (serviceTypes == null)
			{
				throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();
			}
			return serviceTypes.Any(type => type == serviceType);
		}

		/// <summary>
		/// Determines whether the specified service type is registered in the service provider (keyed or non-keyed).
		/// </summary>
		/// <remarks>
		/// WARNING: it uses reflection to access internal properties of the service provider, so it may break in future versions of the framework.
		/// </remarks>
		public static bool IsServiceRegistered<TService>(this IServiceProvider serviceProvider)
		{
			return IsServiceRegistered(serviceProvider, typeof(TService));
		}

		/// <summary>
		/// Determines whether the specified service type is registered in the service provider (keyed or non-keyed).
		/// </summary>
		/// <remarks>
		/// WARNING: it uses reflection to access internal properties of the service provider, so it may break in future versions of the framework.
		/// </remarks>
		public static bool IsServiceRegistered(this IServiceProvider serviceProvider, object serviceKey)
		{
			if (serviceKey is null)
			{
				throw new ArgumentNullException(nameof(serviceKey));
			}

			var serviceKeys = serviceProvider.GetService<Keys>();
			if (serviceKeys == null)
			{
				throw BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory();
			}
			return serviceKeys.Any(key => key.Equals(serviceKey));
		}

		/// <summary>
		/// Resolves all the services of the specified ServiceTye (both keyed and non-keyed) from the service provider.
		/// Order of services is not guaranteed to be the same as the order of registration:
		/// - first will be resolved non-keyed services (in the order of registration)
		/// - then all keyed services (keys will be sorted in ascending order, each key will be reoslved, services inside the key will
		///   be resolved in the order of registration)
		/// </summary>
		/// <remarks>
		/// <para>WARNING: To use these extensions, you need to build the ServiceProvider using <see cref="ServiceProviderFactory"/>.</para>
		/// </remarks>
		public static IEnumerable<object?> GetAllServices(this IServiceProvider serviceProvider, Type serviceType)
		{
			var KeysType = typeof(Keys<>).MakeGenericType(serviceType);
			var keys = serviceProvider.GetService(KeysType) as IEnumerable<object>;
			var servicelist = new List<object?>();
			// add null key to get all non-keyed services
			servicelist.AddRange(serviceProvider.GetServices(serviceType));
			if (keys != null)
			{
				foreach (var serviceKey in keys!)
				{
					var services = serviceProvider.GetKeyedServices(serviceType, serviceKey);
					if (services?.Any() == true)
					{
						servicelist.AddRange(services!);
					}
				}
			}
			return servicelist;
		}

		/// <summary>
		/// Resolves all the services of the specified ServiceTye (both keyed and non-keyed) from the service provider.
		/// Order of services is not guaranteed to be the same as the order of registration:
		/// - first will be resolved non-keyed services (in the order of registration)
		/// - then all keyed services (keys will be sorted in ascending order, each key will be reoslved, services inside the key will
		///   be resolved in the order of registration)
		/// </summary>
		/// <remarks>
		/// <para>WARNING: To use these extensions, you need to build the ServiceProvider using <see cref="ServiceProviderFactory"/>.</para>
		/// </remarks>
		public static IEnumerable<TService> GetAllServices<TService>(this IServiceProvider serviceProvider)
		{
			var keys = serviceProvider.GetService<Keys<TService>>();
			var servicelist = new List<TService>();
			// add null key to get all non-keyed services
			servicelist.AddRange(serviceProvider.GetServices<TService>());
			if (keys != null)
			{
				foreach (var serviceKey in keys)
				{
					var services = serviceProvider.GetKeyedServices<TService>(serviceKey);
					if (services?.Any() == true)
					{
						servicelist.AddRange(services!);
					}
				}
			}
			return servicelist;
		}

		private static InvalidOperationException BuildExceptionBecauseProviderWasNotBuiltUsingTheFactory(Exception? ex = null)
		{
			// throw an exception to inform the user he need to build the service provider the proper
			// way to use these extensions
			return new InvalidOperationException($"To use these extensions, you need to build the ServiceProvider using: {typeof(ServiceProviderFactory).FullName}.", ex);
		}
	}
}
