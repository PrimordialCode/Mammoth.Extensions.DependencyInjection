using Mammoth.Extensions.DependencyInjection.Configuration;
using Mammoth.Extensions.DependencyInjection.Inspector;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class AssemblyInspectorGenericCandidateTests
	{
		[TestMethod]
		public void OrdinaryMarkerScanBuildsAndResolvesWhenTheAssemblyAlsoContainsOpenImplementations()
		{
			var descriptors = new AssemblyInspector().FromAssemblyContaining<Marker>()
				.BasedOn<IMarker>().WithServiceAllInterfaces().LifestyleTransient().ToArray();
			IServiceCollection services = new ServiceCollection();
			foreach (var descriptor in descriptors)
				services.Add(descriptor);

			using var provider = services.BuildServiceProvider();
			Assert.IsInstanceOfType<Marker>(provider.GetRequiredService<IMarker>());
			Assert.HasCount(1, provider.GetServices<IMarker>().ToArray());
			Assert.HasCount(1, descriptors);
			Assert.AreEqual(typeof(Marker), descriptors[0].ImplementationType);
		}

		[TestMethod]
		[DataRow(false, false, false, ServiceLifetime.Transient)]
		[DataRow(false, false, false, ServiceLifetime.Scoped)]
		[DataRow(false, false, false, ServiceLifetime.Singleton)]
		[DataRow(false, true, false, ServiceLifetime.Transient)]
		[DataRow(false, true, false, ServiceLifetime.Scoped)]
		[DataRow(false, true, false, ServiceLifetime.Singleton)]
		[DataRow(true, false, false, ServiceLifetime.Transient)]
		[DataRow(true, false, false, ServiceLifetime.Scoped)]
		[DataRow(true, false, false, ServiceLifetime.Singleton)]
		[DataRow(true, true, false, ServiceLifetime.Transient)]
		[DataRow(true, true, false, ServiceLifetime.Scoped)]
		[DataRow(true, true, false, ServiceLifetime.Singleton)]
		[DataRow(false, false, true, ServiceLifetime.Transient)]
		[DataRow(false, false, true, ServiceLifetime.Scoped)]
		[DataRow(false, false, true, ServiceLifetime.Singleton)]
		[DataRow(false, true, true, ServiceLifetime.Transient)]
		[DataRow(false, true, true, ServiceLifetime.Scoped)]
		[DataRow(false, true, true, ServiceLifetime.Singleton)]
		[DataRow(true, false, true, ServiceLifetime.Transient)]
		[DataRow(true, false, true, ServiceLifetime.Scoped)]
		[DataRow(true, false, true, ServiceLifetime.Singleton)]
		[DataRow(true, true, true, ServiceLifetime.Transient)]
		[DataRow(true, true, true, ServiceLifetime.Scoped)]
		[DataRow(true, true, true, ServiceLifetime.Singleton)]
		public void MarkerScansSkipUnboundImplementationsBeforeConfiguration(bool allInterfaces, bool keyed, bool mapped, ServiceLifetime lifetime)
		{
			var candidates = new AssemblyInspector().FromAssemblyContaining<Marker>().BasedOn<IMarker>();
			var selector = allInterfaces ? candidates.WithServiceAllInterfaces() : candidates.WithServiceBase();
			var configured = new List<Type>();
			selector.Configure((registration, type) =>
			{
				configured.Add(type);
				registration.ServiceKey = keyed ? "marker" : null;
				if (mapped)
					registration.DependsOn = [Dependency.OnValue("label", "mapped")];
			});
			var descriptors = Finish(selector, lifetime);

			CollectionAssert.AreEqual(new[] { typeof(Marker) }, configured);
			Assert.HasCount(1, descriptors);
			Assert.AreEqual(typeof(IMarker), descriptors[0].ServiceType);
			Assert.AreEqual(lifetime, descriptors[0].Lifetime);
			Assert.AreEqual(keyed, descriptors[0].IsKeyedService);
			IServiceCollection services = new ServiceCollection();
			services.Add(descriptors[0]);
			using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
			using var scope = provider.CreateScope();
			var resolved = keyed
				? scope.ServiceProvider.GetKeyedServices<IMarker>("marker").ToArray()
				: scope.ServiceProvider.GetServices<IMarker>().ToArray();
			Assert.HasCount(1, resolved);
			var marker = Assert.IsInstanceOfType<Marker>(resolved[0]);
			Assert.AreEqual(mapped ? "mapped" : "default", marker.Label);
		}

		[TestMethod]
		[DataRow(0, false)]
		[DataRow(1, false)]
		[DataRow(2, false)]
		[DataRow(0, true)]
		[DataRow(1, true)]
		[DataRow(2, true)]
		public void ClosedGenericInterfacesAndConcreteImplementationsStillResolve(int selection, bool keyed)
		{
			var candidates = new AssemblyInspector().FromAssemblyContaining<ClosedRepository>().BasedOn<IRepository<object>>();
			var selector = selection switch
			{
				0 => candidates.WithServiceAllInterfaces(),
				1 => candidates.WithServiceBase(),
				_ => candidates.WithServiceSelf()
			};
			var descriptors = selector.Configure((registration, _) => registration.ServiceKey = keyed ? "closed" : null)
				.LifestyleSingleton().ToArray();
			Assert.HasCount(1, descriptors);
			var serviceType = selection == 2 ? typeof(ClosedRepository) : typeof(IRepository<object>);
			Assert.AreEqual(serviceType, descriptors[0].ServiceType);
			Assert.AreEqual(typeof(ClosedRepository), keyed ? descriptors[0].KeyedImplementationType : descriptors[0].ImplementationType);
			IServiceCollection services = new ServiceCollection();
			services.Add(descriptors[0]);
			using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
			var resolved = keyed ? provider.GetRequiredKeyedService(serviceType, "closed") : provider.GetRequiredService(serviceType);
			Assert.IsInstanceOfType<ClosedRepository>(resolved);
		}

		[TestMethod]
		[DataRow(false, false, false)]
		[DataRow(false, true, false)]
		[DataRow(true, false, false)]
		[DataRow(true, true, false)]
		[DataRow(false, false, true)]
		[DataRow(false, true, true)]
		[DataRow(true, false, true)]
		[DataRow(true, true, true)]
		public void OpenGenericSelfRegistrationsRetainNativeConstruction(bool baseSelection, bool keyed, bool emptyMap)
		{
			var candidates = new AssemblyInspector().FromAssemblyContaining<ClosedRepository>().BasedOn(typeof(Repository<>));
			var selector = baseSelection ? candidates.WithServiceBase() : candidates.WithServiceSelf();
			var descriptors = selector.Configure((registration, _) =>
				{
					registration.ServiceKey = keyed ? "self" : null;
					registration.DependsOn = emptyMap ? [] : null;
				})
				.LifestyleTransient().ToArray();
			Assert.HasCount(1, descriptors);
			Assert.AreEqual(typeof(Repository<>), descriptors[0].ServiceType);
			Assert.AreEqual(typeof(Repository<>), keyed ? descriptors[0].KeyedImplementationType : descriptors[0].ImplementationType);
			IServiceCollection services = new ServiceCollection();
			services.Add(descriptors[0]);
			using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
			var resolved = keyed ? provider.GetRequiredKeyedService<Repository<object>>("self") : provider.GetRequiredService<Repository<object>>();
			Assert.IsNotNull(resolved);
		}

		[TestMethod]
		public void AllInterfaceSelectionDoesNotInferOpenGenericMappings()
		{
			var descriptors = new AssemblyInspector().FromAssemblyContaining<ClosedRepository>()
				.If(type => type == typeof(Repository<>) || type == typeof(ClosedRepository))
				.WithServiceAllInterfaces().LifestyleTransient().ToArray();
			Assert.HasCount(1, descriptors);
			Assert.AreEqual(typeof(IRepository<object>), descriptors[0].ServiceType);
			Assert.AreEqual(typeof(ClosedRepository), descriptors[0].ImplementationType);
		}

		[TestMethod]
		public void BasedOnOpenGenericInterfaceDoesNotGainGenericDiscovery()
		{
			var descriptors = new AssemblyInspector().FromAssemblyContaining<ClosedRepository>()
				.BasedOn(typeof(IRepository<>)).WithServiceBase().LifestyleTransient().ToArray();
			Assert.HasCount(0, descriptors);
		}

		private static ServiceDescriptor[] Finish(ILifestyleSelector selector, ServiceLifetime lifetime) => (lifetime switch
		{
			ServiceLifetime.Transient => selector.LifestyleTransient(),
			ServiceLifetime.Scoped => selector.LifestyleScoped(),
			_ => selector.LifestyleSingleton()
		}).ToArray();

		public interface IMarker { }
		public interface IChildMarker : IMarker { }
		public abstract class AbstractMarker : IMarker { }
		public class Marker : IMarker
		{
			public Marker(string label = "default") => Label = label;
			public string Label { get; }
		}
		public class MarkerGeneric<T> : IMarker { }
		public class GenericContainer<T>
		{
			public class NestedMarker : IMarker { }
		}
		public interface IRepository<T> { }
		public class Repository<T> : IRepository<T> where T : class, new() { }
		public class ClosedRepository : Repository<object> { }
	}
}
