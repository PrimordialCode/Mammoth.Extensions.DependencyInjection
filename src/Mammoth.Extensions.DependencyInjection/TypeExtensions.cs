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
			var assemblyFullName = type.Assembly.FullName;
			return !string.IsNullOrEmpty(assemblyFullName)
				&& (assemblyFullName.StartsWith("System", StringComparison.InvariantCulture)
				|| assemblyFullName.Contains("mscorlib"));
		}
	}
}
