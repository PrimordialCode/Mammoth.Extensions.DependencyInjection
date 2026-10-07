using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class TransientDisposableDiagnosticFormattingTests
{
	[TestMethod]
	[DataRow(false, false, false)]
	[DataRow(false, false, true)]
	[DataRow(false, true, false)]
	[DataRow(false, true, true)]
	[DataRow(true, false, false)]
	[DataRow(true, false, true)]
	[DataRow(true, true, false)]
	[DataRow(true, true, true)]
	public void DiagnosticRetainsExactTextAndResolutionStack(bool keyed, bool factory, bool nested)
	{
		using var culture = new CultureScope("fr-FR");
		object? key = keyed ? "leaf" : null;
		using var provider = Build(key, factory, nested, "consumer");
		var error = ResolveError(provider, key, nested, "consumer");
		Assert.AreEqual(Expected(keyed ? "leaf" : null, factory, nested, "consumer"), error.Message);
		// Failed resolution must not leak a consumer into a later diagnostic.
		var later = ResolveError(provider, key, false, "consumer");
		Assert.AreEqual(Expected(keyed ? "leaf" : null, factory, false, "consumer"), later.Message);
		using var scope = provider.CreateScope();
		Assert.IsNotNull(ResolveLeaf(scope.ServiceProvider, key));
	}

	[TestMethod]
	[DataRow(false, false)]
	[DataRow(false, true)]
	[DataRow(true, false)]
	[DataRow(true, true)]
	public void FormattableKeysUseInvariantCultureAcrossTargets(bool factory, bool customLeaf)
	{
		using var culture = new CultureScope("fr-FR");
		object leafKey = customLeaf ? new FormattableKey() : 1234.5m;
		object consumerKey = customLeaf ? 1234.5m : new FormattableKey();
		using var provider = Build(leafKey, factory, true, consumerKey);
		var error = ResolveError(provider, leafKey, true, consumerKey);
		var expectedLeaf = customLeaf ? "custom-invariant" : "1234.5";
		var expectedConsumer = customLeaf ? "1234.5" : "custom-invariant";
		Assert.AreEqual(Expected(expectedLeaf, factory, true, expectedConsumer), error.Message);
	}

	[TestMethod]
	public void NonFormattableKeyRetainsItsOwnToStringSemantics()
	{
		using var culture = new CultureScope("fr-FR");
		var key = new CultureOnlyKey();
		using var provider = Build(key, false, false, "consumer");
		Assert.AreEqual(Expected("1234,5", false, false, "consumer"),
			ResolveError(provider, key, false, "consumer").Message);
	}

	private static ServiceProvider Build(object? key, bool factory, bool nested, object consumerKey)
	{
		IServiceCollection services = new ServiceCollection();
		if (key == null)
		{
			if (factory) services.AddTransient<ILeaf>(_ => new Leaf());
			else services.AddTransient<ILeaf, Leaf>();
		}
		else
		{
			if (factory) services.AddKeyedTransient<ILeaf>(key, (_, _) => new Leaf());
			else services.AddKeyedTransient<ILeaf, Leaf>(key);
		}
		if (nested) services.AddKeyedTransient<Consumer>(consumerKey,
			(sp, _) => new Consumer(ResolveLeaf(sp, key)));
		return ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
		{
			ValidateOnBuild = true,
			DetectIncorrectUsageOfTransientDisposables = true,
			AllowSingletonToResolveTransientDisposables = false
		});
	}

	private static InvalidOperationException ResolveError(ServiceProvider provider, object? key, bool nested, object consumerKey) =>
		Assert.ThrowsExactly<InvalidOperationException>(() =>
		{
			if (nested) provider.GetRequiredKeyedService<Consumer>(consumerKey);
			else ResolveLeaf(provider, key);
		});

	private static ILeaf ResolveLeaf(IServiceProvider provider, object? key) => key == null
		? provider.GetRequiredService<ILeaf>() : provider.GetRequiredKeyedService<ILeaf>(key);

	private static string Expected(string? key, bool factory, bool nested, string consumerKey)
	{
		var keyPart = key == null ? "" : $"ServiceKey: {key}, ";
		var message = "Trying to resolve Transient Disposable service - " + keyPart
			+ $"ServiceType: {typeof(ILeaf).FullName}, " + (factory ? "(factory) " : "")
			+ $"ImplementationType: {typeof(Leaf).FullName}." + Environment.NewLine
			+ "Requested by (Resolution Context Stack):" + Environment.NewLine
			+ "- " + keyPart + $"ServiceType: {typeof(ILeaf).FullName}";
		return nested ? message + Environment.NewLine
			+ $"- ServiceKey: {consumerKey}, ServiceType: {typeof(Consumer).FullName}" : message;
	}

	private sealed class CultureScope : IDisposable
	{
		private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
		internal CultureScope(string culture) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
		public void Dispose() => CultureInfo.CurrentCulture = _previous;
	}
	public interface ILeaf : IDisposable { }
	public sealed class Leaf : ILeaf { public void Dispose() { } }
	public sealed class Consumer(ILeaf leaf) { public ILeaf Leaf { get; } = leaf; }
	private sealed class FormattableKey : IFormattable
	{
		public string ToString(string? format, IFormatProvider? provider) =>
			provider is CultureInfo { Name: "" } ? "custom-invariant" : "custom-" + CultureInfo.CurrentCulture.Name;
		public override string ToString() => ToString(null, CultureInfo.CurrentCulture);
	}
	private sealed class CultureOnlyKey
	{
		public override string ToString() => 1234.5m.ToString(CultureInfo.CurrentCulture);
	}
}
