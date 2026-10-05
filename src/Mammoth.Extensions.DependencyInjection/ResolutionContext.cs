using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

/// <summary>
/// Holds an AsyncLocal stack for the descriptors being resolved, including their actual registered lifetimes.
/// </summary>
internal static class ResolutionContext
{
	// An AsyncLocal stack that holds the descriptors currently being resolved.
	private static readonly AsyncLocal<Stack<ServiceDescriptor>?> _currentStack = new AsyncLocal<Stack<ServiceDescriptor>?>();

	/// <summary>
	/// Returns the current stack of ServiceDescriptor. If the stack is not initialized, it creates a new one.
	/// </summary>
	public static Stack<ServiceDescriptor> CurrentStack
	{
		get => _currentStack.Value ??= new Stack<ServiceDescriptor>();
	}
}
