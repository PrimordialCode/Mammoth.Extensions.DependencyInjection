#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2263 // Prefer generic overload

using Microsoft.Extensions.DependencyInjection;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class ServiceProviderExtensionsRegistrationTests
	{
		[TestMethod]
		public void IsServiceRegistered_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			using var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsServiceRegistered(typeof(TestService)));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsSingletonServiceRegistered(typeof(TestService)));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsTransientServiceRegistered(typeof(TestService)));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsScopedServiceRegistered(typeof(TestService)));
		}

		[TestMethod]
		public void IsServiceRegistered_Generic_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			using var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsServiceRegistered<TestService>());
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsSingletonServiceRegistered<TestService>());
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsTransientServiceRegistered<TestService>());
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsKeyedServiceRegistered_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			using var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedServiceRegistered("service"));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedSingletonServiceRegistered(typeof(TestService), "service"));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedTransientServiceRegistered(typeof(TestService), "service"));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedScopedServiceRegistered(typeof(TestService), "service"));
		}

		[TestMethod]
		public void IsKeyedServiceRegistered_Generic_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			using var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedServiceRegistered("service"));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedSingletonServiceRegistered<TestService>("service"));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedTransientServiceRegistered<TestService>("service"));
			Assert.ThrowsExactly<InvalidOperationException>(() => sp.IsKeyedScopedServiceRegistered<TestService>("service"));
		}

		[TestMethod]
		public void IsServiceRegistered_Return_False_If_a_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsFalse(sp.IsServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsKeyedServiceRegistered_Return_False_If_a_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsFalse(sp.IsKeyedServiceRegistered("one"));
			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void IsKeyedServiceRegistered_Checking_ServiceKey_Return_False_If_a_Keyed_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			serviceCollection.AddKeyedTransient<TestService>("one");
			serviceCollection.AddKeyedScoped<TestService>("one");
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsFalse(sp.IsKeyedServiceRegistered("two"));
			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("two"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("two"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("two"));
		}

		[TestMethod]
		public void IsKeyedServiceRegistered_Checking_ServiceKey_for_Null_Throws_Exception()
		{
			var serviceCollection = new ServiceCollection();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			Assert.ThrowsExactly<ArgumentNullException>(() => sp.IsKeyedServiceRegistered(serviceKey: null));
			Assert.ThrowsExactly<ArgumentNullException>(() => sp.IsKeyedSingletonServiceRegistered<TestService>(serviceKey: null));
			Assert.ThrowsExactly<ArgumentNullException>(() => sp.IsKeyedTransientServiceRegistered<TestService>(serviceKey: null));
			Assert.ThrowsExactly<ArgumentNullException>(() => sp.IsKeyedScopedServiceRegistered<TestService>(serviceKey: null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
		}

		[TestMethod]
		public void Singleton_IsServiceRegistered_Return_True_If_a_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
			Assert.IsTrue(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Singleton_IsKeyedServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsTrue(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Singleton_IsKeyedServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsKeyedServiceRegistered("one"));

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
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsTrue(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Scoped_IsKeyedServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedScoped<TestService>("one");
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Scoped_IsKeyedServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedScoped<TestService>("one");
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsKeyedServiceRegistered("one"));

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
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsTrue(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Transient_IsKeyedServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedTransient<TestService>("one");
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Transient_IsKeyedServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedTransient<TestService>("one");
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsKeyedServiceRegistered("one"));

			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());

			Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsFalse(sp.IsKeyedScopedServiceRegistered<TestService>("one"));
		}

		[TestMethod]
		public void Can_Mix_Object_And_String_Keys()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedTransient<TestService>("one");
			var objectKey = new object();
			serviceCollection.AddKeyedTransient<TestService>(objectKey);
			// does not throw an exception
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsKeyedServiceRegistered("one"));
			Assert.IsTrue(sp.IsKeyedTransientServiceRegistered<TestService>("one"));
			Assert.IsTrue(sp.IsKeyedServiceRegistered(objectKey));
			Assert.IsTrue(sp.IsKeyedTransientServiceRegistered<TestService>(objectKey));
		}
	}
}

#pragma warning restore CA2263 // Prefer generic overload
#pragma warning restore IDE0079 // Remove unnecessary suppression