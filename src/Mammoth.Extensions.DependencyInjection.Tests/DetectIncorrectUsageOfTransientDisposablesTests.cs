using Mammoth.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	// Transient disposables objects are captured by the container and disposed (and released) when the
	// scope is disposed.
	// If such services are resolved by the root scope, they will never be released until the container is disposed
	// (this will result in a memory leak).
	// The same goes for scoped services.

	[TestClass]
	public class DetectIncorrectUsageOfTransientDisposablesTests
	{
		public class TransientDisposable : IDisposable
		{
			public void Dispose()
			{
				GC.SuppressFinalize(this);
			}
		}

		public class Consumer
		{
			public TransientDisposable TransientDisposable { get; }

			public Consumer(TransientDisposable transientDisposable)
			{
				TransientDisposable = transientDisposable;
			}
		}

		[TestMethod]
		public void ServiceProvider_IsRootScope_True()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			using var sp = serviceCollection.BuildServiceProvider();

			Assert.IsTrue(sp.GetIsRootScope());
		}

		[TestMethod]
		public void ScopeServiceProvider_IsRootScope_False()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			using var sp = serviceCollection.BuildServiceProvider();
			using var scope = sp.CreateScope();
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InRootScope_MemoryLeak()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			using var sp = serviceCollection.BuildServiceProvider();
			var transient = sp.GetService<TransientDisposable>();
			Assert.IsNotNull(transient);
			// check for memory leaks: the "transient" object will never be held by internal sp.Disables array property until the container is disposed
			Assert.IsTrue(sp.GetIsRootScope());
			var disposables = sp.GetDisposables();
			Assert.AreEqual(1, disposables.Count());
			Assert.IsTrue(disposables.Contains(transient));
		}

		[TestMethod]
		public void Resolve_Consumer_InRootScope_MemoryLeak()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			serviceCollection.AddTransient<Consumer>();
			using var sp = serviceCollection.BuildServiceProvider();
			var consumer = sp.GetService<Consumer>();
			Assert.IsNotNull(consumer);
			// check for memory leaks: the "transient" object will never be held by internal sp.Disables array property until the container is disposed
			Assert.IsTrue(sp.GetIsRootScope());
			var disposables = sp.GetDisposables();
			Assert.AreEqual(1, disposables.Count());
			Assert.IsTrue(disposables.Contains(consumer.TransientDisposable));
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InScope_NoMemoryLeak()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			using var sp = serviceCollection.BuildServiceProvider();

			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<TransientDisposable>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}

		[TestMethod]
		public void Resolve_Consumer_InScope_NoMemoryLeak()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			serviceCollection.AddTransient<Consumer>();
			using var sp = serviceCollection.BuildServiceProvider();

			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<Consumer>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer.TransientDisposable));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InRootScope_WithValidation_Throws()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.GetService<TransientDisposable>());
		}

		[TestMethod]
		public void Resolve_Consumer_InRootScope_WithValidation_Throws()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			serviceCollection.AddTransient<Consumer>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.GetService<Consumer>());
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InScope_WithValidation_NoMemoryLeak()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<TransientDisposable>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}

		[TestMethod]
		public void Resolve_Consumer_InScope_WithValidation_NoMemoryLeak()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			serviceCollection.AddTransient<Consumer>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<Consumer>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer.TransientDisposable));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}
	}
}
