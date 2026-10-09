using System.Reflection;
using System.Runtime.CompilerServices;

namespace Mammoth.Extensions.DependencyInjection;

// Object accepts every implementation; reference identity isolates each original
// registration without changing its key or hiding its native constructor call site.
internal sealed class DecorationServiceType() : TypeDelegator(typeof(object))
{
    public override Type UnderlyingSystemType => this;
    public override bool IsAssignableFrom(Type? candidate) => ReferenceEquals(this, candidate) || typeof(object).IsAssignableFrom(candidate);
    public override bool Equals(object? value) => ReferenceEquals(this, value);
    public override bool Equals(Type? value) => ReferenceEquals(this, value);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
