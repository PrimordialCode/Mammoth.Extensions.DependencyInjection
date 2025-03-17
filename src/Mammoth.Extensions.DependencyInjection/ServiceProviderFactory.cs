using Microsoft.Extensions.DependencyInjection;

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
		public static ServiceProvider CreateServiceProvider(IServiceCollection containerBuilder, ExtendedServiceProviderOptions? options = null)
		{
			AddIsRegisteredSupportServices(containerBuilder);

			if (options == null)
			{
				return containerBuilder.BuildServiceProvider();
			}
			var sc = containerBuilder;
			if (options.DetectIncorrectUsageOfTransientDisposables)
			{
				sc = DetectIncorrectUsageOfTransientDisposables.PatchServiceCollection(containerBuilder);
			}
			return sc.BuildServiceProvider(options);
		}

		/// <summary>
		/// Registers support services related to service keys, types, and lifetimes in the provided service collection.
		/// </summary>
		private static void AddIsRegisteredSupportServices(IServiceCollection containerBuilder)
		{
			var dict = new Dictionary<Type, HashSet<object>>();
			var keys = new ServiceKeys();
			var serviceTypes = new ServiceTypes();
			var serviceLifetimes = new ServiceLifetimes();

			foreach (var service in containerBuilder)
			{
				// maybe copying the service descriptors was more effective!

				serviceTypes.Add(service.ServiceType);

				if (service.ServiceKey != null)
				{
					keys.Add(service.ServiceKey);

					if (!dict.TryGetValue(service.ServiceType, out var list))
					{
						list = [];
						dict[service.ServiceType] = list;
					}
					list.Add(service.ServiceKey);
				}

				// Track the lifetime for this service
				serviceLifetimes.Add(service.ServiceType, service.Lifetime, service.ServiceKey);
			}

			// Insert Keys<ServiceType> as a service
			foreach (var kvp in dict)
			{
				var type = typeof(ServiceKeys<>).MakeGenericType(kvp.Key);
				var svc = Activator.CreateInstance(type, kvp.Value);
				containerBuilder.AddSingleton(type, svc!);
			}

			// Insert ServiceTypes as a service
			containerBuilder.AddSingleton(serviceTypes);
			// Insert Keys as a service
			containerBuilder.AddSingleton(keys);
			// Insert Keys<> so it's always resolvable
			containerBuilder.AddSingleton(typeof(ServiceKeys<>));
			// Insert ServiceLifetimes as a service
			containerBuilder.AddSingleton(serviceLifetimes);
		}
	}

	/// <summary>
	/// The list of keys for a given service type.
	/// </summary>
	public class ServiceKeys<T> : HashSet<object>
	{
		/// <summary>
		/// Constructor
		/// </summary>
		public ServiceKeys(IEnumerable<object> collection) : base(collection)
		{ }
	}

	/// <summary>
	/// The list of all keys.
	/// </summary>
	public class ServiceKeys : SortedSet<object>;

	/// <summary>
	/// The list of all registered ServiceType.
	/// </summary>
	public class ServiceTypes : HashSet<Type>;

	/// <summary>
	/// Tracks the lifetime of all registered services.
	/// </summary>
	public class ServiceLifetimes
	{
		private readonly Dictionary<Type, ServiceLifetime> _lifetimes = [];
		private readonly Dictionary<Type, Dictionary<object?, ServiceLifetime>> _keyedLifetimes = [];

		/// <summary>
		/// Adds a service type with its lifetime and optional key.
		/// </summary>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="lifetime">The lifetime of the service.</param>
		/// <param name="serviceKey">The optional key for the service.</param>
		public void Add(Type serviceType, ServiceLifetime lifetime, object? serviceKey = null)
		{
			if (serviceKey == null)
			{
				_lifetimes[serviceType] = lifetime;
				return;
			}
			if (!_keyedLifetimes.TryGetValue(serviceType, out var lifetimesByKey))
			{
				lifetimesByKey = [];
				_keyedLifetimes[serviceType] = lifetimesByKey;
			}

			lifetimesByKey[serviceKey] = lifetime;
		}

		/// <summary>
		/// Gets the lifetime for a specific service type and optional key.
		/// </summary>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="serviceKey">The optional key for the service.</param>
		/// <returns>The lifetime of the service or null if not found.</returns>
		public ServiceLifetime? GetLifetime(Type serviceType, object? serviceKey = null)
		{
			if (_lifetimes.TryGetValue(serviceType, out var lifetime))
			{
				return lifetime;
			}
			if (serviceKey != null)
			{
				if (_keyedLifetimes.TryGetValue(serviceType, out var lifetimesByKey) &&
					lifetimesByKey.TryGetValue(serviceKey, out var lifetime2))
				{
					return lifetime2;
				}
			}

			return null;
		}
	}
}
