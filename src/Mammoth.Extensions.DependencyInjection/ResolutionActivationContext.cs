using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using static Mammoth.Extensions.DependencyInjection.NativeConstructorActivator;

namespace Mammoth.Extensions.DependencyInjection;

// Diagnostic ancestry deliberately flows into background work. Active synchronous
// resolutions have a different lifetime and must not use that ancestry as a cycle set.
internal static class ResolutionActivationContext
{
    [ThreadStatic]
    private static Frame? _current;

    internal static int CurrentOccurrence => _current?.Occurrence ?? -1;

    internal static IDisposable Enter(object registration, ServiceDescriptor descriptor,
        IServiceProvider provider, object? key, int occurrence = -1)
    {
        var previous = _current;
        CheckCycle(previous);
        var frame = new Frame(registration, descriptor, provider, key, previous, occurrence);
        _current = frame;
        return frame;

        void CheckCycle(Frame? current)
        {
            for (var entry = current; entry != null; entry = entry.Parent)
            {
                if (entry.TaskId == Task.CurrentId && ReferenceEquals(entry.Registration, registration)
                    && ReferenceEquals(entry.Provider, provider) && Equals(entry.Key, key))
                {
                    var chain = new List<string>();
                    for (var item = current; item != null; item = item.Parent)
                        chain.Add(Format(item.Descriptor, item.Key));
                    chain.Reverse();
                    chain.Add(Format(descriptor, key));
                    throw new InvalidOperationException($"A circular dependency was detected for the service of type '{descriptor.ServiceType}'.{Environment.NewLine}{string.Join(" -> ", chain)}");
                }
            }
        }
    }

    // Share only successfully validated, provider-independent plans during the
    // current synchronous activation. No plan/visited state survives its frame.
    internal static bool TryGetPlan(ServiceProviderRegistrationSnapshot snapshot, ServiceIdentifier identity,
        out ConstructorPlan plan)
    {
        if (ReferenceEquals(_current?.PlanSnapshot, snapshot) && _current!.Plans != null
            && _current.Plans.TryGetValue(identity, out var existing)
            && ReferenceEquals(existing.ServiceKey, identity.ServiceKey))
        {
            plan = existing;
            return true;
        }
        plan = null!;
        return false;
    }

    internal static void SetPlans(ServiceProviderRegistrationSnapshot snapshot, Dictionary<ServiceIdentifier, ConstructorPlan> plans)
    {
        if (_current == null) return;
        _current.PlanSnapshot = snapshot;
        _current.Plans = plans;
    }

    private static string Format(ServiceDescriptor descriptor, object? key) => key == null
        ? descriptor.ServiceType.ToString()
        : string.Format(CultureInfo.InvariantCulture, "{0} (ServiceKey: {1})", descriptor.ServiceType, key);

    private sealed class Frame(object registration, ServiceDescriptor descriptor, IServiceProvider provider,
        object? key, Frame? parent, int occurrence) : IDisposable
    {
        internal ServiceProviderRegistrationSnapshot? PlanSnapshot { get; set; } = parent?.PlanSnapshot;
        internal Dictionary<ServiceIdentifier, ConstructorPlan>? Plans { get; set; } = parent?.Plans;
        // A task may run inline on this thread while owning an independent execution flow.
        internal int? TaskId { get; } = Task.CurrentId;
        internal int Occurrence { get; } = occurrence;
        internal object Registration { get; } = registration;
        internal ServiceDescriptor Descriptor { get; } = descriptor;
        internal IServiceProvider Provider { get; } = provider;
        internal object? Key { get; } = key;
        internal Frame? Parent { get; } = parent;

        public void Dispose()
        {
            _current = Parent;
        }
    }

}
