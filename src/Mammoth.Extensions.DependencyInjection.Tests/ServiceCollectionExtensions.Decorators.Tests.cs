#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable MSTEST0032 // Assertion condition is always true

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class DecoratorsExtensionsTests
	{
		[TestMethod]
		public void Decorate_Service_Registered_As_Instance()
		{
			var serviceCollection = new ServiceCollection();
			TestService implementationInstance = new TestService();
			serviceCollection.AddSingleton<ITestService>((ITestService)implementationInstance); // innermost service
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>(); //innermost decorator
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>(); // outermost decorator

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<ITestService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);
			Assert.AreEqual(decorator1.Inner, implementationInstance);
		}

		[TestMethod]
		public void Decorate_Service_Registered_As_Instance_Only_Services_Above_Decorate_Call_Will_Be_Decorated()
		{
			var serviceCollection = new ServiceCollection();

			TestService implementationInstance = new TestService();
			serviceCollection.AddSingleton<ITestService>((ITestService)implementationInstance); // innermost service
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>(); //innermost decorator
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>(); // outermost decorator
			TestService implementationInstance2 = new TestService();
			serviceCollection.AddSingleton<ITestService>((ITestService)implementationInstance2);

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var services = _serviceProvider.GetServices<ITestService>();

			var service = services.First();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);
			Assert.AreEqual(decorator1.Inner, implementationInstance);

			service = services.Last();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestService>(service);
			Assert.AreEqual(service, implementationInstance2);
		}

		[TestMethod]
		public void Decorate_Service_Registered_As_Type()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<ITestService, TestService>(); // innermost service
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>(); //innermost decorator
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>(); // outermost decorator

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<ITestService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);
		}

		[TestMethod]
		public void Decorate_Service_Registered_As_Type_Only_Services_Above_Decorate_Call_Will_Be_Decorated()
		{
			var serviceCollection = new ServiceCollection();

			serviceCollection.AddTransient<ITestService, TestService>(); // innermost service
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>(); //innermost decorator
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>(); // outermost decorator
			serviceCollection.AddTransient<ITestService, AnotherTestService>();

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var services = _serviceProvider.GetServices<ITestService>();

			var service = services.First();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);

			service = services.Last();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<AnotherTestService>(service);
		}

		[TestMethod]
		public void Decorate_Service_Registered_As_Factory()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<ITestService, TestService>((_) => new TestService());
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>();

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<ITestService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);
		}

		[TestMethod]
		public void Decorate_Service_Registered_As_Factory_Only_Services_Above_Decorate_Call_Will_Be_Decorated()
		{
			var serviceCollection = new ServiceCollection();

			serviceCollection.AddTransient<ITestService, TestService>((_) => new TestService());
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>();
			serviceCollection.AddTransient<ITestService, AnotherTestService>((_) => new AnotherTestService());

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var services = _serviceProvider.GetServices<ITestService>();

			var service = services.First();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);

			service = services.Last();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<AnotherTestService>(service);
		}

		[TestMethod]
		public void Decorate_KeyedService_Registered_As_Instance()
		{
			var serviceCollection = new ServiceCollection();
			TestService implementationInstance = new TestService();
			TestService implementationInstance2 = new TestService();

			serviceCollection.AddSingleton<ITestService>(implementationInstance); // this will not be decorated

			serviceCollection.TryAddKeyedSingleton("key", (ITestService)implementationInstance); // innermost service
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>(); //innermost decorator
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>(); // outermost decorator

			serviceCollection.TryAddKeyedSingleton("key2", (ITestService)implementationInstance2); // this will not be decorated

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<ITestService>();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType< TestService>(service);
			Assert.AreEqual(service, implementationInstance);

			service = _serviceProvider.GetRequiredKeyedService<ITestService>("key");
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType< TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);
			Assert.AreEqual(decorator1.Inner, implementationInstance);

			service = _serviceProvider.GetRequiredKeyedService<ITestService>("key2");
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestService>(service);
			Assert.AreEqual(service, implementationInstance2);
		}

		[TestMethod]
		public void Decorate_KeyedService_Registered_As_Type()
		{
			var serviceCollection = new ServiceCollection();

			serviceCollection.AddTransient<ITestService, TestService>(); // this will not be decorated

			serviceCollection.TryAddKeyedTransient<ITestService, TestService>("key"); // innermost service
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>(); //innermost decorator
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>(); // outermost decorator

			serviceCollection.TryAddKeyedTransient<ITestService, TestService>("key2"); // this will not be decorated

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<ITestService>();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestService>(service);

			service = _serviceProvider.GetRequiredKeyedService<ITestService>("key");
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);

			service = _serviceProvider.GetRequiredKeyedService<ITestService>("key2");
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestService>(service);
		}

		[TestMethod]
		public void Decorate_KeyedService_Registered_As_Factory()
		{
			var serviceCollection = new ServiceCollection();

			serviceCollection.AddTransient<ITestService>((_) => new TestService()); // this will not be created

			serviceCollection.TryAddKeyedTransient<ITestService>("key", (_, _) => new TestService()); // innermost service
			serviceCollection.Decorate<ITestService, TestServiceDecorator1>(); //innermost decorator
			serviceCollection.Decorate<ITestService, TestServiceDecorator2>();
			serviceCollection.Decorate<ITestService, TestServiceDecorator3>(); // outermost decorator

			serviceCollection.TryAddKeyedTransient<ITestService>("key2", (_, _) => new TestService()); // innermost service

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<ITestService>();
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestService>(service);

			service = _serviceProvider.GetRequiredKeyedService<ITestService>("key");
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceDecorator3>(service);
			var decorator3 = (TestServiceDecorator3)service;
			Assert.IsNotNull(decorator3.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator2>(decorator3.Inner);
			var decorator2 = (TestServiceDecorator2)decorator3.Inner;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsInstanceOfType<ExternalService>(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<TestServiceDecorator1>(decorator2.Inner);
			var decorator1 = (TestServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<TestService>(decorator1.Inner);

			service = _serviceProvider.GetRequiredKeyedService<ITestService>("key2");
			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestService>(service);
		}

		#region Tests for Class Decoration (not just interfaces)

		[TestMethod]
		public void Decorate_ConcreteClass_Registered_As_Type()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TestService>(); // Register concrete class directly
			serviceCollection.Decorate<TestService, TestServiceConcreteDecorator>(); 

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<TestService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceConcreteDecorator>(service);
			var decorator = (TestServiceConcreteDecorator)service;
			Assert.IsNotNull(decorator.Inner);
			Assert.IsInstanceOfType<TestService>(decorator.Inner);
		}

		[TestMethod]
		public void Decorate_ConcreteClass_Registered_As_Instance()
		{
			var serviceCollection = new ServiceCollection();
			var implementationInstance = new TestService();
			serviceCollection.AddSingleton(implementationInstance); // Register concrete class instance
			serviceCollection.Decorate<TestService, TestServiceConcreteDecorator>();

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<TestService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceConcreteDecorator>(service);
			var decorator = (TestServiceConcreteDecorator)service;
			Assert.IsNotNull(decorator.Inner);
			Assert.IsInstanceOfType<TestService>(decorator.Inner);
			Assert.AreEqual(decorator.Inner, implementationInstance);
		}

		[TestMethod]
		public void Decorate_ConcreteClass_Registered_As_Factory()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<TestService>(_ => new TestService()); // Register with factory
			serviceCollection.Decorate<TestService, TestServiceConcreteDecorator>();

			serviceCollection.AddTransient<ExternalService>();

			using var _serviceProvider = serviceCollection.BuildServiceProvider();

			var service = _serviceProvider.GetRequiredService<TestService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<TestServiceConcreteDecorator>(service);
			var decorator = (TestServiceConcreteDecorator)service;
			Assert.IsNotNull(decorator.Inner);
			Assert.IsInstanceOfType<TestService>(decorator.Inner);
		}

		#endregion
	}

	public class TestServiceDecorator1 : ITestService
	{
		public readonly ITestService Inner;

		public TestServiceDecorator1(ITestService transientService)
		{
			Inner = transientService;
		}

		public T? GetById<T>(string id) where T : class
		{
			return Inner.GetById<T>(id);
		}

		public bool WasDisposed()
		{
			return Inner.WasDisposed();
		}
	}

	/// <summary>
	/// Some decorators might depend on other services
	/// </summary>
	public class TestServiceDecorator2 : ITestService
	{
		public readonly ITestService Inner;

		public TestServiceDecorator2(ITestService transientService,
			ExternalService externalService)
		{
			Inner = transientService;
			ExternalService = externalService;
		}

		public ExternalService ExternalService { get; }
		public T? GetById<T>(string id) where T : class
		{
			return Inner.GetById<T>(id);
		}

		public bool WasDisposed()
		{
			return Inner.WasDisposed();
		}
	}

	public class TestServiceDecorator3 : ITestService
	{
		public readonly ITestService Inner;

		public TestServiceDecorator3(ITestService transientService)
		{
			Inner = transientService;
		}
		public T? GetById<T>(string id) where T : class
		{
			return Inner.GetById<T>(id);
		}

		public bool WasDisposed()
		{
			return Inner.WasDisposed();
		}
	}
}

#pragma warning restore MSTEST0032 // Assertion condition is always true
#pragma warning restore IDE0079 // Remove unnecessary suppression