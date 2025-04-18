﻿using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// <para>
	/// The basic idea is use Keyed Services to register multiple implementations of the same interface by name.
	/// Provide a map (constructor param, service name) that will be used to select the implementation to use.
	/// </para>
	/// <para>idea took from: https://github.com/dotnet/runtime/issues/91638</para>
	/// </summary>
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

			GetConstructorAndParameters(serviceType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(serviceType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(implementationType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(implementationType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddSingleton(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddSingleton(DependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddSingleton(DependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddSingleton<TService, TImplementation>(DependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(serviceType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(serviceType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(implementationType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(implementationType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddScoped(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddScoped(DependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddScoped(DependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddScoped<TService, TImplementation>(DependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(serviceType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddTransient(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(implementationType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddTransient(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(implementationType, out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddTransient(serviceType, DependsOnResolutionFunc<object>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddTransient(DependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddTransient(DependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddTransient<TService, TImplementation>(DependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddKeyedSingleton<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddKeyedScoped<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TService), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TService>(dependsOn, ctor, parameter));
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

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			return services.AddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
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
			}

			GetConstructorAndParameters(typeof(TImplementation), out ConstructorInfo ctor, out ParameterInfo[] parameter);

			services.TryAddKeyedTransient<TService>(serviceKey, KeyedDependsOnResolutionFunc<TImplementation>(dependsOn, ctor, parameter));
		}

#pragma warning disable RCS1224 // Make method an extension method
		internal static Func<IServiceProvider, TTarget> DependsOnResolutionFunc<TTarget>(Dependency[] dependsOn, ConstructorInfo ctor, ParameterInfo[] parameter) where TTarget : class
		{
			return sp =>
			{
				if (sp is IKeyedServiceProvider keyed)
				{
					var args = new object[parameter.Length];

					for (int i = 0; i < args.Length; i++)
					{
						var p = parameter[i];
						// look for the parameter name in the dependsOn map
						var dep = Array.Find(dependsOn, d => d.ParameterName == p.Name);
						if (dep != null)
						{
							if (dep.T == Dependency.DependencyType.KeyedServices)
							{
								// Use the type as the key, or fallback to normal service resolution
								args[i] = keyed.GetRequiredKeyedService(p.ParameterType, dep.Value);
							}
							else
							{
								args[i] = dep.Value;
							}
						}
						else
						{
							args[i] = sp.GetRequiredService(p.ParameterType);
						}
					}

					return (TTarget)ctor.Invoke(args);
				}
				throw new NotSupportedException($"ServiceProvider must be an {nameof(IKeyedServiceProvider)}");
			};
		}

		internal static Func<IServiceProvider, object?, TTarget> KeyedDependsOnResolutionFunc<TTarget>(Dependency[] dependsOn, ConstructorInfo ctor, ParameterInfo[] parameter) where TTarget : class
		{
			return (sp, _) =>
			{
				if (sp is IKeyedServiceProvider keyed)
				{
					var args = new object[parameter.Length];

					for (int i = 0; i < args.Length; i++)
					{
						var p = parameter[i];
						// look for the parameter name in the dependsOn map
						var dep = Array.Find(dependsOn, d => d.ParameterName == p.Name);
						if (dep != null)
						{
							if (dep.T == Dependency.DependencyType.KeyedServices)
							{
								// Use the type as the key, or fallback to normal service resolution
								args[i] = keyed.GetRequiredKeyedService(p.ParameterType, dep.Value);
							}
							else
							{
								args[i] = dep.Value;
							}
						}
						else
						{
							args[i] = sp.GetRequiredService(p.ParameterType);
						}
					}

					return (TTarget)ctor.Invoke(args);
				}
				throw new NotSupportedException($"ServiceProvider must be an {nameof(IKeyedServiceProvider)}");
			};
		}

		internal static void GetConstructorAndParameters(Type target, out ConstructorInfo ctor, out ParameterInfo[] parameter)
		{
			// Select the constructor with the highest number of parameters
			// maybe we should use the same strategy of: ActivatorUtilities.CreateInstance
			ctor = target.GetConstructors()
				.OrderByDescending(c => c.GetParameters().Length)
				.First();
			parameter = ctor.GetParameters();
		}
#pragma warning restore RCS1224 // Make method an extension method
	}
}
