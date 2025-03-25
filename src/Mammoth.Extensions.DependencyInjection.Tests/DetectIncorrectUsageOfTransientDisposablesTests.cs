using Mammoth.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

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

		public class ScopedDisposable : IDisposable
		{
			public void Dispose()
			{
				GC.SuppressFinalize(this);
			}
		}

		/// <summary>
		/// Consumer object that depends on a transient disposable object.
		/// </summary>
		public class Consumer
		{
			public TransientDisposable TransientDisposable { get; }

			public Consumer(TransientDisposable transientDisposable)
			{
				TransientDisposable = transientDisposable;
			}
		}

		public class DisposableConsumer : IDisposable
		{
			public TransientDisposable TransientDisposable { get; }

			public DisposableConsumer(TransientDisposable transientDisposable)
			{
				TransientDisposable = transientDisposable;
			}

			public void Dispose()
			{
				GC.SuppressFinalize(this);
			}
		}

		public class DisposableConsumerFactory
		{
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
			public DisposableConsumer Build(IServiceProvider sp)
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression
			{
				return new DisposableConsumer(sp.GetRequiredService<TransientDisposable>());
			}
		}

		/// <summary>
		/// Singleton object that depends on a transient disposable object.
		/// </summary>
		public class SingletonWithTransient
		{
			public TransientDisposable TransientDisposable { get; }

			public SingletonWithTransient(TransientDisposable transientDisposable)
			{
				TransientDisposable = transientDisposable;
			}
		}

		/// <summary>
		/// Singleton object that depends on a transient disposable object.
		/// </summary>
		public class SingletonWithScoped
		{
			public ScopedDisposable TransientDisposable { get; }

			public SingletonWithScoped(ScopedDisposable transientDisposable)
			{
				TransientDisposable = transientDisposable;
			}
		}

		public class ThirdLevel
		{
			public ThirdLevel(SingletonWithTransient singletonWithTransient)
			{
				SingletonWithTransient = singletonWithTransient;
			}

			public SingletonWithTransient SingletonWithTransient { get; }
		}

		public interface ITransientOpenGeneric<T> : IDisposable;

		public class TransientOpenGeneric<T> : ITransientOpenGeneric<T>
		{
			public void Dispose()
			{
				GC.SuppressFinalize(this);
			}
		}

		private static ServiceCollection CreateServiceCollection()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TransientDisposable>();
			serviceCollection.AddScoped<ScopedDisposable>();
			serviceCollection.AddTransient<Consumer>();
			serviceCollection.AddSingleton<DisposableConsumerFactory>();
			serviceCollection.AddTransient(sp => sp.GetRequiredService<DisposableConsumerFactory>().Build(sp));
			serviceCollection.AddSingleton<SingletonWithTransient>();
			// serviceCollection.AddSingleton<SingletonWithScoped>(); // with ValidateScopes = true will raise exceptions upon Service Provider creation.
			serviceCollection.AddTransient<ThirdLevel>();
			serviceCollection.AddTransient(typeof(ITransientOpenGeneric<>), typeof(TransientOpenGeneric<>));
			return serviceCollection;
		}

		private static ServiceProvider BuildServiceProvider(ServiceCollection serviceCollection)
		{
			return serviceCollection.BuildServiceProvider(new ServiceProviderOptions
			{
				ValidateOnBuild = true,
				ValidateScopes = true
			});
		}

		[TestMethod]
		public void ServiceProvider_IsRootScope_True()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			Assert.IsTrue(sp.GetIsRootScope());
		}

		[TestMethod]
		public void ScopeServiceProvider_IsRootScope_False()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			using var scope = sp.CreateScope();
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InRootScope_MemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			var transient = sp.GetService<TransientDisposable>();
			Assert.IsNotNull(transient);
			// check for memory leaks: the "transient" object will never be held by internal sp.Disables array property until the container is disposed
			Assert.IsTrue(sp.GetIsRootScope());
			var disposables = sp.GetDisposables();
			Assert.AreEqual(1, disposables.Count());
			Assert.IsTrue(disposables.Contains(transient));
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InScope_NoMemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<TransientDisposable>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InRootScope_WithValidation_Throws()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.GetService<TransientDisposable>());
		}

		/// <summary>
		/// We can allow some services to be excluded from the detection of incorrect usage of transient disposables.
		/// Some AspNetCore services are registered as transient disposables, they can be resolved from the root scope.
		/// They are managed by the framework, we trust it.
		/// </summary>
		[TestMethod]
		public void Resolve_TransientDisposable_InRootScope_WithValidation_ExclusionPatterns_DoesNotThrow()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					DetectIncorrectUsageOfTransientDisposablesExclusionPatterns = ["TransientDisposable"],
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			var transient = sp.GetService<TransientDisposable>();
			Assert.IsNotNull(transient);
			// check for memory leaks: the "transient" object will never be held by internal sp.Disables array property until the container is disposed
			Assert.IsTrue(sp.GetIsRootScope());
			var disposables = sp.GetDisposables();
			Assert.AreEqual(1, disposables.Count());
			Assert.IsTrue(disposables.Contains(transient));
		}

		[TestMethod]
		public void Resolve_TransientDisposable_InScope_WithValidation_NoMemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
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
		public void Resolve_Consumer_InRootScope_MemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			var consumer = sp.GetService<Consumer>();
			Assert.IsNotNull(consumer);
			// check for memory leaks: the "transient" object will never be held by internal sp.Disables array property until the container is disposed
			Assert.IsTrue(sp.GetIsRootScope());
			var disposables = sp.GetDisposables();
			Assert.AreEqual(1, disposables.Count());
			Assert.IsTrue(disposables.Contains(consumer.TransientDisposable));
		}

		[TestMethod]
		public void Resolve_Consumer_InScope_NoMemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<Consumer>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer.TransientDisposable));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}

		[TestMethod]
		public void Resolve_Consumer_InRootScope_WithValidation_Throws()
		{
			var serviceCollection = CreateServiceCollection();
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
		public void Resolve_Consumer_InScope_WithValidation_NoMemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
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

		[TestMethod]
		public void Resolve_DisposableConsumer_using_factory_InRootScope_WithValidation_Throws()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.GetService<DisposableConsumer>());
		}

		[TestMethod]
		public void Resolve_DisposableConsumer_using_factory_InRootScope_WithValidation_NoMemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			// resolving in scope works!
			using var scope = sp.CreateScope();
			var disposableConsumer = scope.ServiceProvider.GetService<DisposableConsumer>();

			Assert.IsNotNull(disposableConsumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(disposableConsumer));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(disposableConsumer));
		}

		/// <summary>
		/// A singleton that depends on a transient Disposable object should not throw a
		/// "detect incorrect usage of transient disposables" exception.
		/// Because that transient Disposable object is captured by the ServiceProvider and disposed (and released) when the
		/// container is disposed.
		/// </summary>
		[TestMethod]
		public void Resolve_Singleton_with_Transient_Dependency_InRootScope_MemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			var s1 = sp.GetService<SingletonWithTransient>();
			Assert.IsNotNull(s1);
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsTrue(sp.GetDisposables().Contains(s1.TransientDisposable));
		}

		/// <summary>
		/// A singleton that depends on a transient Disposable object should not throw a
		/// "detect incorrect usage of transient disposables" exception.
		/// Because that transient Disposable object is captured by the ServiceProvider and disposed (and released) when the
		/// container is disposed.
		/// </summary>
		[TestMethod]
		public void Resolve_Singleton_with_Transient_Dependency_InRootScope_WithValidation_DoNotAllowTransient_Throws()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					AllowSingletonToResolveTransientDisposables = false,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.GetService<SingletonWithTransient>());
		}

		/// <summary>
		/// A singleton that depends on a transient Disposable object should not throw a
		/// "detect incorrect usage of transient disposables" exception.
		/// Because that transient Disposable object is captured by the ServiceProvider and disposed (and released) when the
		/// container is disposed.
		/// </summary>
		[TestMethod]
		public void Resolve_Singleton_with_Transient_Dependency_InRootScope_WithValidation_AllowTransient_DoesNotThrows()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					AllowSingletonToResolveTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			var s1 = sp.GetService<SingletonWithTransient>();
			Assert.IsNotNull(s1);

			using var scope = sp.CreateScope();
			var s2 = scope.ServiceProvider.GetService<SingletonWithTransient>();

			Assert.IsNotNull(s2);
			Assert.AreEqual(s1, s2);

			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsFalse(scope.ServiceProvider.GetDisposables().Contains(s2));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsTrue(sp.GetDisposables().Contains(s2.TransientDisposable));
		}

		/// <summary>
		/// A singleton that depends on a transient Disposable object should not throw a
		/// "detect incorrect usage of transient disposables" exception.
		/// Because that transient Disposable object is captured by the ServiceProvider and disposed (and released) when the
		/// container is disposed.
		/// </summary>
		[TestMethod]
		public void Resolve_Singleton_with_Transient_Dependency_InRootScope_WithValidation_AllowTransient_DoesNotThrows2()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					AllowSingletonToResolveTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			var s1 = sp.GetService<ThirdLevel>();
			Assert.IsNotNull(s1);

			using var scope = sp.CreateScope();
			var s2 = scope.ServiceProvider.GetService<ThirdLevel>();

			Assert.IsNotNull(s2);
			Assert.AreEqual(s1.SingletonWithTransient, s2.SingletonWithTransient);
			Assert.AreEqual(s1.SingletonWithTransient.TransientDisposable, s2.SingletonWithTransient.TransientDisposable);

			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.AreEqual(0, scope.ServiceProvider.GetDisposables().Count());
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsTrue(sp.GetDisposables().Contains(s1.SingletonWithTransient.TransientDisposable));
		}

		[TestMethod]
		public void Resolve_Singleton_with_Transient_Dependency_InRootScope_WithValidation_AllowTransient_DoesNotThrows3()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedTransient<TransientDisposable>("one");
			serviceCollection.AddKeyedSingleton<SingletonWithTransient>("two", dependsOn: [
				Parameter.ForKey("transientDisposable").Eq("one")
				]);
			serviceCollection.AddKeyedTransient<ThirdLevel>("tree", dependsOn: [
				Parameter.ForKey("singletonWithTransient").Eq("two")
				]);

			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					AllowSingletonToResolveTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			var s1 = sp.GetKeyedService<ThirdLevel>("tree");
			Assert.IsNotNull(s1);

			using var scope = sp.CreateScope();
			var s2 = scope.ServiceProvider.GetKeyedService<ThirdLevel>("tree");

			Assert.IsNotNull(s2);
			Assert.AreEqual(s1.SingletonWithTransient, s2.SingletonWithTransient);
			Assert.AreEqual(s1.SingletonWithTransient.TransientDisposable, s2.SingletonWithTransient.TransientDisposable);

			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.AreEqual(0, scope.ServiceProvider.GetDisposables().Count());
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsTrue(sp.GetDisposables().Contains(s1.SingletonWithTransient.TransientDisposable));
		}

		[TestMethod]
		public void Resolve_OpenGeneric_TransientDisposable_InRootScope_MemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			var transient = sp.GetService<ITransientOpenGeneric<string>>();
			Assert.IsNotNull(transient);
			// check for memory leaks: the "transient" object will never be held by internal sp.Disables array property until the container is disposed
			Assert.IsTrue(sp.GetIsRootScope());
			var disposables = sp.GetDisposables();
			Assert.AreEqual(1, disposables.Count());
			Assert.IsTrue(disposables.Contains(transient));
		}

		[TestMethod]
		public void Resolve_OpenGeneric_TransientDisposable_InScope_NoMemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = BuildServiceProvider(serviceCollection);

			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<ITransientOpenGeneric<string>>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}

		[TestMethod]
		public void Resolve_OpenGeneric_TransientDisposable_InRootScope_WithValidation_DoesNotThrows()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			var transient = sp.GetService<ITransientOpenGeneric<string>>();
			// it does not throw!!! we have a memory leak
			Assert.IsNotNull(transient);
			// check for memory leaks: the "transient" object will never be held by internal sp.Disables array property until the container is disposed
			Assert.IsTrue(sp.GetIsRootScope());
			var disposables = sp.GetDisposables();
			Assert.AreEqual(1, disposables.Count());
			Assert.IsTrue(disposables.Contains(transient));
		}

		[TestMethod]
		public void Resolve_OpenGeneric_TransientDisposable_InScope_WithValidation_NoMemoryLeak()
		{
			var serviceCollection = CreateServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			using var scope = sp.CreateScope();
			var consumer = scope.ServiceProvider.GetService<ITransientOpenGeneric<string>>();
			Assert.IsNotNull(consumer);
			Assert.IsFalse(scope.ServiceProvider.GetIsRootScope());
			Assert.IsTrue(scope.ServiceProvider.GetDisposables().Contains(consumer));
			Assert.IsTrue(sp.GetIsRootScope());
			Assert.IsFalse(sp.GetDisposables().Contains(consumer));
		}

		[TestMethod]
		public void Register_OpenGeneric_TransientDisposable_WithValidation_Throws()
		{
			var serviceCollection = CreateServiceCollection();
			Assert.ThrowsExactly<InvalidOperationException>(() =>
			{
				using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
					new ExtendedServiceProviderOptions
					{
						DetectIncorrectUsageOfTransientDisposables = true,
						ThrowOnOpenGenericTransientDisposable = true,
						ValidateOnBuild = true,
						ValidateScopes = true
					});
			});
		}

		[TestMethod]
		public void Register_OpenGeneric_TransientDisposable_WithoutValidation_LogsError()
		{
			var serviceCollection = CreateServiceCollection();
			// register a fake logger
			var fakeLogger = new FakeLogger<ServiceProviderFactory>();
			serviceCollection.AddSingleton<ILogger<ServiceProviderFactory>>(fakeLogger);
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection,
				new ExtendedServiceProviderOptions
				{
					DetectIncorrectUsageOfTransientDisposables = true,
					ThrowOnOpenGenericTransientDisposable = false,
					ValidateOnBuild = true,
					ValidateScopes = true
				});
			Assert.AreEqual(1, fakeLogger.Collector.Count);
			Assert.AreEqual(LogLevel.Warning, fakeLogger.LatestRecord.Level);
			Assert.AreEqual("Open generic transient disposable registration detected, ServiceKey: (null), ServiceType: Mammoth.Extensions.DependencyInjection.Tests.DetectIncorrectUsageOfTransientDisposablesTests+ITransientOpenGeneric`1[T], ImplementationType: Mammoth.Extensions.DependencyInjection.Tests.DetectIncorrectUsageOfTransientDisposablesTests+TransientOpenGeneric`1[T]", fakeLogger.LatestRecord.Message);
		}
	}
}
