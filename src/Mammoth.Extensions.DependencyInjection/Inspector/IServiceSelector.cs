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
		/// <param name="condition">Condition to match</param>
		/// <returns>A Service selector</returns>
#pragma warning disable CA1716 // Identifiers should not match keywords
		IServiceSelector If(Predicate<Type> condition);
#pragma warning restore CA1716 // Identifiers should not match keywords

		/// <summary>
		/// Specifies that all interfaces implemented by the selected types should be used as service interfaces.
		/// </summary>
		/// <returns>The lifestyle selector.</returns>
		/// <remarks>
		/// Implementations with unbound generic parameters are skipped before configuration; generic interface mappings are not inferred.
		/// Interfaces in System or its child namespaces, and interfaces from the assembly named exactly mscorlib,
		/// are excluded. Comparisons are ordinal and case-sensitive; application assembly name prefixes do not affect selection.
		/// </remarks>
		ILifestyleSelector WithServiceAllInterfaces();

		/// <summary>
		/// Specifies that the base type should be used as service interface.
		/// </summary>
		/// <returns>The lifestyle selector.</returns>
		/// <remarks>
		/// Implementations with unbound generic parameters are skipped before configuration unless the base service is a generic type definition.
		/// </remarks>
		ILifestyleSelector WithServiceBase();

		/// <summary>
		/// Specifies that the selected types should be used as service interfaces.
		/// </summary>
		/// <returns>The lifestyle selector.</returns>
		ILifestyleSelector WithServiceSelf();
	}
}
