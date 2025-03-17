using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// original code took from blazor-samples:
	/// https://github.com/dotnet/blazor-samples/blob/main/8.0/BlazorSample_BlazorWebApp/DetectIncorrectUsagesOfTransientDisposables.cs
	/// </summary>
	internal static class DetectIncorrectUsageOfTransientDisposables
	{
		/// <summary>
		/// Create a new "patched" service collection that will throw an exception if a transient disposable service is resolved in the root scope.
		/// </summary>
		public static IServiceCollection PatchServiceCollection(IServiceCollection containerBuilder)
		{
			var collection = new ServiceCollection();

			foreach (var descriptor in containerBuilder)
			{
				switch (descriptor.Lifetime)
				{
					case ServiceLifetime.Transient
						when (descriptor is { IsKeyedService: true, KeyedImplementationType: not null }
							&& typeof(IDisposable).IsAssignableFrom(descriptor.KeyedImplementationType))
							|| (descriptor is { IsKeyedService: false, ImplementationType: not null }
								&& typeof(IDisposable).IsAssignableFrom(descriptor.ImplementationType)):
						collection.Add(CreatePatchedDescriptor(descriptor));
						break;
					case ServiceLifetime.Transient
						when descriptor is { IsKeyedService: true, KeyedImplementationFactory: not null }:
						collection.Add(CreatePatchedKeyedFactoryDescriptor(descriptor));
						break;
					case ServiceLifetime.Transient
						when descriptor is { IsKeyedService: false, ImplementationFactory: not null }:
						collection.Add(CreatePatchedFactoryDescriptor(descriptor));
						break;
					default:
						collection.Add(descriptor);
						break;
				}
			}

			return collection;
		}

		private static ServiceDescriptor CreatePatchedFactoryDescriptor(
			ServiceDescriptor original)
		{
			var newDescriptor = new ServiceDescriptor(
				original.ServiceType,
				(sp) =>
				{
					var originalFactory = original.ImplementationFactory ??
						throw new InvalidOperationException("originalFactory is null.");

					var originalResult = originalFactory(sp);

					if (sp.GetIsRootScope() && originalResult is IDisposable d)
					{
						ThrowTransientDisposableException(d.GetType().Name);
					}

					return originalResult;
				},
				ServiceLifetime.Transient);

			return newDescriptor;
		}

		private static ServiceDescriptor CreatePatchedKeyedFactoryDescriptor(ServiceDescriptor original)
		{
			var newDescriptor = new ServiceDescriptor(
				original.ServiceType,
				original.ServiceKey,
				(sp, obj) =>
				{
					var originalFactory = original.KeyedImplementationFactory ??
										  throw new InvalidOperationException("KeyedImplementationFactory is null.");

					var originalResult = originalFactory(sp, obj);

					if (sp.GetIsRootScope() && originalResult is IDisposable d)
					{
						ThrowTransientDisposableException(d.GetType().Name);
					}

					return originalResult;
				},
				ServiceLifetime.Transient);

			return newDescriptor;
		}

		private static ServiceDescriptor CreatePatchedDescriptor(
			ServiceDescriptor original)
		{
			var newDescriptor = new ServiceDescriptor(
				original.ServiceType,
				(sp) =>
				{
					if (sp.GetIsRootScope())
					{
						ThrowTransientDisposableException(original.ImplementationType?.Name);
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

			return newDescriptor;
		}

		private static void ThrowTransientDisposableException(string? typeName)
		{
			throw new InvalidOperationException(
				$"Trying to resolve transient disposable service {typeName} in the wrong scope (root scope).");
		}
	}
}
