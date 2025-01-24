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
		/// <param name="condition">Condition to match</param>
		/// <returns>A Service selector</returns>
#pragma warning disable CA1716 // Identifiers should not match keywords
		IServiceSelector If(Predicate<Type> condition);
#pragma warning restore CA1716 // Identifiers should not match keywords

		/// <summary>
		/// Specifies the base type that the selected types should be based on.
		/// </summary>
		/// <param name="baseType">The base type.</param>
		/// <returns>The service selector.</returns>
		IServiceSelector BasedOn(Type baseType);

		/// <summary>
		/// Specifies the base type that the selected types should be based on.
		/// </summary>
		/// <typeparam name="T">The base type.</typeparam>
		/// <returns>The service selector.</returns>
		IServiceSelector BasedOn<T>();

		/// <summary>
		/// Specifies that the selected types should be in the same exact namespace as the specified type.
		/// </summary>
		IServiceSelector InSameNamespaceAs<T>();

		/// <summary>
		/// Specifies that the selected types should be in the same namespace as the specified type.
		/// </summary>
		/// <param name="includeSubNamespaces">If set to true, will also include types from sub-namespaces.</param>
		IServiceSelector InSameNamespaceAs<T>(bool includeSubNamespaces);
	}
}
