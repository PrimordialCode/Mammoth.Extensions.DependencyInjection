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
		public static IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
		{
			var dict = new Dictionary<Type, HashSet<object>>();
			var keys = new Keys();
			var serviceTypes = new ServiceTypes();
			var serviceLifetimes = new ServiceLifetimes();
			var serviceLifetimesByType = new Dictionary<Type, Dictionary<object?, ServiceLifetime>>();

			foreach (var service in containerBuilder)
			{
				serviceTypes.Add(service.ServiceType);

				// Track the lifetime for this service
				serviceLifetimes.Add(service.ServiceType, service.Lifetime, service.ServiceKey);

				// Store lifetime by type and key
				if (!serviceLifetimesByType.TryGetValue(service.ServiceType, out var lifetimesForType))
				{
					lifetimesForType = [];
					serviceLifetimesByType[service.ServiceType] = lifetimesForType;
				}
				lifetimesForType[service.ServiceKey] = service.Lifetime;

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
			}

			// Insert Keys<ServiceType> as a service
			foreach (var kvp in dict)
			{
				var type = typeof(Keys<>).MakeGenericType(kvp.Key);
				var svc = Activator.CreateInstance(type, kvp.Value);
				containerBuilder.AddSingleton(type, svc!);
			}

			// Insert ServiceLifetimes<ServiceType> as a service
			foreach (var kvp in serviceLifetimesByType)
			{
				var type = typeof(ServiceLifetimes<>).MakeGenericType(kvp.Key);
				var svc = Activator.CreateInstance(type, kvp.Value);
				containerBuilder.AddSingleton(type, svc!);
			}

			// Insert ServiceTypes as a service
			containerBuilder.AddSingleton(serviceTypes);
			// Insert Keys as a service
			containerBuilder.AddSingleton(keys);
			// Insert Keys<> so it's always resolvable
			containerBuilder.AddSingleton(typeof(Keys<>));
			// Insert ServiceLifetimes as a service
			containerBuilder.AddSingleton(serviceLifetimes);
			// Insert ServiceLifetimes<> so it's always resolvable
			containerBuilder.AddSingleton(typeof(ServiceLifetimes<>));

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
	public class Keys : SortedSet<object>;

	/// <summary>
	/// The list of all registered ServiceType.
	/// </summary>
	public class ServiceTypes : HashSet<Type>;

	/// <summary>
	/// Stores the lifetime information for a specific service type with its associated keys.
	/// </summary>
	/// <typeparam name="T">The service type.</typeparam>
	public class ServiceLifetimes<T> : Dictionary<object?, ServiceLifetime>
	{
		/// <summary>
		/// Constructor
		/// </summary>
		public ServiceLifetimes(Dictionary<object?, ServiceLifetime> lifetimes) : base(lifetimes)
		{ }
	}

	/// <summary>
	/// Tracks the lifetime of all registered services.
	/// </summary>
	public class ServiceLifetimes
	{
		private readonly Dictionary<Type, Dictionary<object?, ServiceLifetime>> _lifetimes = [];

		/// <summary>
		/// Adds a service type with its lifetime and optional key.
		/// </summary>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="lifetime">The lifetime of the service.</param>
		/// <param name="serviceKey">The optional key for the service.</param>
		public void Add(Type serviceType, ServiceLifetime lifetime, object? serviceKey = null)
		{
			if (!_lifetimes.TryGetValue(serviceType, out var lifetimesByKey))
			{
				lifetimesByKey = [];
				_lifetimes[serviceType] = lifetimesByKey;
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
			if (_lifetimes.TryGetValue(serviceType, out var lifetimesByKey) &&
				lifetimesByKey.TryGetValue(serviceKey, out var lifetime))
			{
				return lifetime;
			}

			return null;
		}

		/// <summary>
		/// Gets all service types with their associated lifetimes.
		/// </summary>
		public IReadOnlyDictionary<Type, Dictionary<object?, ServiceLifetime>> GetAllLifetimes() => _lifetimes;
	}
}
