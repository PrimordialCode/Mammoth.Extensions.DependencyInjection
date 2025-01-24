namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Provides extension methods for <see cref="Type"/>.
	/// </summary>
	public static class TypeExtensions
	{
		/// <summary>
		/// <para>Determines whether the specified type is a framework type.</para>
		/// <para>A type is considered a framework type if it is defined in the System namespace or if it is defined in the mscorlib assembly.</para>
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns><c>true</c> if the specified type is a framework type; otherwise, <c>false</c>.</returns>
		public static bool IsFrameworkType(this Type type)
		{
			return type.Assembly.FullName.StartsWith("System", StringComparison.InvariantCulture)
				|| type.Assembly.FullName.Contains("mscorlib");
		}

		/// <summary>
		/// Retrieves all the interfaces implemented by the specified type, including interfaces implemented by its base types.
		/// </summary>
		/// <param name="type">The type to retrieve the interfaces from.</param>
		/// <returns>An enumerable collection of interfaces implemented by the specified type.</returns>
		public static IEnumerable<Type> GetAllInterfaces(this Type type)
		{
			var interfaces = new HashSet<Type>();

			// Add interfaces of the current type
			foreach (var interfaceType in type.GetInterfaces())
			{
				interfaces.Add(interfaceType);
			}

			// Recursively add interfaces of base types
			var baseType = type.BaseType;
			while (baseType != null)
			{
				foreach (var interfaceType in baseType.GetInterfaces())
				{
					interfaces.Add(interfaceType);
				}
				baseType = baseType.BaseType;
			}

			return interfaces;
		}
	}
}
