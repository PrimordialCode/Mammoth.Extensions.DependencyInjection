using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// <para>
	/// The basic idea is use Keyed Services to register multiple implementations of the same interface by name.
	/// Provide a map (constructor param, service name) that will be used to select the implementation to use.
	/// </para>
	/// <para>idea took from: https://github.com/dotnet/runtime/issues/91638</para>
	/// </summary>
	/// <remarks>
	/// DependsOn selects constructors when the service is resolved. A single constructor marked
	/// with ActivatorUtilitiesConstructorAttribute takes precedence; otherwise the unique longest
	/// constructor whose parameters can be supplied is used. Parameter-name overrides take priority
	/// over keyed attributes and ordinary services. Optional defaults apply only when no service is
	/// registered. Equal-length ambiguity or an unsatisfied preferred constructor throws.
	/// Unused map entries retain their existing behavior and are ignored.
	/// Selection requires IServiceProviderIsService and IServiceProviderIsKeyedService probes and
	/// does not instantiate dependencies belonging to rejected constructors.
	/// </remarks>
	public static partial class ServiceCollectionExtensions
	{
		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				return services.AddSingleton(serviceType);
			}

			var constructorTarget = serviceType;

			return services.AddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddSingleton(this IServiceCollection services, Type serviceType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddSingleton(serviceType);
				return;
			}

			var constructorTarget = serviceType;

			services.TryAddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, Type implementationType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				return services.AddSingleton(serviceType, implementationType);
			}

			var constructorTarget = implementationType;

			return services.AddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddSingleton(this IServiceCollection services, Type serviceType, Type implementationType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddSingleton(serviceType, implementationType);
				return;
			}

			var constructorTarget = implementationType;

			services.TryAddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				return services.AddSingleton<TService>();
			}

			var constructorTarget = typeof(TService);

			return services.AddSingleton(DependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddSingleton<TService>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddSingleton<TService>();
				return;
			}

			var constructorTarget = typeof(TService);

			services.TryAddSingleton(DependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				return services.AddSingleton<TService, TImplementation>();
			}

			var constructorTarget = typeof(TImplementation);

			return services.AddSingleton<TService, TImplementation>(DependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddScoped(this IServiceCollection services, Type serviceType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				return services.AddScoped(serviceType);
			}

			var constructorTarget = serviceType;

			return services.AddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddScoped(this IServiceCollection services, Type serviceType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddScoped(serviceType);
				return;
			}

			var constructorTarget = serviceType;

			services.TryAddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddScoped(this IServiceCollection services, Type serviceType, Type implementationType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				return services.AddScoped(serviceType, implementationType);
			}

			var constructorTarget = implementationType;

			return services.AddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddScoped(this IServiceCollection services, Type serviceType, Type implementationType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddScoped(serviceType, implementationType);
				return;
			}

			var constructorTarget = implementationType;

			services.TryAddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddScoped<TService>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				return services.AddScoped<TService>();
			}

			var constructorTarget = typeof(TService);

			return services.AddScoped(DependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddScoped<TService>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddScoped<TService>();
				return;
			}

			var constructorTarget = typeof(TService);

			services.TryAddScoped(DependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				return services.AddScoped<TService, TImplementation>();
			}

			var constructorTarget = typeof(TImplementation);

			return services.AddScoped<TService, TImplementation>(DependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddTransient(this IServiceCollection services, Type serviceType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				return services.AddTransient(serviceType);
			}

			var constructorTarget = serviceType;

			return services.AddTransient(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddTransient(this IServiceCollection services, Type serviceType, Type implementationType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				return services.AddTransient(serviceType, implementationType);
			}

			var constructorTarget = implementationType;

			return services.AddTransient(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddTransient(this IServiceCollection services, Type serviceType, Type implementationType, Dependency[] dependsOn)
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddTransient(serviceType, implementationType);
				return;
			}

			var constructorTarget = implementationType;

			services.TryAddTransient(serviceType, DependsOnResolutionFunc<object>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddTransient<TService>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				return services.AddTransient<TService>();
			}

			var constructorTarget = typeof(TService);

			return services.AddTransient(DependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddTransient<TService>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddTransient<TService>();
				return;
			}

			var constructorTarget = typeof(TService);

			services.TryAddTransient(DependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				return services.AddTransient<TService, TImplementation>();
			}

			var constructorTarget = typeof(TImplementation);

			return services.AddTransient<TService, TImplementation>(DependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddKeyedSingleton<TService>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				return services.AddKeyedSingleton<TService>(serviceKey);
			}

			var constructorTarget = typeof(TService);

			return services.AddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddKeyedSingleton<TService>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddKeyedSingleton<TService>(serviceKey);
				return;
			}

			var constructorTarget = typeof(TService);

			services.TryAddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddKeyedSingleton<TService, TImplementation>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				return services.AddKeyedSingleton<TService, TImplementation>(serviceKey);
			}

			var constructorTarget = typeof(TImplementation);

			return services.AddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a singleton service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddKeyedSingleton<TService, TImplementation>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddKeyedSingleton<TService, TImplementation>(serviceKey);
				return;
			}

			var constructorTarget = typeof(TImplementation);

			services.TryAddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddKeyedScoped<TService>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				return services.AddKeyedScoped<TService>(serviceKey);
			}

			var constructorTarget = typeof(TService);

			return services.AddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddKeyedScoped<TService>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddKeyedScoped<TService>(serviceKey);
				return;
			}

			var constructorTarget = typeof(TService);

			services.TryAddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddKeyedScoped<TService, TImplementation>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				return services.AddKeyedScoped<TService, TImplementation>(serviceKey);
			}

			var constructorTarget = typeof(TImplementation);

			return services.AddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a scoped service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddKeyedScoped<TService, TImplementation>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddKeyedScoped<TService, TImplementation>(serviceKey);
				return;
			}

			var constructorTarget = typeof(TImplementation);

			services.TryAddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddKeyedTransient<TService>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				return services.AddKeyedTransient<TService>(serviceKey);
			}

			var constructorTarget = typeof(TService);

			return services.AddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddKeyedTransient<TService>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddKeyedTransient<TService>(serviceKey);
				return;
			}

			var constructorTarget = typeof(TService);

			services.TryAddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use.
		/// </summary>
		public static IServiceCollection AddKeyedTransient<TService, TImplementation>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				return services.AddKeyedTransient<TService, TImplementation>(serviceKey);
			}

			var constructorTarget = typeof(TImplementation);

			return services.AddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

		/// <summary>
		/// Add a transient service with a map of dependencies to select the implementation to use
		/// if the service type hasn't already been registered.
		/// </summary>
		public static void TryAddKeyedTransient<TService, TImplementation>(this IServiceCollection services, object? serviceKey, Dependency[] dependsOn)
			where TService : class
			where TImplementation : class, TService
		{
			if (dependsOn.Length == 0)
			{
				services.TryAddKeyedTransient<TService, TImplementation>(serviceKey);
				return;
			}

			var constructorTarget = typeof(TImplementation);

			services.TryAddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, constructorTarget));
		}

        internal static Func<IServiceProvider, TTarget> DependsOnResolutionFunc<TTarget>(Dependency[] dependsOn, Type target) where TTarget : class
            => provider => (TTarget)CreateDependsOnInstance(provider, target, dependsOn);

        internal static Func<IServiceProvider, object?, TTarget> KeyedDependsOnResolutionFunc<TTarget>(Dependency[] dependsOn, Type target) where TTarget : class
            => (provider, key) => (TTarget)CreateDependsOnInstance(provider, target, dependsOn, key);
    }
}
