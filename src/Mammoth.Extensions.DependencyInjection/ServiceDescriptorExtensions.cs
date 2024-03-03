using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for <see cref="ServiceDescriptor"/>.
	/// </summary>
	public static class ServiceDescriptorExtensions
	{
		/// <summary>
		/// Changes the service type of the <paramref name="original"/> <see cref="ServiceDescriptor"/> to the specified <paramref name="newServiceType"/>.
		/// </summary>
		/// <param name="original">The original <see cref="ServiceDescriptor"/>.</param>
		/// <param name="newServiceType">The new service type.</param>
		/// <returns>A new <see cref="ServiceDescriptor"/> with the updated service type.</returns>
		public static ServiceDescriptor ChangeServiceType(this ServiceDescriptor original, Type newServiceType)
		{
			if (!original.IsKeyedService)
			{
				// Check if the original descriptor uses a factory function
				if (original.ImplementationFactory != null)
				{
					return new ServiceDescriptor(newServiceType, original.ImplementationFactory, original.Lifetime);
				}

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
				// Check if the original descriptor uses a factory function
				if (original.KeyedImplementationFactory != null)
				{
					return new ServiceDescriptor(newServiceType, original.ServiceKey, original.KeyedImplementationFactory, original.Lifetime);
				}

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
		/// The implementation type of the <paramref name="descriptor"/> <see cref="ServiceDescriptor"/>.
		/// if the service descriptor was registered with a factory function, a proxy type will be returned.
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

				// If the descriptor was registered with a factory function
				// there's no easy way to find out the function return type
				// we assume it's compatible with the ServiceType that was used to register the service
				if (descriptor.ImplementationFactory != null)
				{
					return CreateProxyTypeForServiceInterface(descriptor.ServiceType);
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

				// If the descriptor was registered with a factory function
				// there's no easy way to find out the function return type
				// we assume it's compatible with the ServiceType that was used to register the service
				if (descriptor.KeyedImplementationFactory != null)
				{
					return CreateProxyTypeForServiceInterface(descriptor.ServiceType);
				}
			}

			return null;
		}

		internal static Type CreateProxyTypeForServiceInterface(this Type serviceType)
		{
			if (!serviceType.IsInterface)
			{
				throw new InvalidOperationException("The service type must be an interface to create a proxy.");
			}

			var assemblyName = new AssemblyName("DynamicProxies");
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
			var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

			// Define a new interface that inherits from the existing service interface,
			// this interface will be used as marker to create new service descriptors
			var typeBuilder = moduleBuilder.DefineType(
				$"{serviceType.Name}_Proxy_{Guid.NewGuid()}",
				TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public,
				null,
				new Type[] { serviceType }
			);

			return typeBuilder.CreateTypeInfo()!.AsType();
		}
	}
}