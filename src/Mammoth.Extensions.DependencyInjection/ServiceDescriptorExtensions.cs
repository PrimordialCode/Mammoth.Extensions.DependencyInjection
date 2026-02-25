using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for <see cref="ServiceDescriptor"/>.
	/// </summary>
	internal static class ServiceDescriptorExtensions
	{
		/// <summary>
		/// <para>
		/// Changes the service type of the <paramref name="original"/> <see cref="ServiceDescriptor"/> to the specified <paramref name="newServiceType"/>.
		/// </para>
		/// <para>
		/// This method only handles type-based and instance-based registrations. Factory-based registrations
		/// are not supported here because they are handled directly in the decorator extension methods
		/// by capturing the original factory in a closure (see <see cref="ServiceCollectionExtensions.Decorate{TService, TDecorator}"/>).
		/// </para>
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
	}
}