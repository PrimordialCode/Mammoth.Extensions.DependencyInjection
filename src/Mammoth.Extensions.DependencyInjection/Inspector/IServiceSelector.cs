using System;

namespace Mammoth.Extensions.DependencyInjection.Inspector
{
	/// <summary>
	/// Represents a service selector that allows specifying the service interfaces.
	/// </summary>
	public interface IServiceSelector
	{
		/// <summary>
		/// Specifies that the selected types should match a condition to be included.
		/// </summary>
		/// <param name="ifFilter">Condition to match</param>
		/// <returns>A Service selector</returns>
		IServiceSelector If(Predicate<Type> ifFilter);

		/// <summary>
		/// Specifies that all interfaces implemented by the selected types should be used as service interfaces.
		/// </summary>
		/// <returns>The lifestyle selector.</returns>
		/// <remarks>
		/// Some interfaces will be excluded:
		/// - all those from the System namespace.
		/// </remarks>
		ILifestyleSelector WithServiceAllInterfaces();

		/// <summary>
		/// Specifies that the base type should be used as service interface.
		/// </summary>
		/// <returns>The lifestyle selector.</returns>
		ILifestyleSelector WithServiceBase();

		/// <summary>
		/// Specifies that the selected types should be used as service interfaces.
		/// </summary>
		/// <returns>The lifestyle selector.</returns>
		ILifestyleSelector WithServiceSelf();
	}
}
