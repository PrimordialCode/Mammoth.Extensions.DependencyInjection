using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using static Mammoth.Extensions.DependencyInjection.NativeConstructorActivator;
using static Mammoth.Extensions.DependencyInjection.ServiceProviderRegistrationSnapshot;

namespace Mammoth.Extensions.DependencyInjection;

// Validate the original constructor edges before opaque tracking factories start
// nested native resolutions. An explicit DFS stack cannot trigger a native stack
// hop while singleton/scoped cache locks are held. No service is activated here.
internal static class NativeConstructorGraphValidator
{
    internal static Dictionary<ServiceIdentifier, ConstructorPlan> Validate(IServiceProvider provider, Type implementationType, object? key, ConstructorPlan plan)
    {
        var snapshot = provider.GetRequiredService<ServiceProviderRegistrationSnapshot>();
        var plans = new Dictionary<ServiceIdentifier, ConstructorPlan>
        {
            [new ServiceIdentifier(key, implementationType)] = plan
        };
        var active = new HashSet<(int, ServiceIdentifier)>();
        var completed = new HashSet<(int, ServiceIdentifier)>();
        var frames = new Stack<Frame>();
        var root = snapshot.GetConstructorRegistration(ResolutionActivationContext.CurrentOccurrence, key)
            ?? new ConstructorRegistration(-1, new ServiceIdentifier(key, implementationType), implementationType);
        active.Add((root.Index, root.Service));
        frames.Push(new Frame(root, Dependencies(plan).GetEnumerator()));
        try
        {
            while (frames.Count != 0)
            {
                var frame = frames.Peek();
                if (!frame.Dependencies.MoveNext())
                {
                    frames.Pop().Dependencies.Dispose();
                    var identity = (frame.Registration.Index, frame.Registration.Service);
                    active.Remove(identity);
                    completed.Add(identity);
                    continue;
                }
                var registration = frame.Dependencies.Current;
                var next = (registration.Index, registration.Service);
                if (active.Contains(next))
                {
                    var chain = frames.Reverse().Select(entry => Format(entry.Registration.Service))
                        .Concat(new[] { Format(registration.Service) });
                    throw new InvalidOperationException($"A circular dependency was detected for the service of type '{registration.Service.ServiceType}'.{Environment.NewLine}{string.Join(" -> ", chain)}");
                }
                if (completed.Contains(next)) continue;
                var implementation = new ServiceIdentifier(registration.Service.ServiceKey, registration.ImplementationType);
                if (!plans.TryGetValue(implementation, out var childPlan)
                    || !ReferenceEquals(childPlan.ServiceKey, registration.Service.ServiceKey))
                {
                    childPlan = Plan(provider, registration.ImplementationType, registration.Service.ServiceKey, graphOnly: true);
                    plans[implementation] = childPlan;
                }
                active.Add(next);
                frames.Push(new Frame(registration, Dependencies(childPlan).GetEnumerator()));
            }
        }
        finally
        {
            foreach (var frame in frames) frame.Dependencies.Dispose();
        }

        return plans;

        IEnumerable<ConstructorRegistration> Dependencies(ConstructorPlan constructorPlan)
        {
            foreach (var argument in constructorPlan.Arguments)
            {
                if (argument.ServiceType == null) continue;
                foreach (var registration in snapshot.GetConstructorRegistrations(argument.ServiceType, argument.Key))
                    yield return registration;
            }
        }
    }

    private static string Format(ServiceIdentifier service) => service.ServiceKey == null
        ? service.ServiceType.ToString()
        : string.Format(CultureInfo.InvariantCulture, "{0} (ServiceKey: {1})", service.ServiceType, service.ServiceKey);

    private sealed class Frame(ConstructorRegistration registration, IEnumerator<ConstructorRegistration> dependencies)
    {
        internal ConstructorRegistration Registration { get; } = registration;
        internal IEnumerator<ConstructorRegistration> Dependencies { get; } = dependencies;
    }
}
