using System;
using System.Reflection;

namespace Mammoth.Extensions.DependencyInjection.Inspector
{
	/// <summary>
	/// Represents an assembly inspector that allows selecting types and creating service descriptors.
	/// </summary>
	public interface IAssemblySelector
	{
		/// <summary>
		/// Specifies the current calling assembly.
		/// </summary>
		/// <returns>The class selector.</returns>
		IClassSelector FromAssembly(Assembly assembly);

		/// <summary>
		/// Specifies the current calling assembly.
		/// </summary>
		/// <returns>The class selector.</returns>
		IClassSelector FromThisAssembly();

		/// <summary>
		/// Specifies the assembly containing the types to select from.
		/// </summary>
		/// <returns>The class selector.</returns>
		IClassSelector FromAssemblyContaining(Type type);

		/// <summary>
		/// Specifies the assembly containing the types to select from.
		/// </summary>
		/// <typeparam name="T">The type contained in the assembly.</typeparam>
		/// <returns>The class selector.</returns>
		IClassSelector FromAssemblyContaining<T>();
	}
}
