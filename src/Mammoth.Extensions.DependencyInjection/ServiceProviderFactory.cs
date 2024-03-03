using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Custom factory provider that will register some structures that will hold
	/// all the information needed to know what services are registered (like service type, service key, etc).
	/// These structures will be used to gather insights about the services registered in the <see cref="IServiceProvider"/>.
	/// </summary>
	public class ServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
	{
		/// <inheritdoc/>
		public IServiceCollection CreateBuilder(IServiceCollection services) => services;

		/// <inheritdoc/>
		IServiceProvider IServiceProviderFactory<IServiceCollection>.CreateServiceProvider(IServiceCollection containerBuilder)
		{
			return CreateServiceProvider(containerBuilder);
		}

		/// <summary>
		/// Enrich the <paramref name="containerBuilder"/> with the necessary services to be able to resolve the keys
		/// and other useful services; then build the <see cref="IServiceProvider"/>.
		/// </summary>
		public static IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
		{
			var dict = new Dictionary<Type, HashSet<object>>();
			var keys = new Keys();
			var serviceTypes = new ServiceTypes();

			foreach (var service in containerBuilder)
			{
				serviceTypes.Add(service.ServiceType);

				if (service.ServiceKey != null)
				{
					keys.Add(service.ServiceKey);

					if (!dict.TryGetValue(service.ServiceType, out var list))
					{
						list = new HashSet<object>();
						dict[service.ServiceType] = list;
					}
					list.Add(service.ServiceKey);
				}
			}

			// Insert Keys<ServiceType> as a service
			foreach (var kvp in dict)
			{
				var type = typeof(Keys<>).MakeGenericType(kvp.Key);
				var svc = Activator.CreateInstance(type, kvp.Value);
				containerBuilder.AddSingleton(type, svc!);
			}

			// Insert ServiceTypes as a service
			containerBuilder.AddSingleton(serviceTypes);
			// Insert Keys as a service
			containerBuilder.AddSingleton(keys);
			// Insert Keys<> so it's always resolvable
			containerBuilder.AddSingleton(typeof(Keys<>));

			return containerBuilder.BuildServiceProvider();
		}
	}

	/// <summary>
	/// The list of keys for a given service type.
	/// </summary>
	public class Keys<T> : HashSet<object>
	{
		/// <summary>
		/// Constructor
		/// </summary>
		public Keys(IEnumerable<object> collection) : base(collection)
		{ }
	}

	/// <summary>
	/// The list of all keys.
	/// </summary>
	public class Keys : SortedSet<object>
	{ }

	/// <summary>
	/// The list of all registered ServiceType.
	/// </summary>
	public class ServiceTypes : HashSet<Type>
	{ }
}
