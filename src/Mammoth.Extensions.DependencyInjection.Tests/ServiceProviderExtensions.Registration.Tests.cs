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

		#region Issue #19 - GetServiceDescriptors Bidirectional IsAssignableFrom Bug

		[TestMethod]
		public void Issue19_BaseTypeIsolation_Register_Object_As_Singleton_And_TestService_As_Transient_Verify_No_CrossMatch()
		{
			// Arrange: Register object as Singleton and TestService as Transient
			// With the buggy code, object.IsAssignableFrom(TestService) = true, so GetServiceDescriptors for TestService
			// would incorrectly include the object registration, causing .Last() to return the wrong descriptor
			// With the fix, we only match exact types or types the query type can be assigned from
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton(typeof(object));  // Register base type as singleton
			serviceCollection.AddTransient<TestService>();  // Register specific type as transient
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// Act & Assert
			// object singleton should be found when querying for object
			Assert.IsTrue(sp.IsSingletonServiceRegistered(typeof(object)));

			// TestService should be transient, NOT singleton (the fix prevents object from matching TestService)
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
			Assert.IsTrue(sp.IsTransientServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Issue19_InterfaceHierarchy_Register_Interface_And_Implementation_With_Different_Lifetimes()
		{
			// Arrange: Register ITestService as Singleton and TestService as Transient
			// This tests that interface and implementation registrations are correctly distinguished
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton(typeof(ITestService), typeof(TestService));  // ITestService -> TestService as Singleton
			serviceCollection.AddTransient<TestService>();  // Direct TestService as Transient
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// Act & Assert
			// ITestService should be singleton (first registration)
			Assert.IsTrue(sp.IsSingletonServiceRegistered(typeof(ITestService)));

			// TestService direct registration should be transient (last registration)
			Assert.IsTrue(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Issue19_LastDescriptorSelection_Multiple_Registrations_Same_Type_Last_Wins()
		{
			// Arrange: Register TestService three times with different lifetimes, last one should determine the checks
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			serviceCollection.AddScoped<TestService>();
			serviceCollection.AddTransient<TestService>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// Act
			var descriptors = serviceCollection.GetServiceDescriptors(typeof(TestService));

			// Assert - all three registrations should be present
			Assert.AreEqual(3, descriptors.Length);

			// The last one is transient
			Assert.AreEqual(ServiceLifetime.Transient, descriptors.Last().Lifetime);

			// When checking, the last registration wins
			Assert.IsTrue(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsScopedServiceRegistered<TestService>());  // Last is not scoped
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());  // Last is not singleton
		}

		[TestMethod]
		public void Issue19_UnrelatedTypes_Do_Not_Cross_Match_Even_With_Interface()
		{
			// Arrange: Both TestService and AnotherTestService implement IAnotherInterface
			// The bug could cause them to cross-match due to bidirectional IsAssignableFrom
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton(typeof(IAnotherInterface), typeof(AnotherTestService));
			serviceCollection.AddTransient<TestService>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// Act & Assert
			// Querying for TestService should NOT return the IAnotherInterface registration
			Assert.IsTrue(sp.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(sp.IsSingletonServiceRegistered<TestService>());

			// Querying for IAnotherInterface should return the singleton
			Assert.IsTrue(sp.IsSingletonServiceRegistered(typeof(IAnotherInterface)));
		}

		[TestMethod]
		public void Issue19_Exact_Type_Match_Only()
		{
			// Arrange: Register TestService as singleton
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// Act
			var descriptors = serviceCollection.GetServiceDescriptors(typeof(TestService));

			// Assert - should find exact match only
			Assert.AreEqual(1, descriptors.Length);
			Assert.AreEqual(typeof(TestService), descriptors[0].ServiceType);
			Assert.AreEqual(ServiceLifetime.Singleton, descriptors[0].Lifetime);

			// Should be registered as singleton
			Assert.IsTrue(sp.IsSingletonServiceRegistered<TestService>());
		}

		[TestMethod]
		public void Issue19_Implementation_Type_Matches_Interface_Registration()
		{
			// Arrange: Register interface with implementation and also register implementation directly
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<ITestService, TestService>();  // Register interface->implementation as singleton
			serviceCollection.AddTransient<TestService>();  // Register implementation directly as transient
			using var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// Act & Assert
			// ITestService should be resolvable as singleton from the first registration
			Assert.IsTrue(sp.IsSingletonServiceRegistered(typeof(ITestService)));

			// TestService direct registration should be transient
			Assert.IsTrue(sp.IsTransientServiceRegistered<TestService>());

			// The bug would have caused bidirectional matching to mix these up
			// With the fix, they are properly isolated
		}

		#endregion
	}
}

#pragma warning restore CA2263 // Prefer generic overload
#pragma warning restore IDE0079 // Remove unnecessary suppression