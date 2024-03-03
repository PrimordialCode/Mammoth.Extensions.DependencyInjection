using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Mammoth.Extensions.DependencyInjection
{
	// Instead of using reflection to access internal properties of the service provider, consider using the following approach:
	// Resolve all services: another approach to consider is "enumerate all keys": https://github.com/dotnet/runtime/issues/91466#issuecomment-1723532096
	// We can create a custom factory provider that will register some structure with all the
	// information needed to know what services are registered (like service type, service key, etc).
	// we can then use these structures to resolve all services.
	// This approach is more reliable and less likely to break in future versions of the framework.

	// These extension methods were disabled in favour of those that use the ServiceProviderFactory approach.

	/// <summary>
	/// Provides extension methods for <see cref="IServiceProvider"/>.
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

			var descriptors = GetServiceDescriptors(serviceProvider);
			return descriptors?.Any(sd => sd.ServiceType == serviceType) ?? false;
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

			var descriptors = GetServiceDescriptors(serviceProvider);
			return descriptors?.Any(sd => sd.ServiceKey?.Equals(serviceKey) == true) ?? false;
		}

		/// <summary>
		/// Resolves all the services of the specified ServiceTye (both keyed and non-keyed) from the service provider.
		/// </summary>
		/// <remarks>
		/// <para>WARNING: it uses reflection to access internal properties of the service provider, so it may break in future versions of the framework.</para>
		/// <para>WARNING: It might be removed once enumerating all services with KeyedService.AnyKey work as expected.</para>
		/// </remarks>
		public static IEnumerable<object> GetAllServices(this IServiceProvider serviceProvider, Type serviceType)
		{
			var descriptors = GetServiceDescriptors(serviceProvider);
			if (descriptors == null)
			{
				return Enumerable.Empty<object>();
			}
			// find out all the unique services keyed names
			var serviceKeys = descriptors
				.Where(sd => sd.ServiceType == serviceType)
				.Select(sd => sd.ServiceKey)
				.Distinct()
				.ToList();
			var servicelist = new List<object>();
			// null key to get all non-keyed services
			foreach (var serviceKey in serviceKeys)
			{
				var services = serviceProvider.GetKeyedServices(serviceType, serviceKey);
				if (services?.Any() == true)
				{
					servicelist.AddRange(services!);
				}
			}
			return servicelist;
		}

		/// <summary>
		/// Resolves all the services of the specified ServiceTye (both keyed and non-keyed) from the service provider.
		/// </summary>
		/// <remarks>
		/// <para>WARNING: it uses reflection to access internal properties of the service provider, so it may break in future versions of the framework.</para>
		/// <para>WARNING: It might be removed once enumerating all services with KeyedService.AnyKey work as expected.</para>
		/// </remarks>
		public static IEnumerable<TService> GetAllServices<TService>(this IServiceProvider serviceProvider)
		{
			var descriptors = GetServiceDescriptors(serviceProvider);
			if (descriptors == null)
			{
				return Enumerable.Empty<TService>();
			}
			// find out all the unique services keyed names
			var serviceKeys = descriptors
				.Where(sd => sd.ServiceType == typeof(TService))
				.Select(sd => sd.ServiceKey)
				.Distinct()
				.ToList();
			var servicelist = new List<TService>();
			// null key to get all non-keyed services
			foreach (var serviceKey in serviceKeys)
			{
				var services = serviceProvider.GetKeyedServices<TService>(serviceKey);
				if (services?.Any() == true)
				{
					servicelist.AddRange(services!);
				}
			}
			return servicelist;
		}

		private static IEnumerable<ServiceDescriptor> GetServiceDescriptors(IServiceProvider serviceProvider)
		{
			var callSiteFactoryProperty = serviceProvider.GetType().GetProperty("CallSiteFactory", BindingFlags.NonPublic | BindingFlags.Instance);
			if (callSiteFactoryProperty != null)
			{
				var callSiteFactory = callSiteFactoryProperty.GetValue(serviceProvider);
				var serviceDescriptorsProperty = callSiteFactory?.GetType().GetProperty("Descriptors", BindingFlags.NonPublic | BindingFlags.Instance);
				if (serviceDescriptorsProperty != null)
				{
					return (IEnumerable<ServiceDescriptor>)serviceDescriptorsProperty.GetValue(callSiteFactory);
				}
			}
			throw new InvalidOperationException("Internal implementation of ServiceProvider changed, cannot enumerate service descriptors.");
		}
	}
}
