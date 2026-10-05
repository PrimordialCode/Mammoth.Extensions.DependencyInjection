using Microsoft.Extensions.DependencyInjection;
using System.Collections;

namespace Mammoth.Extensions.DependencyInjection;

/// <summary>
/// Holds immutable, flow-local frames for the descriptors being resolved.
/// </summary>
internal static class ResolutionContext
{
	private static readonly AsyncLocal<Frame?> _current = new();

	public static Frame? Current => _current.Value;

	public static IReadOnlyCollection<ServiceDescriptor> CurrentStack =>
		Current ?? (IReadOnlyCollection<ServiceDescriptor>)Array.Empty<ServiceDescriptor>();

	public static Frame? Push(ServiceDescriptor descriptor)
	{
		var previous = Current;
		_current.Value = new Frame(descriptor, previous);
		return previous;
	}

	public static void Restore(Frame? previous) => _current.Value = previous;

	// ExecutionContext branches inherit this immutable ancestry snapshot. Each branch
	// replaces only its own AsyncLocal value; neither a sibling's push nor the original
	// caller's finally can change it, even when a child task outlives that caller.
	internal sealed class Frame(ServiceDescriptor descriptor, Frame? parent) : IReadOnlyCollection<ServiceDescriptor>
	{
		public ServiceDescriptor Descriptor { get; } = descriptor;
		public Frame? Parent { get; } = parent;
		public int Count { get; } = (parent?.Count ?? 0) + 1;

		public IEnumerator<ServiceDescriptor> GetEnumerator()
		{
			for (Frame? frame = this; frame != null; frame = frame.Parent)
			{
				yield return frame.Descriptor;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
