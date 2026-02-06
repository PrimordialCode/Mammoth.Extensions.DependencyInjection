using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for <see cref="ServiceDescriptor"/>.
	/// </summary>
	internal static class ServiceDescriptorExtensions
	{
		/// <summary>
		/// Changes the service type of the <paramref name="original"/> <see cref="ServiceDescriptor"/> to the specified <paramref name="newServiceType"/>.
		/// </summary>
		/// <param name="original">The original <see cref="ServiceDescriptor"/>.</param>
		/// <param name="newServiceType">The new service type.</param>
		/// <returns>A new <see cref="ServiceDescriptor"/> with the updated service type.</returns>
		internal static ServiceDescriptor ChangeServiceType(this ServiceDescriptor original, Type newServiceType)
		{
			if (!original.IsKeyedService)
			{
				// Check if the original descriptor was registered with a specific instance
				if (original.ImplementationInstance != null)
				{
					return new ServiceDescriptor(newServiceType, original.ImplementationInstance);
				}

				// If the original descriptor was registered with a type
				return new ServiceDescriptor(newServiceType, original.ImplementationType!, original.Lifetime);
			}
			else
			{
				// Check if the original descriptor was registered with a specific instance
				if (original.KeyedImplementationInstance != null)
				{
					return new ServiceDescriptor(newServiceType, original.ServiceKey, original.KeyedImplementationInstance);
				}

				// If the original descriptor was registered with a type
				return new ServiceDescriptor(newServiceType, original.ServiceKey, original.KeyedImplementationType!, original.Lifetime);
			}
		}

		/// <summary>
		/// Gets the implementation type of the <paramref name="descriptor"/> <see cref="ServiceDescriptor"/>.
		/// </summary>
		/// <param name="descriptor">The <see cref="ServiceDescriptor"/>.</param>
		/// <returns>
		/// The implementation type of the <paramref name="descriptor"/> <see cref="ServiceDescriptor"/>,
		/// or <c>null</c> if the descriptor was registered with a factory function.
		/// </returns>
		internal static Type? GetImplementationType(this ServiceDescriptor descriptor)
		{
			// see: https://github.com/dotnet/runtime/issues/95789
			if (!descriptor.IsKeyedService)
			{
				// If the descriptor was registered with a type, return it directly
				if (descriptor.ImplementationType != null)
				{
					return descriptor.ImplementationType;
				}
				// If the descriptor was registered with a specific instance
				if (descriptor.ImplementationInstance != null)
				{
					return descriptor.ImplementationInstance.GetType();
				}
			}
			else
			{
				// If the descriptor was registered with a type, return it directly
				if (descriptor.KeyedImplementationType != null)
				{
					return descriptor.KeyedImplementationType;
				}
				// If the descriptor was registered with a specific instance
				if (descriptor.KeyedImplementationInstance != null)
				{
					return descriptor.KeyedImplementationInstance.GetType();
				}
			}

			return null;
		}
	}
}