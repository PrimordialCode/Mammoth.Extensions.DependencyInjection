using System;
using System.Collections.Generic;
using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Inspector
{
	/// <summary>
	/// Represents a lifestyle selector that allows specifying the lifestyle of the service descriptors.
	/// </summary>
	public interface ILifestyleSelector
	{
		/// <summary>
		/// Configures the service registration.
		/// </summary>
		/// <param name="configurer">action used to configure each service that will be registered</param>
		/// <returns>The lifestyle selector.</returns>
		ILifestyleSelector Configure(Action<ServiceRegistration> configurer);

		/// <summary>
		/// Specifies that the service descriptors should have a transient lifestyle.
		/// </summary>
		/// <returns>The collection of service descriptors.</returns>
		IEnumerable<ServiceDescriptor> LifestyleTransient();

		/// <summary>
		/// Specifies that the service descriptors should have a scoped lifestyle.
		/// </summary>
		/// <returns>The collection of service descriptors.</returns>
		IEnumerable<ServiceDescriptor> LifestyleScoped();

		/// <summary>
		/// Specifies that the service descriptors should have a singleton lifestyle.
		/// </summary>
		/// <returns>The collection of service descriptors.</returns>
		IEnumerable<ServiceDescriptor> LifestyleSingleton();
	}
}
