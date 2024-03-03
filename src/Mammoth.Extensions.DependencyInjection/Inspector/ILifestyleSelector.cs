using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Inspector
{
	/// <summary>
	/// Represents a lifestyle selector that allows specifying the lifestyle of the service descriptors.
	/// </summary>
	public interface ILifestyleSelector
	{
		/// <summary>
		/// Specifies that the service descriptors should have a transient lifestyle.
		/// </summary>
		/// <returns>The collection of service descriptors.</returns>
		IEnumerable<ServiceDescriptor> LifestyleTransient(Dependency[]? dependsOn = null);

		/// <summary>
		/// Specifies that the service descriptors should have a scoped lifestyle.
		/// </summary>
		/// <returns>The collection of service descriptors.</returns>
		IEnumerable<ServiceDescriptor> LifestyleScoped(Dependency[]? dependsOn = null);

		/// <summary>
		/// Specifies that the service descriptors should have a singleton lifestyle.
		/// </summary>
		/// <returns>The collection of service descriptors.</returns>
		IEnumerable<ServiceDescriptor> LifestyleSingleton(Dependency[]? dependsOn = null);
	}
}
