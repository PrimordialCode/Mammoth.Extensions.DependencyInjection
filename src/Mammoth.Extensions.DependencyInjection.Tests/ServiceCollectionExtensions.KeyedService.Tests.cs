using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;
using Mammoth.Extensions.DependencyInjection.Inspector;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class KeyedServicesExtensionsTests
	{
		[TestMethod]
		public void Singleton_Generic_Resolve_DependsOn_Parameter()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService1>("one");
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService2>("two");
			serviceCollection.AddSingleton<ServiceWithKeyedDep>(new Dependency[] {
				Parameter.ForKey("keyedService").Eq("one")
			});
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithKeyedDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType(service.KeyedService, typeof(KeyedService1));
		}

		[TestMethod]
		public void Singleton_Resolve_DependsOn_Parameter()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService1>("one");
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService2>("two");
			serviceCollection.AddSingleton(typeof(ServiceWithKeyedDep), new Dependency[] {
				Parameter.ForKey("keyedService").Eq("one")
				});
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithKeyedDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType(service.KeyedService, typeof(KeyedService1));
		}

		[TestMethod]
		public void Singleton_Generic_Resolve_DependsOn_Value()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<ServiceWithValueDep>(new Dependency[] {
				Dependency.OnValue("dep", "val1")
			});
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithValueDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.Dep);
			Assert.AreEqual("val1", service.Dep);
		}

		[TestMethod]
		public void Singleton_Resolve_DependsOn_Value()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton(typeof(ServiceWithValueDep), new Dependency[] {
				Dependency.OnValue("dep", "val1")
				});
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithValueDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.Dep);
			Assert.AreEqual("val1", service.Dep);
		}

		[TestMethod]
		public void Resolve_KeyedService_DependsOn_Parameter()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService1>("one");
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService2>("two");
			serviceCollection.TryAddKeyedSingleton<ServiceWithKeyedDep>(
				"keyed",
				new Dependency[] {
				Parameter.ForKey("keyedService").Eq("one")
				});
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredKeyedService<ServiceWithKeyedDep>("keyed");
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType(service.KeyedService, typeof(KeyedService1));
		}

		[TestMethod]
		public void Resolve_KeyedService_DependsOn_Value()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedSingleton<ServiceWithValueDep>(
				"keyed",
				new Dependency[] {
				Dependency.OnValue("dep", "val1")
				});
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredKeyedService<ServiceWithValueDep>("keyed");
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.Dep);
			Assert.AreEqual("val1", service.Dep);
		}

		[TestMethod]
		public void Resolve_DependsOn_Value_Decorated()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<IServiceWithValueDep, ServiceWithValueDep>(new Dependency[] {
				Dependency.OnValue("dep", "val1")
			});
			serviceCollection.Decorate<IServiceWithValueDep, ServiceWithValueDepDecorator1>();
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<IServiceWithValueDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.Dep);
			Assert.AreEqual("val1", service.Dep);
			Assert.IsInstanceOfType(service, typeof(ServiceWithValueDepDecorator1));
			var decorator = (ServiceWithValueDepDecorator1)service;
			Assert.IsInstanceOfType(decorator.Inner, typeof(ServiceWithValueDep));
		}

		[TestMethod]
		public void AssemblyInspector_Resolve_KeyedService_DependsOn_Parameter()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService1>("one");
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService2>("two");
			serviceCollection.Add(
				new AssemblyInspector()
					.FromAssemblyContaining<ServiceWithKeyedDep>()
					.BasedOn<ServiceWithKeyedDep>()
					.WithServiceSelf()
					.LifestyleSingleton(dependsOn: new Dependency[]
					{
						Parameter.ForKey("keyedService").Eq("one")
					})
				);
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithKeyedDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType(service.KeyedService, typeof(KeyedService1));
		}

		[TestMethod]
		public void Resolve_Open_Generic()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<IKeyedService, KeyedService1>();
			serviceCollection.AddTransient(typeof(IOpengenericServiceWithKeyedDep<>), typeof(OpenGenericServiceWithKeyedDep<>));

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<IOpengenericServiceWithKeyedDep<string>>();
			Assert.IsNotNull(service);
			Assert.AreEqual(typeof(string).FullName, service.GetType().GenericTypeArguments[0].FullName);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType(service.KeyedService, typeof(KeyedService1));
		}

		/// <summary>
		/// registering an open generic with a factory function is not supported:
		/// https://github.com/dotnet/runtime/issues/41050
		/// https://stackoverflow.com/questions/39029344/factory-pattern-with-open-generics
		/// </summary>
		[TestMethod]
		public void NOTSUPPORTED_Resolve_Open_Generic_DependsOn()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService1>("one");
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService2>("two");
			// DependsOn is simulated by using a factory function
			serviceCollection.AddTransient(typeof(IOpengenericServiceWithKeyedDep<>), typeof(OpenGenericServiceWithKeyedDep<>),
				dependsOn: new Dependency[]
				{
					Parameter.ForKey("keyedService").Eq("one")
				});

			Assert.ThrowsException<ArgumentException>(() =>
			{
				using var serviceProvider = serviceCollection.BuildServiceProvider();
			});

			/*
			var service = serviceProvider.GetRequiredService<IOpengenericServiceWithKeyedDep<string>>();
			Assert.IsNotNull(service);
			Assert.AreEqual(typeof(string).FullName, service.GetType().GenericTypeArguments[0].FullName);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType(service.KeyedService, typeof(KeyedService1));
			*/
		}

		public interface IServiceWithKeyedDep
		{
			IKeyedService KeyedService { get; }
		}

		public class ServiceWithKeyedDep : IServiceWithKeyedDep
		{
			public ServiceWithKeyedDep(IKeyedService keyedService)
			{
				KeyedService = keyedService;
			}

			public IKeyedService KeyedService { get; }
		}

		public interface IServiceWithValueDep
		{
			string Dep { get; }
		}

		public class ServiceWithValueDep : IServiceWithValueDep
		{
			public ServiceWithValueDep(string dep)
			{
				Dep = dep;
			}

			public string Dep { get; }
		}

		public class ServiceWithValueDepDecorator1 : IServiceWithValueDep
		{
			public IServiceWithValueDep Inner { get; }

			public ServiceWithValueDepDecorator1(IServiceWithValueDep inner)
			{
				Inner = inner;
			}

			public string Dep => Inner.Dep;
		}

		public interface IOpengenericServiceWithKeyedDep<T>
		{
			IKeyedService KeyedService { get; }
		}

		public class OpenGenericServiceWithKeyedDep<T> : IOpengenericServiceWithKeyedDep<T>
		{
			public OpenGenericServiceWithKeyedDep(IKeyedService keyedService)
			{
				KeyedService = keyedService;
			}

			public IKeyedService KeyedService { get; }
		}
	}
}
