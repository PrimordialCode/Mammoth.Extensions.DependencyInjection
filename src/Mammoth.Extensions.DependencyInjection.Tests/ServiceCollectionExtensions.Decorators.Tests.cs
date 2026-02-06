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

	/// <summary>
	/// A concrete class used as a service type for class decoration tests.
	/// Methods are virtual so decorators (subclasses) can override them.
	/// </summary>
	public class ConcreteService
	{
		public virtual string GetValue() => "ConcreteService";
	}

	/// <summary>
	/// Decorator for ConcreteService (inherits from the class, not an interface).
	/// </summary>
	public class ConcreteServiceDecorator1 : ConcreteService
	{
		public readonly ConcreteService Inner;

		public ConcreteServiceDecorator1(ConcreteService inner)
		{
			Inner = inner;
		}

		public override string GetValue() => $"Decorator1({Inner.GetValue()})";
	}

	/// <summary>
	/// Decorator for ConcreteService that also depends on an external service.
	/// </summary>
	public class ConcreteServiceDecorator2 : ConcreteService
	{
		public readonly ConcreteService Inner;
		public ExternalService ExternalService { get; }

		public ConcreteServiceDecorator2(ConcreteService inner, ExternalService externalService)
		{
			Inner = inner;
			ExternalService = externalService;
		}

		public override string GetValue() => $"Decorator2({Inner.GetValue()})";
	}

	[TestClass]
	public class ClassDecoratorsExtensionsTests
	{
		[TestMethod]
		public void Decorate_Class_Registered_As_Type_Transient()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<ConcreteService>();
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator2>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ConcreteService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<ConcreteServiceDecorator2>(service);
			var decorator2 = (ConcreteServiceDecorator2)service;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(decorator2.Inner);
			var decorator1 = (ConcreteServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<ConcreteService>(decorator1.Inner);
			Assert.AreEqual("Decorator2(Decorator1(ConcreteService))", service.GetValue());
		}

		[TestMethod]
		public void Decorate_Class_Registered_As_Type_Singleton()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<ConcreteService>();
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service1 = serviceProvider.GetRequiredService<ConcreteService>();
			var service2 = serviceProvider.GetRequiredService<ConcreteService>();

			Assert.IsNotNull(service1);
			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(service1);
			Assert.AreSame(service1, service2);
		}

		[TestMethod]
		public void Decorate_Class_Registered_As_Type_Scoped()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddScoped<ConcreteService>();
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			ConcreteService service1, service2, service3;
			using (var scope = serviceProvider.CreateScope())
			{
				service1 = scope.ServiceProvider.GetRequiredService<ConcreteService>();
				service2 = scope.ServiceProvider.GetRequiredService<ConcreteService>();
			}
			using (var scope = serviceProvider.CreateScope())
			{
				service3 = scope.ServiceProvider.GetRequiredService<ConcreteService>();
			}

			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(service1);
			Assert.AreSame(service1, service2);
			Assert.AreNotSame(service1, service3);
		}

		[TestMethod]
		public void Decorate_Class_Registered_As_Instance()
		{
			var serviceCollection = new ServiceCollection();
			var instance = new ConcreteService();
			serviceCollection.AddSingleton(instance);
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator2>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ConcreteService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<ConcreteServiceDecorator2>(service);
			var decorator2 = (ConcreteServiceDecorator2)service;
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(decorator2.Inner);
			var decorator1 = (ConcreteServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.AreSame(instance, decorator1.Inner);
		}

		[TestMethod]
		public void Decorate_Class_Registered_As_Factory()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<ConcreteService>(_ => new ConcreteService());
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator2>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ConcreteService>();

			Assert.IsNotNull(service);
			Assert.IsInstanceOfType<ConcreteServiceDecorator2>(service);
			var decorator2 = (ConcreteServiceDecorator2)service;
			Assert.IsNotNull(decorator2.ExternalService);
			Assert.IsNotNull(decorator2.Inner);
			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(decorator2.Inner);
			var decorator1 = (ConcreteServiceDecorator1)decorator2.Inner;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<ConcreteService>(decorator1.Inner);
			Assert.AreEqual("Decorator2(Decorator1(ConcreteService))", service.GetValue());
		}

		[TestMethod]
		public void Decorate_Class_Keyed_Registered_As_Type()
		{
			var serviceCollection = new ServiceCollection();

			serviceCollection.AddTransient<ConcreteService>(); // this will not be decorated

			serviceCollection.AddKeyedTransient<ConcreteService>("key");
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var unkeyed = serviceProvider.GetRequiredService<ConcreteService>();
			Assert.IsNotNull(unkeyed);
			Assert.IsInstanceOfType<ConcreteService>(unkeyed);
			Assert.IsNotInstanceOfType<ConcreteServiceDecorator1>(unkeyed);

			var keyed = serviceProvider.GetRequiredKeyedService<ConcreteService>("key");
			Assert.IsNotNull(keyed);
			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(keyed);
		}

		[TestMethod]
		public void Decorate_Class_Keyed_Registered_As_Factory()
		{
			var serviceCollection = new ServiceCollection();

			serviceCollection.AddTransient<ConcreteService>(_ => new ConcreteService()); // this will not be decorated

			serviceCollection.AddKeyedTransient<ConcreteService>("key", (_, _) => new ConcreteService());
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var unkeyed = serviceProvider.GetRequiredService<ConcreteService>();
			Assert.IsNotNull(unkeyed);
			Assert.IsInstanceOfType<ConcreteService>(unkeyed);
			Assert.IsNotInstanceOfType<ConcreteServiceDecorator1>(unkeyed);

			var keyed = serviceProvider.GetRequiredKeyedService<ConcreteService>("key");
			Assert.IsNotNull(keyed);
			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(keyed);
			var decorator1 = (ConcreteServiceDecorator1)keyed;
			Assert.IsNotNull(decorator1.Inner);
			Assert.IsInstanceOfType<ConcreteService>(decorator1.Inner);
		}

		[TestMethod]
		public void Decorate_Class_Keyed_Registered_As_Instance()
		{
			var serviceCollection = new ServiceCollection();
			var instance = new ConcreteService();
			var keyedInstance = new ConcreteService();

			serviceCollection.AddSingleton(instance); // this will not be decorated

			serviceCollection.AddKeyedSingleton("key", keyedInstance);
			serviceCollection.Decorate<ConcreteService, ConcreteServiceDecorator1>();

			serviceCollection.AddTransient<ExternalService>();

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var unkeyed = serviceProvider.GetRequiredService<ConcreteService>();
			Assert.IsNotNull(unkeyed);
			Assert.AreSame(instance, unkeyed);

			var keyed = serviceProvider.GetRequiredKeyedService<ConcreteService>("key");
			Assert.IsNotNull(keyed);
			Assert.IsInstanceOfType<ConcreteServiceDecorator1>(keyed);
			var decorator1 = (ConcreteServiceDecorator1)keyed;
			Assert.AreSame(keyedInstance, decorator1.Inner);
		}
	}
}

#pragma warning restore MSTEST0032 // Assertion condition is always true
#pragma warning restore IDE0079 // Remove unnecessary suppression