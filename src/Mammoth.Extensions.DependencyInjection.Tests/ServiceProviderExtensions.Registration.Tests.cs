using Microsoft.Extensions.DependencyInjection;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class ServiceProviderExtensionsRegistrationTests
	{
		[TestMethod]
		public void IsServiceRegistered_KeyedService_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsException<InvalidOperationException>(() => sp.IsServiceRegistered("service"));
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsKeyedSingletonServiceRegistered<TestService>("service"));
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsKeyedTransientServiceRegistered<TestService>("service"));
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsKeyedScopedServiceRegistered<TestService>("service"));
		}

		[TestMethod]
		public void IsServiceRegistered_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsException<InvalidOperationException>(() => sp.IsServiceRegistered(typeof(TestService)));
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsSingletonServiceRegistered(typeof(TestService)));
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsTransientServiceRegistered(typeof(TestService)));
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsScopedServiceRegistered(typeof(TestService)));
		}

		[TestMethod]
		public void IsServiceRegistered_Generic_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsException<InvalidOperationException>(() => sp.IsServiceRegistered<TestService>());
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsSingletonServiceRegistered<TestService>());
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsTransientServiceRegistered<TestService>());
			Assert.ThrowsException<InvalidOperationException>(() => sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Return_False_If_a_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsFalse(sp.IsServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_Return_False_If_a_Keyed_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			serviceCollection.AddKeyedTransient<TestService>("one");
			serviceCollection.AddKeyedScoped<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsFalse(sp.IsServiceRegistered("two"));
			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("two"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("two"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("two"));
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_for_Null_Throws_Exception()
		{
			var serviceCollection = new ServiceCollection();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			Assert.ThrowsException<ArgumentNullException>(() => sp.IsServiceRegistered(serviceKey: null));
			Assert.ThrowsException<ArgumentNullException>(() => sp.IsKeyedSingletonServiceRegistered<TestService>(serviceKey: null));
			Assert.ThrowsException<ArgumentNullException>(() => sp.IsKeyedTransientServiceRegistered<TestService>(serviceKey: null));
			Assert.ThrowsException<ArgumentNullException>(() => sp.IsKeyedScopedServiceRegistered<TestService>(serviceKey: null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
		}

		[TestMethod]
		public void Singleton_IsServiceRegistered_Return_True_If_a_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
			Assert.IsTrue(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Singleton_IsServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsTrue(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Singleton_IsServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered("one"));

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsTrue(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Scoped_IsServiceRegistered_Return_True_If_a_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddScoped<TestService>();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsTrue(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Scoped_IsServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedScoped<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Scoped_IsServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedScoped<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered("one"));

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Transient_IsServiceRegistered_Return_True_If_a_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TestService>();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsTrue(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Transient_IsServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedTransient<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Transient_IsServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedTransient<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered("one"));

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}
	}
}
