using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// original code took from blazor-samples:
	/// https://github.com/dotnet/blazor-samples/blob/main/8.0/BlazorSample_BlazorWebApp/DetectIncorrectUsagesOfTransientDisposables.cs
	/// </summary>
	internal static class DetectIncorrectUsageOfTransientDisposables
	{
		/// <summary>
		/// Create a new "patched" service collection that replace ServiceDescriptors with new one capable of tracking the resolution context.
		/// Wraps each ServiceDescriptor so that when an instance is created the provided callback is invoked.
		/// </summary>
		public static IServiceCollection PatchForResolutionContextTracking(IServiceCollection services)
		{
			var collection = new ServiceCollection();

			for (int i = 0; i < services.Count; i++)
			{
				var descriptor = services[i];

				if (!descriptor.IsKeyedService)
				{
					// If the service type or implementation type is an open generic, skip patching.
					if (descriptor.ServiceType.IsGenericTypeDefinition ||
						(descriptor.ImplementationType?.IsGenericTypeDefinition == true))
					{
						collection.Add(descriptor);
						continue;
					}

					// Wrap a registration that uses an ImplementationFactory.
					if (descriptor.ImplementationFactory != null)
					{
						var originalFactory = descriptor.ImplementationFactory;
						var d = ServiceDescriptor.Describe(
							descriptor.ServiceType,
							sp =>
							{
								// track the object we are about to resolve
								ResolutionContext.CurrentStack.Push(new ServiceIdentifier(descriptor.ServiceKey, descriptor.ServiceType));
								var instance = originalFactory(sp);
								ResolutionContext.CurrentStack.Pop();
								return instance;
							},
							descriptor.Lifetime);
						collection.Add(d);
						continue;
					}
					// Wrap a registration that uses an ImplementationType.
					else if (descriptor.ImplementationType != null)
					{
						var implementationType = descriptor.ImplementationType;
						var d = ServiceDescriptor.Describe(
							descriptor.ServiceType,
							sp =>
							{
								ResolutionContext.CurrentStack.Push(new ServiceIdentifier(descriptor.ServiceKey, descriptor.ServiceType));
								var instance = ActivatorUtilities.CreateInstance(sp, implementationType);
								ResolutionContext.CurrentStack.Pop();
								return instance;
							},
							descriptor.Lifetime);
						collection.Add(d);
						continue;
					}
				}
				else
				{
					// If the service type or implementation type is an open generic, skip patching.
					if (descriptor.ServiceType.IsGenericTypeDefinition ||
						(descriptor.KeyedImplementationType?.IsGenericTypeDefinition == true))
					{
						collection.Add(descriptor);
						continue;
					}

					// Wrap a registration that uses an ImplementationFactory.
					if (descriptor.KeyedImplementationFactory != null)
					{
						var originalFactory = descriptor.KeyedImplementationFactory;
						var d = ServiceDescriptor.DescribeKeyed(
							descriptor.ServiceType,
							descriptor.ServiceKey,
							(sp, key) =>
							{
								// track the object we are about to resolve
								ResolutionContext.CurrentStack.Push(new ServiceIdentifier(descriptor.ServiceKey, descriptor.ServiceType));
								var instance = originalFactory(sp, key);
								ResolutionContext.CurrentStack.Pop();
								return instance;
							},
							descriptor.Lifetime);
						collection.Add(d);
						continue;
					}
					// Wrap a registration that uses an ImplementationType.
					else if (descriptor.KeyedImplementationType != null)
					{
						var implementationType = descriptor.KeyedImplementationType;
						var d = ServiceDescriptor.DescribeKeyed(
							descriptor.ServiceType,
							descriptor.ServiceKey,
							(sp, _) =>
							{
								ResolutionContext.CurrentStack.Push(new ServiceIdentifier(descriptor.ServiceKey, descriptor.ServiceType));
								var instance = ActivatorUtilities.CreateInstance(sp, implementationType);
								ResolutionContext.CurrentStack.Pop();
								return instance;
							},
							descriptor.Lifetime);
						collection.Add(d);
						continue;
					}
				}
				// For registrations that use an already-created instance,
				// there's nothing to patch.
				collection.Add(descriptor);
			}

			return collection;
		}

		/// <summary>
		/// Create a new "patched" service collection that will throw an exception if a transient disposable service is resolved in the root scope.
		/// </summary>
		public static (IServiceCollection ServiceCollection, List<ServiceDescriptor> OpenGenericDisposables) PatchForDetectIncorrectUsageOfTransientDisposables(
			IServiceCollection containerBuilder,
			bool allowSingletonToResolveTransientDisposables,
			bool throwOnOpenGenericTransientDisposable
			)
		{
			var collection = new ServiceCollection();
			var openGenericDisposables = new List<ServiceDescriptor>();

			foreach (var descriptor in containerBuilder)
			{
				switch (descriptor.Lifetime)
				{
					// if its an open generic, we can't patch it
					case ServiceLifetime.Transient
						when (descriptor.IsKeyedService && descriptor.KeyedImplementationType?.IsGenericTypeDefinition == true)
							|| (!descriptor.IsKeyedService && descriptor.ImplementationType?.IsGenericTypeDefinition == true):
						if ((descriptor.IsKeyedService && typeof(IDisposable).IsAssignableFrom(descriptor.KeyedImplementationType))
							|| (!descriptor.IsKeyedService && typeof(IDisposable).IsAssignableFrom(descriptor.ImplementationType)))
						{
							if (throwOnOpenGenericTransientDisposable)
							{
								throw new InvalidOperationException(
									$"Trying to register an open generic transient disposable service {descriptor.KeyedImplementationType?.Name}.");
							}
							else
							{
								openGenericDisposables.Add(descriptor);
							}
						}
						collection.Add(descriptor);
						break;
					case ServiceLifetime.Transient
						when (descriptor is { IsKeyedService: true, KeyedImplementationType: not null }
							&& typeof(IDisposable).IsAssignableFrom(descriptor.KeyedImplementationType))
							|| (descriptor is { IsKeyedService: false, ImplementationType: not null }
								&& typeof(IDisposable).IsAssignableFrom(descriptor.ImplementationType)):
						collection.Add(CreatePatchedDescriptor(descriptor, allowSingletonToResolveTransientDisposables));
						break;
					case ServiceLifetime.Transient
						when descriptor is { IsKeyedService: true, KeyedImplementationFactory: not null }:
						collection.Add(CreatePatchedKeyedFactoryDescriptor(descriptor, allowSingletonToResolveTransientDisposables));
						break;
					case ServiceLifetime.Transient
						when descriptor is { IsKeyedService: false, ImplementationFactory: not null }:
						collection.Add(CreatePatchedFactoryDescriptor(descriptor, allowSingletonToResolveTransientDisposables));
						break;
					default:
						collection.Add(descriptor);
						break;
				}
			}

			return (collection, openGenericDisposables);
		}

		private static ServiceDescriptor CreatePatchedFactoryDescriptor(
			ServiceDescriptor original,
			bool allowSingletonToResolveTransientDisposables
			)
		{
			return new ServiceDescriptor(
				original.ServiceType,
				(sp) =>
				{
					var originalFactory = original.ImplementationFactory ??
						throw new InvalidOperationException("originalFactory is null.");

					var originalResult = originalFactory(sp);

					if (sp.GetIsRootScope() && originalResult is IDisposable d)
					{
						//check the ResolutionContext to see if the service is being resolved by a singleton
						//if it is, then it's safe to resolve the transient disposable service
						if (!IsResolvedBySingleton(sp, allowSingletonToResolveTransientDisposables))
						{
							ThrowTransientDisposableException(original.ServiceKey, original.ServiceType, d.GetType(), isFactory: true);
						}
					}

					return originalResult;
				},
				ServiceLifetime.Transient);
		}

		private static ServiceDescriptor CreatePatchedKeyedFactoryDescriptor(
			ServiceDescriptor original,
			bool allowSingletonToResolveTransientDisposables
			)
		{
			return new ServiceDescriptor(
				original.ServiceType,
				original.ServiceKey,
				(sp, obj) =>
				{
					var originalFactory = original.KeyedImplementationFactory ??
										  throw new InvalidOperationException("KeyedImplementationFactory is null.");

					var originalResult = originalFactory(sp, obj);

					if (sp.GetIsRootScope() && originalResult is IDisposable d)
					{
						//check the ResolutionContext to see if the service is being resolved by a singleton
						//if it is, then it's safe to resolve the transient disposable service
						if (!IsResolvedBySingleton(sp, allowSingletonToResolveTransientDisposables))
						{
							ThrowTransientDisposableException(original.ServiceKey, original.ServiceType, d.GetType(), isFactory: true);
						}
					}

					return originalResult;
				},
				ServiceLifetime.Transient);
		}

		private static ServiceDescriptor CreatePatchedDescriptor(
			ServiceDescriptor original,
			bool allowSingletonToResolveTransientDisposables
			)
		{
			if (!original.IsKeyedService)
			{
				return new ServiceDescriptor(
					original.ServiceType,
					(sp) =>
					{
						if (sp.GetIsRootScope())
						{
							//check the ResolutionContext to see if the service is being resolved by a singleton
							//if it is, then it's safe to resolve the transient disposable service
							if (!IsResolvedBySingleton(sp, allowSingletonToResolveTransientDisposables))
							{
								ThrowTransientDisposableException(original.ServiceKey, original.ServiceType, original.ImplementationType, isFactory: false);
							}
						}

						if (original.ImplementationType is null)
						{
							throw new InvalidOperationException(
								"ImplementationType is null.");
						}

						return ActivatorUtilities.CreateInstance(sp,
							original.ImplementationType);
					},
					ServiceLifetime.Transient);
			}
			else
			{
				return new ServiceDescriptor(
					original.ServiceType,
					original.ServiceKey,
					(sp, _) =>
					{
						if (sp.GetIsRootScope())
						{
							//check the ResolutionContext to see if the service is being resolved by a singleton
							//if it is, then it's safe to resolve the transient disposable service
							if (!IsResolvedBySingleton(sp, allowSingletonToResolveTransientDisposables))
							{
								ThrowTransientDisposableException(original.ServiceKey, original.ServiceType, original.KeyedImplementationType, isFactory: false);
							}
						}

						if (original.KeyedImplementationType is null)
						{
							throw new InvalidOperationException(
								"KeyedImplementationType is null.");
						}

						return ActivatorUtilities.CreateInstance(sp,
							original.KeyedImplementationType);
					},
					ServiceLifetime.Transient);
			}
		}

		private static void ThrowTransientDisposableException(object? serviceKey, Type? serviceType, Type? implementationType, bool isFactory)
		{
			var sb = new StringBuilder();
			sb.Append("Trying to resolve Transient Disposable service - ");
			if (serviceKey != null)
			{
				sb.Append($"ServiceKey: {serviceKey}, ");
			}
			if (serviceType != null)
			{
				sb.Append($"ServiceType: {serviceType.FullName}, ");
			}
			if (implementationType != null)
			{
				if (isFactory)
				{
					sb.Append("(factory) ");
				}
				sb.Append($"ImplementationType: {implementationType.FullName}.");
			}
			if (ResolutionContext.CurrentStack.Count > 0)
			{
				sb.AppendLine();
				sb.Append("Requested by (Resolution Context Stack):");
				foreach (var entry in ResolutionContext.CurrentStack)
				{
					sb.AppendLine();
					sb.Append("- ");
					if (entry.ServiceKey != null)
					{
						sb.Append($"ServiceKey: {entry.ServiceKey}, ");
					}
					sb.Append($"ServiceType: {entry.ServiceType.FullName}");
				}
			}
			throw new InvalidOperationException(sb.ToString());
		}

		private static bool IsResolvedBySingleton(
			IServiceProvider sp,
			bool allowSingletonToResolveTransientDisposables
			)
		{
			if (allowSingletonToResolveTransientDisposables)
			{
				var stack = ResolutionContext.CurrentStack;
				foreach (var entry in stack)
				{
					// If we used our tracking, each entry is either a Type or a KeyedResolution.
					bool isSingleton;
					if (entry.ServiceKey == null)
					{
						isSingleton = sp.IsSingletonServiceRegistered(entry.ServiceType);
					}
					else
					{
						isSingleton = sp.IsKeyedSingletonServiceRegistered(entry.ServiceType, entry.ServiceKey);
					}
					if (isSingleton)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
