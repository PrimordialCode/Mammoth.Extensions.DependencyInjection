using Microsoft.Extensions.DependencyInjection;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class ServiceProviderExtensionsResolutionTests
	{
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
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// resolve all non-keyed services
			var services = serviceProvider.GetServices<ITestService>();
			Assert.AreEqual(1, services.Count());
			Assert.IsInstanceOfType<AnotherTestService>(services.First());

			// passing "null" as the key should return all non-keyed services
			var keyedServices = serviceProvider.GetKeyedServices<ITestService>(null);
			Assert.AreEqual(1, keyedServices.Count());
			Assert.IsInstanceOfType<AnotherTestService>(keyedServices.First());

			// resolve all keyed services
			keyedServices = serviceProvider.GetKeyedServices<ITestService>("one");
			Assert.AreEqual(2, keyedServices.Count());
			Assert.IsInstanceOfType<TestService>(keyedServices.First());
			Assert.IsInstanceOfType<AnotherTestService>(keyedServices.Last());

			keyedServices = serviceProvider.GetKeyedServices<ITestService>(KeyedService.AnyKey);
			Assert.AreEqual(0, keyedServices.Count()); // this is not an expected behavior, I expected it to return all services
		}

		/// <summary>
		/// Register Keyed and non-keyed services and resolve a service that has an IEnumerable<T> dependency.
		/// </summary>
		[TestMethod]
		public void Cannot_Resolve_All_NonKeyed_and_KeyedServices_as_IEnumerable_dependency()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			serviceCollection.AddTransient<ItHasIEnumerableDependency>();
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// resolve service that has IEnumerable<T> dependency
			var service = serviceProvider.GetService<ItHasIEnumerableDependency>();
			// framework only resolves non-keyed services
			Assert.IsNotNull(service);
			Assert.AreEqual(1, service.Services.Count());
			Assert.IsInstanceOfType<AnotherTestService>(service.Services.First());
			// you need to register with a resolve function that uses GetAllServices<T> to resolve all services
			// see a test below
		}

		[TestMethod]
		public void GetAllServices_Keyed_and_NonKeyed_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("two");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("two");
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			var iTestServiceType = typeof(ITestService);
			var services = serviceProvider.GetAllServices(iTestServiceType);
			Assert.AreEqual(5, services.Count());
			Assert.IsInstanceOfType<AnotherTestService>(services.First());
			Assert.IsInstanceOfType<TestService>(services.ElementAt(1));
			Assert.IsInstanceOfType<AnotherTestService>(services.ElementAt(2));
			Assert.IsInstanceOfType<TestService>(services.ElementAt(3));
			Assert.IsInstanceOfType<AnotherTestService>(services.Last());
		}

		[TestMethod]
		public void GetAllServices_Keyed_and_NonKeyed_Generic_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("two");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("two");
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			var services = serviceProvider.GetAllServices<ITestService>();
			Assert.AreEqual(5, services.Count());
			Assert.IsInstanceOfType<AnotherTestService>(services.First());
			Assert.IsInstanceOfType<TestService>(services.ElementAt(1));
			Assert.IsInstanceOfType<AnotherTestService>(services.ElementAt(2));
			Assert.IsInstanceOfType<TestService>(services.ElementAt(3));
			Assert.IsInstanceOfType<AnotherTestService>(services.Last());
		}

		/// <summary>
		/// Register Keyed and non-keyed services and resolve a service that has an IEnumerable<T> dependency.
		/// </summary>
		[TestMethod]
		public void GetAllServices_Resolve_All_NonKeyed_and_KeyedServices_as_IEnumerable_dependency()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			serviceCollection.AddTransient<ItHasIEnumerableDependency>(sp =>
			{
				return ActivatorUtilities.CreateInstance<ItHasIEnumerableDependency>(
					sp,
					sp.GetAllServices<ITestService>()
					);
			});
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			// resolve service that has IEnumerable<T> dependency
			var service = serviceProvider.GetService<ItHasIEnumerableDependency>();
			// framework only resolves non-keyed services
			Assert.IsNotNull(service);
			Assert.AreEqual(3, service.Services.Count());
			Assert.IsInstanceOfType<AnotherTestService>(service.Services.First());
			Assert.IsInstanceOfType<TestService>(service.Services.ElementAt(1));
			Assert.IsInstanceOfType<AnotherTestService>(service.Services.Last());
		}

		[TestMethod]
		public void GetAllServices_Keyed_and_NonKeyed_With_Decorators_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("two");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("two");
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			var iTestServiceType = typeof(ITestService);
			var services = serviceProvider.GetAllServices(iTestServiceType);
			Assert.AreEqual(5, services.Count());
			Assert.IsInstanceOfType<AnotherTestService>(services.First());
			Assert.IsInstanceOfType<TestService>(services.ElementAt(1));
			Assert.IsInstanceOfType<TestServiceDecorator1>(services.ElementAt(2));
			Assert.IsInstanceOfType<TestService>(services.ElementAt(3));
			Assert.IsInstanceOfType<AnotherTestService>(services.Last());
		}

		[TestMethod]
		public void GetAllServices_Keyed_and_NonKeyed_With_Decorators_Generic_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("two");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("two");
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			var services = serviceProvider.GetAllServices<ITestService>();
			Assert.AreEqual(5, services.Count());
			Assert.IsInstanceOfType<AnotherTestService>(services.First());
			Assert.IsInstanceOfType<TestService>(services.ElementAt(1));
			Assert.IsInstanceOfType<TestServiceDecorator1>(services.ElementAt(2));
			Assert.IsInstanceOfType<TestService>(services.ElementAt(3));
			Assert.IsInstanceOfType<AnotherTestService>(services.Last());
		}

		[TestMethod]
		public void GetAllServices_Returns_Empty_If_No_Service_Matches_Generic_Extension_Method()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
			serviceCollection.AddKeyedSingleton<ITestService, AnotherTestService>("one");
			serviceCollection.AddTransient<ITestService, AnotherTestService>();
			using var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);

			var services = serviceProvider.GetAllServices<IAnotherInterface>();
			Assert.AreEqual(0, services.Count());
		}
	}

	public class ItHasIEnumerableDependency
	{
		public ItHasIEnumerableDependency(IEnumerable<ITestService> services)
		{
			Services = services;
		}

		public IEnumerable<ITestService> Services { get; }
	}
}
