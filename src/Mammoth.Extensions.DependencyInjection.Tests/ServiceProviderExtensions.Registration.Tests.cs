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
		}

		[TestMethod]
		public void IsServiceRegistered_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsException<InvalidOperationException>(() => sp.IsServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Generic_Throws_Because_ServiceProvider_Not_Configured()
		{
			var serviceCollection = new ServiceCollection();
			var sp = serviceCollection.BuildServiceProvider();

			Assert.ThrowsException<InvalidOperationException>(() => sp.IsServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Return_True_If_a_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Return_False_If_a_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsFalse(sp.IsServiceRegistered<AnotherTestService>());
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_Return_True_If_a_Keyed_ServiceType_was_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsTrue(sp.IsServiceRegistered("one"));
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_Return_False_If_a_Keyed_ServiceType_was_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<TestService>("one");
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			Assert.IsFalse(sp.IsServiceRegistered("two"));
		}

		[TestMethod]
		public void IsServiceRegistered_Checking_ServiceKey_for_Null_Throws_Exception()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();
			var sp = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			Assert.ThrowsException<ArgumentNullException>(() => sp.IsServiceRegistered(serviceKey: null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
		}
	}
}
