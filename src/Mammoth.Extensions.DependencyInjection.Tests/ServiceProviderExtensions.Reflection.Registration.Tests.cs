using Microsoft.Extensions.DependencyInjection;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	// Tests disabled because they were using Extension methods using reflection

	[TestClass]
	public class ServiceProviderExtensionsTests
	{
		[TestMethod]
		public void IsServiceRegistered_Return_True_If_a_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = serviceCollection.BuildServiceProvider();

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Return_False_If_a_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.IsFalse(sp.IsServiceRegistered<AnotherTestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = serviceCollection.BuildServiceProvider();

			Assert.IsTrue(sp.IsServiceRegistered("one"));
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_Return_False_If_a_Keyed_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = serviceCollection.BuildServiceProvider();

			Assert.IsFalse(sp.IsServiceRegistered("two"));
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_for_Null_Throws_Exception()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsException<ArgumentNullException>(() => sp.IsServiceRegistered(serviceKey: null));
		}

		/// <summary>
		/// Register Keyed and non-keyed services and resolve them all.
		/// </summary>
		[TestMethod]
		public void Cannot_Resolve_All_NonKeyed_and_KeyedServices_at_the_same_time_with_framework_methods_KeyedService_AnyKey()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			// resolve all non-keyed services
			var services = serviceProvider.GetServices<ITestService>();
			Assert.AreEqual(1, services.Count());
			Assert.IsInstanceOfType(services.First(), typeof(AnotherTestService));

			// passing "null" as the key should return all non-keyed services
			var keyedServices = serviceProvider.GetKeyedServices<ITestService>(null);
			Assert.AreEqual(1, keyedServices.Count());
			Assert.IsInstanceOfType(keyedServices.First(), typeof(AnotherTestService));

			// resolve all keyed services
			keyedServices = serviceProvider.GetKeyedServices<ITestService>("one");
			Assert.AreEqual(2, keyedServices.Count());
			Assert.IsInstanceOfType(keyedServices.First(), typeof(TestService));
			Assert.IsInstanceOfType(keyedServices.Last(), typeof(AnotherTestService));

			keyedServices = serviceProvider.GetKeyedServices<ITestService>(KeyedService.AnyKey);
			Assert.AreEqual(0, keyedServices.Count()); // this is not an expected behavior, I expected it to return all services
		}

		[TestMethod]
		public void GetAllServices_Keyed_and_NonKeyed_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var services = serviceProvider.GetAllServices(typeof(ITestService));
			Assert.AreEqual(3, services.Count());
			Assert.IsInstanceOfType(services.First(), typeof(TestService));
			Assert.IsInstanceOfType(services.ElementAt(1), typeof(AnotherTestService));
			Assert.IsInstanceOfType(services.Last(), typeof(AnotherTestService));
		}

		[TestMethod]
		public void GetAllServices_Keyed_and_NonKeyed_Generic_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var services = serviceProvider.GetAllServices<ITestService>();
			Assert.AreEqual(3, services.Count());
			Assert.IsInstanceOfType(services.First(), typeof(TestService));
			Assert.IsInstanceOfType(services.ElementAt(1), typeof(AnotherTestService));
			Assert.IsInstanceOfType(services.Last(), typeof(AnotherTestService));
		}

		[TestMethod]
		public void GetAllServices_Returns_Empty_If_No_Service_Matches_Generic_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var services = serviceProvider.GetAllServices<IAnotherInterface>();
			Assert.AreEqual(0, services.Count());
		}
	}
}
