namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Provides extension methods for <see cref="Type"/>.
	/// </summary>
	public static class TypeExtensions
	{
		/// <summary>
		/// <para>Determines whether the specified type is a framework type.</para>
		/// <para>A type is considered a framework type if it is defined in the System namespace or one of its child namespaces, or in the assembly named exactly mscorlib. Namespace and assembly names are compared ordinally and case-sensitively.</para>
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns><c>true</c> if the specified type is a framework type; otherwise, <c>false</c>.</returns>
		public static bool IsFrameworkType(this Type type)
		{
			var typeNamespace = type.Namespace;
			return string.Equals(typeNamespace, "System", StringComparison.Ordinal)
				|| typeNamespace?.StartsWith("System.", StringComparison.Ordinal) == true
				|| string.Equals(type.Assembly.GetName().Name, "mscorlib", StringComparison.Ordinal);
		}
	}
}
