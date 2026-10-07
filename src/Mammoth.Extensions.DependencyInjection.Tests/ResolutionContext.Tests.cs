using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ResolutionContextTests
{
	private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);
	private const string Key = "context";

	[TestMethod]
	[DataRow(false, false, false)]
	[DataRow(false, false, true)]
	[DataRow(false, true, false)]
	[DataRow(false, true, true)]
	[DataRow(true, false, false)]
	[DataRow(true, false, true)]
	[DataRow(true, true, false)]
	[DataRow(true, true, true)]
	public async Task ChildSingletonDoesNotExemptParentOrSibling(bool keyed, bool factory, bool sibling)
	{
		using var entered = new ManualResetEventSlim();
		using var release = new ManualResetEventSlim();
		var services = CreateServices(keyed, factory);
		Register<Singleton>(services, keyed, ServiceLifetime.Singleton, _ =>
		{
			entered.Set();
			Wait(release);
			return new Singleton();
		});
		using var provider = Build(services);
		// Ensure the old mutable stack exists in the context inherited by both branches.
		provider.GetRequiredService<Warmup>();
		AssertRejected(provider, keyed);
		var pending = Task.Run(() => Resolve<Singleton>(provider, keyed));
		try
		{
			Wait(entered);
			if (sibling) await Task.Run(() => AssertRejected(provider, keyed));
			else AssertRejected(provider, keyed);
		}
		finally
		{
			release.Set();
			await pending;
		}
		AssertRejected(provider, keyed);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task ChildKeepsInheritedAncestryAfterOriginatingResolutionReturns(bool keyed)
	{
		using var release = new ManualResetEventSlim();
		Task<Resource>? pending = null;
		var services = CreateServices(keyed, factory: false);
		Register<Singleton>(services, keyed, ServiceLifetime.Singleton, sp =>
		{
			pending = Task.Run(() =>
			{
				Wait(release);
				return Resolve<Resource>(sp, keyed);
			});
			return new Singleton();
		});
		using var provider = Build(services);
		Resolve<Singleton>(provider, keyed);
		try
		{
			// Restoring the parent's context must neither keep its singleton nor erase
			// the snapshot inherited by the child when the task was started.
			AssertRejected(provider, keyed);
		}
		finally
		{
			release.Set();
			Assert.IsNotNull(pending);
			Assert.IsNotNull(await pending);
		}
		AssertRejected(provider, keyed);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task ParentRestorationDoesNotRemoveAnActiveChildFrame(bool keyed)
	{
		using var entered = new ManualResetEventSlim();
		using var release = new ManualResetEventSlim();
		Task<InvalidOperationException>? pending = null;
		var services = CreateServices(keyed, factory: false);
		Register<BlockedChild>(services, keyed, ServiceLifetime.Transient, sp =>
		{
			entered.Set();
			Wait(release);
			Resolve<Resource>(sp, keyed);
			return new BlockedChild();
		});
		Register<Origin>(services, keyed, ServiceLifetime.Transient, sp =>
		{
			pending = Task.Run(() => Assert.ThrowsExactly<InvalidOperationException>(
				() => Resolve<BlockedChild>(sp, keyed)));
			Wait(entered);
			return new Origin();
		});
		using var provider = Build(services);
		try
		{
			Resolve<Origin>(provider, keyed);
			AssertRejected(provider, keyed);
		}
		finally
		{
			release.Set();
			if (pending != null)
			{
				var error = await pending;
				var stack = error.Message.Substring(error.Message.IndexOf("Requested by", StringComparison.Ordinal));
				var keyPart = keyed ? $"ServiceKey: {Key}, " : "";
				Assert.AreEqual("Requested by (Resolution Context Stack):" + Environment.NewLine
					+ $"- {keyPart}ServiceType: {typeof(Resource).FullName}" + Environment.NewLine
					+ $"- {keyPart}ServiceType: {typeof(BlockedChild).FullName}" + Environment.NewLine
					+ $"- {keyPart}ServiceType: {typeof(Origin).FullName}", stack);
			}
		}
		AssertRejected(provider, keyed);
	}

	[TestMethod]
	[DataRow(false, false)]
	[DataRow(false, true)]
	[DataRow(true, false)]
	[DataRow(true, true)]
	public async Task NestedChildAndExceptionRestoreTheirOwnAncestry(bool keyed, bool factory)
	{
		var services = CreateServices(keyed, factory);
		Register<Failure>(services, keyed, ServiceLifetime.Transient,
			_ => throw new ApplicationException("Expected factory failure"));
		Register<Nested>(services, keyed, ServiceLifetime.Transient, sp =>
		{
			Assert.ThrowsExactly<ApplicationException>(() => Resolve<Failure>(sp, keyed));
			return new Nested(Resolve<Resource>(sp, keyed));
		});
		Register<Singleton>(services, keyed, ServiceLifetime.Singleton, sp =>
		{
			// Flowed children retain the legitimate singleton ancestor, including
			// after a nested failure, without removing it from their parent.
			Task.Run(() => Resolve<Nested>(sp, keyed)).GetAwaiter().GetResult();
			Assert.IsNotNull(Resolve<Nested>(sp, keyed).Resource);
			return new Singleton();
		});
		using var provider = Build(services);
		await Task.Run(() => Resolve<Singleton>(provider, keyed));
		AssertRejected(provider, keyed);
		var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve<Nested>(provider, keyed));
		StringAssert.Contains(error.Message, typeof(Nested).FullName!);
		Assert.IsFalse(error.Message.Contains(typeof(Failure).FullName!));
		Assert.IsFalse(error.Message.Contains(typeof(Singleton).FullName!));
		AssertRejected(provider, keyed);
	}

	private static ServiceCollection CreateServices(bool keyed, bool factory)
	{
		var services = new ServiceCollection();
		services.AddTransient<Warmup>();
		if (factory) Register<Resource>(services, keyed, ServiceLifetime.Transient, _ => new Resource());
		else if (keyed) services.AddKeyedTransient<Resource>(Key);
		else services.AddTransient<Resource>();
		return services;
	}

	private static void Register<T>(IServiceCollection services, bool keyed, ServiceLifetime lifetime,
		Func<IServiceProvider, T> factory) where T : class
	{
		services.Add(keyed
			? ServiceDescriptor.DescribeKeyed(typeof(T), Key, (sp, _) => factory(sp), lifetime)
			: ServiceDescriptor.Describe(typeof(T), sp => factory(sp), lifetime));
	}

	private static ServiceProvider Build(IServiceCollection services) =>
		ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
		{
			ValidateOnBuild = true,
			DetectIncorrectUsageOfTransientDisposables = true,
			AllowSingletonToResolveTransientDisposables = true
		});

	private static T Resolve<T>(IServiceProvider provider, bool keyed) where T : notnull => keyed
		? provider.GetRequiredKeyedService<T>(Key) : provider.GetRequiredService<T>();

	private static void AssertRejected(IServiceProvider provider, bool keyed)
	{
		var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve<Resource>(provider, keyed));
		StringAssert.Contains(error.Message, typeof(Resource).FullName!);
		Assert.IsFalse(error.Message.Contains(typeof(Singleton).FullName!));
		Assert.IsFalse(error.Message.Contains(typeof(Nested).FullName!));
		Assert.IsFalse(error.Message.Contains(typeof(Origin).FullName!));
		Assert.IsFalse(error.Message.Contains(typeof(BlockedChild).FullName!));
		Assert.AreEqual(0, ResolutionContext.CurrentStack.Count());
	}

	private static void Wait(ManualResetEventSlim gate) =>
		Assert.IsTrue(gate.Wait(GateTimeout), "Timed out waiting for deterministic resolution gate.");

	public sealed class Origin { }
	public sealed class BlockedChild { }
	public sealed class Warmup { }
	public sealed class Singleton { }
	public sealed class Failure { }
	public sealed class Nested(Resource resource) { public Resource Resource { get; } = resource; }
	public sealed class Resource : IDisposable { public void Dispose() { } }
}
