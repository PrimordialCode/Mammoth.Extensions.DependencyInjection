#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable MSTEST0032 // Assertion condition is always true

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;
using Mammoth.Extensions.DependencyInjection.Inspector;
using Mammoth.Extensions.DependencyInjection.Configuration;

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
			serviceCollection.AddSingleton<ServiceWithKeyedDep>([
				Parameter.ForKey("keyedService").Eq("one")
			]);
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithKeyedDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType<KeyedService1>(service.KeyedService);
		}

		[TestMethod]
		public void Singleton_Resolve_DependsOn_Parameter()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService1>("one");
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService2>("two");

			var serviceWithKeyedDepType = typeof(ServiceWithKeyedDep);
			serviceCollection.AddSingleton(serviceWithKeyedDepType, [
				Parameter.ForKey("keyedService").Eq("one")
				]);
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithKeyedDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType<KeyedService1>(service.KeyedService);
		}

		[TestMethod]
		public void Singleton_Generic_Resolve_DependsOn_Value()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<ServiceWithValueDep>([
				Dependency.OnValue("dep", "val1")
			]);
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

			var serviceWithValueDepType = typeof(ServiceWithValueDep);
			serviceCollection.AddSingleton(serviceWithValueDepType, [
				Dependency.OnValue("dep", "val1")
			]);
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
				[
				Parameter.ForKey("keyedService").Eq("one")
				]);
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredKeyedService<ServiceWithKeyedDep>("keyed");
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType<KeyedService1>(service.KeyedService);
		}

		[TestMethod]
		public void Resolve_KeyedService_DependsOn_Value()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedSingleton<ServiceWithValueDep>(
				"keyed",
				[
				Dependency.OnValue("dep", "val1")
				]);
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
			serviceCollection.AddTransient<IServiceWithValueDep, ServiceWithValueDep>([
				Dependency.OnValue("dep", "val1")
			]);
			serviceCollection.Decorate<IServiceWithValueDep, ServiceWithValueDepDecorator1>();
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<IServiceWithValueDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.Dep);
			Assert.AreEqual("val1", service.Dep);
			Assert.IsInstanceOfType<ServiceWithValueDepDecorator1>(service);
			var decorator = (ServiceWithValueDepDecorator1)service;
			Assert.IsInstanceOfType<ServiceWithValueDep>(decorator.Inner);
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
					.Configure((configureAction, _) => configureAction.DependsOn =
					[
						Parameter.ForKey("keyedService").Eq("one")
					])
					.LifestyleSingleton()
				);
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<ServiceWithKeyedDep>();
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType<KeyedService1>(service.KeyedService);
		}

		[TestMethod]
		public void AssemblyInspector_Resolve_KeyedService_ServiceKey_DependsOn_Parameter()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService1>("one");
			serviceCollection.TryAddKeyedTransient<IKeyedService, KeyedService2>("two");
			serviceCollection.Add(
				new AssemblyInspector()
					.FromAssemblyContaining<ServiceWithKeyedDep>()
					.BasedOn<ServiceWithKeyedDep>()
					.WithServiceSelf()
					.Configure((configureAction, _) =>
					{
						configureAction.ServiceKey = "three";
						configureAction.DependsOn =
						[
							Parameter.ForKey("keyedService").Eq("one")
						];
					})
					.LifestyleSingleton()
				);
			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredKeyedService<ServiceWithKeyedDep>("three");
			Assert.IsNotNull(service);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType<KeyedService1>(service.KeyedService);
		}

		[TestMethod]
		public void Resolve_Open_Generic()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<IKeyedService, KeyedService1>();
			serviceCollection.AddTransient(typeof(IOpenGenericServiceWithKeyedDep<>), typeof(OpenGenericServiceWithKeyedDep<>));

			using var serviceProvider = serviceCollection.BuildServiceProvider();

			var service = serviceProvider.GetRequiredService<IOpenGenericServiceWithKeyedDep<string>>();
			Assert.IsNotNull(service);
			Assert.AreEqual(typeof(string).FullName, service.GetType().GenericTypeArguments[0].FullName);
			Assert.IsNotNull(service.KeyedService);
			Assert.IsInstanceOfType<KeyedService1>(service.KeyedService);
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
			serviceCollection.AddTransient(typeof(IOpenGenericServiceWithKeyedDep<>), typeof(OpenGenericServiceWithKeyedDep<>),
				dependsOn:
				[
					Parameter.ForKey("keyedService").Eq("one")
				]);

			Assert.ThrowsExactly<ArgumentException>(() =>
			{
				using var serviceProvider = serviceCollection.BuildServiceProvider();
			});

			/***
			var service = serviceProvider.GetRequiredService<IOpenGenericServiceWithKeyedDep<string>>();
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

#pragma warning disable S2326 // Unused type parameters should be removed
		public interface IOpenGenericServiceWithKeyedDep<T>
#pragma warning restore S2326 // Unused type parameters should be removed
		{
			IKeyedService KeyedService { get; }
		}

		public class OpenGenericServiceWithKeyedDep<T> : IOpenGenericServiceWithKeyedDep<T>
		{
			public OpenGenericServiceWithKeyedDep(IKeyedService keyedService)
			{
				KeyedService = keyedService;
			}

			public IKeyedService KeyedService { get; }
		}
	}
}

#pragma warning restore MSTEST0032 // Assertion condition is always true
#pragma warning restore IDE0079 // Remove unnecessary suppression