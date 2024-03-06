using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Inspector
{
	/// <summary>
	/// A very lightweight assembly inspector that allows selecting types and creating service descriptors.
	/// </summary>
	public class AssemblyInspector : IAssemblySelector, IClassSelector, IServiceSelector, ILifestyleSelector
	{
		private Assembly? _assembly;
		private Type? _baseType;
		private Type? _inSameNamespaceAsType;
		private bool _includeSubnamespaces;
		private Predicate<Type>? _ifFilter;
		private ServiceSelection _serviceSelection;
		private Action<ServiceRegistration>? _serviceRegistrationConfigurer;

		private enum ServiceSelection
		{
			Base,
			AllInterfaces,
			Self
		}

		/// <inheritdoc/>
		public IClassSelector FromAssembly(Assembly assembly)
		{
			_assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
			return this;
		}

		/// <inheritdoc/>
		public IClassSelector FromThisAssembly()
		{
			_assembly = Assembly.GetCallingAssembly();
			return this;
		}

		/// <inheritdoc/>
		public IClassSelector FromAssemblyContaining(Type type)
		{
			_assembly = type.Assembly;
			return this;
		}

		/// <inheritdoc/>
		public IClassSelector FromAssemblyContaining<T>()
		{
			_assembly = typeof(T).Assembly;
			return this;
		}

		/// <inheritdoc/>
		public IServiceSelector BasedOn(Type baseType)
		{
			_baseType = baseType;
			return this;
		}

		/// <inheritdoc/>
		public IServiceSelector BasedOn<Type>()
		{
			_baseType = typeof(Type);
			return this;
		}

		/// <inheritdoc/>
		public IServiceSelector InSameNamespaceAs<Type>()
		{
			_inSameNamespaceAsType = typeof(Type);
			return this;
		}

		/// <inheritdoc/>
		public IServiceSelector InSameNamespaceAs<Type>(bool includeSubnamespaces)
		{
			_inSameNamespaceAsType = typeof(Type);
			_includeSubnamespaces = includeSubnamespaces;
			return this;
		}

		/// <inheritdoc/>
		public IServiceSelector If(Predicate<Type> ifFilter)
		{
			if (_ifFilter != null)
			{
				throw new NotSupportedException("Cannot set if filter more than once.");
			}

			_ifFilter = ifFilter ?? throw new ArgumentNullException(nameof(ifFilter));
			return this;
		}

		/// <inheritdoc/>
		public ILifestyleSelector WithServiceAllInterfaces()
		{
			_serviceSelection = ServiceSelection.AllInterfaces;
			return this;
		}

		/// <inheritdoc/>
		public ILifestyleSelector WithServiceBase()
		{
			_serviceSelection = ServiceSelection.Base;
			return this;
		}

		/// <inheritdoc/>
		public ILifestyleSelector WithServiceSelf()
		{
			_serviceSelection = ServiceSelection.Self;
			return this;
		}

		/// <inheritdoc/>
		public ILifestyleSelector Configure(Action<ServiceRegistration> configurer)
		{
			_serviceRegistrationConfigurer = configurer;
			return this;
		}

		/// <inheritdoc/>
		public IEnumerable<ServiceDescriptor> LifestyleTransient()
		{
			return CreateServiceDescriptors(ServiceLifetime.Transient);
		}

		/// <inheritdoc/>
		public IEnumerable<ServiceDescriptor> LifestyleScoped()
		{
			return CreateServiceDescriptors(ServiceLifetime.Scoped);
		}

		/// <inheritdoc/>
		public IEnumerable<ServiceDescriptor> LifestyleSingleton()
		{
			return CreateServiceDescriptors(ServiceLifetime.Singleton);
		}

		private IEnumerable<ServiceDescriptor> CreateServiceDescriptors(ServiceLifetime lifetime)
		{
			var descriptors = new List<ServiceDescriptor>();
			var types = FilterTypes(_assembly!.GetTypes());
			// filter the types given the selected strategy
			foreach (var type in types)
			{
				switch (_serviceSelection)
				{
					case ServiceSelection.AllInterfaces:
						// inpect interfaces (even of the base types) and exclude some (like IDisposable)
						foreach (var interfaceType in type.GetAllInterfaces())
						{
							if (!interfaceType.IsFrameworkType())
							{
								AddServiceDescriptorToDescriptors(lifetime, descriptors, type, interfaceType);
							}
						}
						break;
					case ServiceSelection.Base:
						if (_baseType == null)
						{
							throw new InvalidOperationException("The base type is not specified, you need to call BasedOn().");
						}
						AddServiceDescriptorToDescriptors(lifetime, descriptors, type, _baseType);
						break;
					case ServiceSelection.Self:
						AddServiceDescriptorToDescriptors(lifetime, descriptors, type, type);
						break;
				}
			}
			return descriptors;
		}

		private void AddServiceDescriptorToDescriptors(ServiceLifetime lifetime, List<ServiceDescriptor> descriptors, Type implementationType, Type serviceType)
		{
			object? serviceKey = null;
			Dependency[]? dependsOn = null;
			if (_serviceRegistrationConfigurer != null)
			{
				var serviceRegistration = new ServiceRegistration();
				_serviceRegistrationConfigurer(serviceRegistration);
				serviceKey = serviceRegistration.ServiceKey;
				dependsOn = serviceRegistration.DependsOn;
			}
			if (dependsOn == null || dependsOn.Length == 0)
			{
				descriptors.Add(new ServiceDescriptor(serviceType, serviceKey, implementationType, lifetime));
			}
			else
			{
				ServiceCollectionExtensions.GetConstructorAndParameters(implementationType, out ConstructorInfo ctor, out ParameterInfo[] parameteter);
				if (serviceKey == null)
				{
					descriptors.Add(new ServiceDescriptor(serviceType, ServiceCollectionExtensions.DependsOnResolutionFunc<object>(dependsOn, ctor, parameteter), lifetime));
				}
				else
				{
					descriptors.Add(new ServiceDescriptor(serviceType, serviceKey, ServiceCollectionExtensions.KeyedDependsOnResolutionFunc<object>(dependsOn, ctor, parameteter), lifetime));
				}
			}
		}

		private IEnumerable<Type> FilterTypes(Type[] types)
		{
			var filteredTypes = types.Where(type => type.IsClass && !type.IsAbstract);
			if (_baseType != null)
			{
				return filteredTypes.Where(t => _baseType.IsAssignableFrom(t)).ToArray();
			}
			if (_inSameNamespaceAsType != null)
			{
				if (_includeSubnamespaces)
				{
					return filteredTypes.Where(t => t.Namespace?.StartsWith(_inSameNamespaceAsType.Namespace) == true).ToArray();
				}
				return filteredTypes.Where(t => t.Namespace == _inSameNamespaceAsType.Namespace).ToArray();
			}
			if (_ifFilter != null)
			{
				return filteredTypes.Where(t => _ifFilter(t)).ToArray();
			}
			return types;
		}
	}
}
