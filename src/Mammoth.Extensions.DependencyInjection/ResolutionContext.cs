namespace Mammoth.Extensions.DependencyInjection;

/// <summary>
/// Holds an AsyncLocal stack for types or markers being resolved. Provides access to the current stack of
/// ServiceIdentifiers.
/// </summary>
internal static class ResolutionContext
{
	// An AsyncLocal stack that holds the types (or keyed markers) currently being resolved.
	private static readonly AsyncLocal<Stack<ServiceIdentifier>?> _currentStack = new AsyncLocal<Stack<ServiceIdentifier>?>();

	/// <summary>
	/// Returns the current stack of ServiceIdentifier. If the stack is not initialized, it creates a new one.
	/// </summary>
	public static Stack<ServiceIdentifier> CurrentStack
	{
		get => _currentStack.Value ??= new Stack<ServiceIdentifier>();
	}
}
