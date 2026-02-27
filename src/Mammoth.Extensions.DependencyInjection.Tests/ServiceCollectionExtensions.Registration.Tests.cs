using Microsoft.Extensions.DependencyInjection;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class ServiceCollectionExtensionsRegistrationTests
	{
		[TestMethod]
		public void IsTransientServiceRegistered_Returns_False_When_Service_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();

			Assert.IsFalse(serviceCollection.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(serviceCollection.IsTransientServiceRegistered(typeof(TestService)));
		}

		[TestMethod]
		public void IsScopedServiceRegistered_Returns_False_When_Service_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();

			Assert.IsFalse(serviceCollection.IsScopedServiceRegistered<TestService>());
			Assert.IsFalse(serviceCollection.IsScopedServiceRegistered(typeof(TestService)));
		}

		[TestMethod]
		public void IsSingletonServiceRegistered_Returns_False_When_Service_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();

			Assert.IsFalse(serviceCollection.IsSingletonServiceRegistered<TestService>());
			Assert.IsFalse(serviceCollection.IsSingletonServiceRegistered(typeof(TestService)));
		}

		[TestMethod]
		public void IsKeyedSingletonServiceRegistered_Returns_False_When_Service_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();

			Assert.IsFalse(serviceCollection.IsKeyedSingletonServiceRegistered<TestService>("key"));
			Assert.IsFalse(serviceCollection.IsKeyedSingletonServiceRegistered(typeof(TestService), "key"));
		}

		[TestMethod]
		public void IsKeyedScopedServiceRegistered_Returns_False_When_Service_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();

			Assert.IsFalse(serviceCollection.IsKeyedScopedServiceRegistered<TestService>("key"));
			Assert.IsFalse(serviceCollection.IsKeyedScopedServiceRegistered(typeof(TestService), "key"));
		}

		[TestMethod]
		public void IsKeyedTransientServiceRegistered_Returns_False_When_Service_Not_Registered()
		{
			var serviceCollection = new ServiceCollection();

			Assert.IsFalse(serviceCollection.IsKeyedTransientServiceRegistered<TestService>("key"));
			Assert.IsFalse(serviceCollection.IsKeyedTransientServiceRegistered(typeof(TestService), "key"));
		}

		[TestMethod]
		public void IsTransientServiceRegistered_Returns_True_When_Registered_As_Transient()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TestService>();

			Assert.IsTrue(serviceCollection.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(serviceCollection.IsScopedServiceRegistered<TestService>());
			Assert.IsFalse(serviceCollection.IsSingletonServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsScopedServiceRegistered_Returns_True_When_Registered_As_Scoped()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddScoped<TestService>();

			Assert.IsFalse(serviceCollection.IsTransientServiceRegistered<TestService>());
			Assert.IsTrue(serviceCollection.IsScopedServiceRegistered<TestService>());
			Assert.IsFalse(serviceCollection.IsSingletonServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsSingletonServiceRegistered_Returns_True_When_Registered_As_Singleton()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<TestService>();

			Assert.IsFalse(serviceCollection.IsTransientServiceRegistered<TestService>());
			Assert.IsFalse(serviceCollection.IsScopedServiceRegistered<TestService>());
			Assert.IsTrue(serviceCollection.IsSingletonServiceRegistered<TestService>());
		}

		[TestMethod]
		public void IsKeyedTransientServiceRegistered_Returns_True_When_Registered_As_Keyed_Transient()
		{
			var serviceCollection = new ServiceCollection();
			var key = Guid.NewGuid();
			serviceCollection.AddKeyedTransient<TestService>(key);

			Assert.IsTrue(serviceCollection.IsKeyedTransientServiceRegistered<TestService>(key));
			Assert.IsFalse(serviceCollection.IsKeyedScopedServiceRegistered<TestService>(key));
			Assert.IsFalse(serviceCollection.IsKeyedSingletonServiceRegistered<TestService>(key));
		}

		[TestMethod]
		public void IsKeyedScopedServiceRegistered_Returns_True_When_Registered_As_Keyed_Scoped()
		{
			var serviceCollection = new ServiceCollection();
			var key = Guid.NewGuid();
			serviceCollection.AddKeyedScoped<TestService>(key);

			Assert.IsFalse(serviceCollection.IsKeyedTransientServiceRegistered<TestService>(key));
			Assert.IsTrue(serviceCollection.IsKeyedScopedServiceRegistered<TestService>(key));
			Assert.IsFalse(serviceCollection.IsKeyedSingletonServiceRegistered<TestService>(key));
		}

		[TestMethod]
		public void IsKeyedSingletonServiceRegistered_Returns_True_When_Registered_As_Keyed_Singleton()
		{
			var serviceCollection = new ServiceCollection();
			var key = Guid.NewGuid();
			serviceCollection.AddKeyedSingleton<TestService>(key);

			Assert.IsFalse(serviceCollection.IsKeyedTransientServiceRegistered<TestService>(key));
			Assert.IsFalse(serviceCollection.IsKeyedScopedServiceRegistered<TestService>(key));
			Assert.IsTrue(serviceCollection.IsKeyedSingletonServiceRegistered<TestService>(key));
		}
	}
}
