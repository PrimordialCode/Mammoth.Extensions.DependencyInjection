using System;

namespace Mammoth.Extensions.DependencyInjection.Inspector
{
	/// <summary>
	/// Represents a class selector that allows specifying the base type.
	/// </summary>
	public interface IClassSelector
	{
		/// <summary>
		/// Specifies that the selected types should match a condition to be included.
		/// </summary>
		/// <param name="ifFilter">Condition to match</param>
		/// <returns>A Service selector</returns>
		IServiceSelector If(Predicate<Type> ifFilter);

		/// <summary>
		/// Specifies the base type that the selected types should be based on.
		/// </summary>
		/// <param name="baseType">The base type.</param>
		/// <returns>The service selector.</returns>
		IServiceSelector BasedOn(Type baseType);

		/// <summary>
		/// Specifies the base type that the selected types should be based on.
		/// </summary>
		/// <typeparam name="Type">The base type.</typeparam>
		/// <returns>The service selector.</returns>
		IServiceSelector BasedOn<Type>();

		/// <summary>
		/// Specifies that the selected types should be in the same exact namespace as the specified type.
		/// </summary>
		IServiceSelector InSameNamespaceAs<Type>();

		/// <summary>
		/// Specifies that the selected types should be in the same namespace as the specified type.
		/// </summary>
		/// <param name="includeSubnamespaces">If set to true, will also include types from subnamespaces.</param>
		IServiceSelector InSameNamespaceAs<Type>(bool includeSubnamespaces);
	}
}
