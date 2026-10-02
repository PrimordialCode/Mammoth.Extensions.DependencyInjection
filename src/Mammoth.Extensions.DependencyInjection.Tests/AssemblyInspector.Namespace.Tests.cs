using Mammoth.Extensions.DependencyInjection.Inspector;
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection.Tests.NamespaceFixtures;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class AssemblyInspectorNamespaceRegressionTests
	{
		[TestMethod]
		public void DefaultOverloadSelectsTheExactCaseSensitiveNamespace()
		{
			var descriptors = NewInspector().InSameNamespaceAs<Acme.Services.Marker>()
				.WithServiceSelf().LifestyleSingleton().ToArray();
			AssertTypes(descriptors, typeof(Acme.Services.Marker), typeof(Acme.Services.ExactService));
		}

		[TestMethod]
		[DataRow(false, ServiceLifetime.Transient)]
		[DataRow(false, ServiceLifetime.Scoped)]
		[DataRow(false, ServiceLifetime.Singleton)]
		[DataRow(true, ServiceLifetime.Transient)]
		[DataRow(true, ServiceLifetime.Scoped)]
		[DataRow(true, ServiceLifetime.Singleton)]
		public void NamespaceBoundaryIsRespectedAcrossLifetimes(bool includeChildren, ServiceLifetime lifetime)
		{
			var selector = NewInspector().InSameNamespaceAs<Acme.Services.Marker>(includeChildren).WithServiceSelf();
			var descriptors = Finish(selector, lifetime);
			var expected = includeChildren
				? new[] { typeof(Acme.Services.Marker), typeof(Acme.Services.ExactService), typeof(Acme.Services.Child.ChildService), typeof(Acme.Services.Child.Deep.DeepService) }
				: new[] { typeof(Acme.Services.Marker), typeof(Acme.Services.ExactService) };
			AssertTypes(descriptors, expected);
			Assert.IsTrue(descriptors.All(d => d.Lifetime == lifetime && d.ServiceType == d.ImplementationType));
		}

		[TestMethod]
		public void OmittingTheNamespaceFilterStillIncludesSiblingAndDifferentCaseNamespaces()
		{
			var descriptors = NewInspector().BasedOn<IScanFixture>().WithServiceSelf().LifestyleSingleton().ToArray();
			AssertTypes(descriptors, AllFixtures);
		}

		[TestMethod]
		public void GlobalDefaultOverloadSelectsOnlyGlobalConcreteClasses()
		{
			AssertGlobalOnly(NewInspector().InSameNamespaceAs<GlobalNamespaceMarker>().WithServiceSelf().LifestyleSingleton().ToArray());
		}

		[TestMethod]
		[DataRow(false, ServiceLifetime.Transient)]
		[DataRow(false, ServiceLifetime.Scoped)]
		[DataRow(false, ServiceLifetime.Singleton)]
		[DataRow(true, ServiceLifetime.Transient)]
		[DataRow(true, ServiceLifetime.Scoped)]
		[DataRow(true, ServiceLifetime.Singleton)]
		public void GlobalBoolOverloadHasAnExplicitRootNamespacePolicy(bool includeChildren, ServiceLifetime lifetime)
		{
			var descriptors = Finish(NewInspector().InSameNamespaceAs<GlobalNamespaceMarker>(includeChildren).WithServiceSelf(), lifetime);
			if (includeChildren)
			{
				// Every named namespace is beneath the global namespace when children are requested.
				foreach (var fixture in AllFixtures)
					Assert.IsTrue(descriptors.Any(d => d.ImplementationType == fixture));
				Assert.IsFalse(descriptors.Any(d => d.ImplementationType == typeof(AbstractGlobalNamespaceFixture)));
			}
			else
			{
				AssertGlobalOnly(descriptors);
			}
			Assert.IsTrue(descriptors.All(d => d.Lifetime == lifetime && d.ServiceType == d.ImplementationType));
		}

		[TestMethod]
		[DataRow(false, false, 0)]
		[DataRow(false, false, 1)]
		[DataRow(false, false, 2)]
		[DataRow(false, true, 0)]
		[DataRow(false, true, 1)]
		[DataRow(false, true, 2)]
		[DataRow(true, false, 0)]
		[DataRow(true, false, 1)]
		[DataRow(true, false, 2)]
		[DataRow(true, true, 0)]
		[DataRow(true, true, 1)]
		[DataRow(true, true, 2)]
		public void NamespaceSelectionComposesWithBasedOnAndIf(bool global, bool includeChildren, int serviceSelection)
		{
			var inspector = NewInspector();
			inspector.BasedOn<IScanFixture>();
			var selector = global
				? inspector.InSameNamespaceAs<GlobalNamespaceMarker>(includeChildren)
				: inspector.InSameNamespaceAs<Acme.Services.Marker>(includeChildren);
			selector = selector.If(t => t != typeof(Acme.Services.Marker) && t != typeof(GlobalNamespaceMarker));
			var lifestyle = serviceSelection switch
			{
				0 => selector.WithServiceSelf(),
				1 => selector.WithServiceBase(),
				_ => selector.WithServiceAllInterfaces()
			};
			var descriptors = lifestyle.LifestyleSingleton().ToArray();
			var expected = global
				? includeChildren ? AllFixtures.Where(t => t != typeof(Acme.Services.Marker) && t != typeof(GlobalNamespaceMarker)).ToArray() : new[] { typeof(GlobalNamespaceService) }
				: includeChildren ? new[] { typeof(Acme.Services.ExactService), typeof(Acme.Services.Child.ChildService), typeof(Acme.Services.Child.Deep.DeepService) } : new[] { typeof(Acme.Services.ExactService) };
			AssertTypes(descriptors, expected);
			Assert.IsTrue(descriptors.All(d => d.ServiceType == (serviceSelection == 0 ? d.ImplementationType : typeof(IScanFixture))));
		}

		private static readonly Type[] AllFixtures =
		[
			typeof(GlobalNamespaceMarker), typeof(GlobalNamespaceService), typeof(Acme.Services.Marker), typeof(Acme.Services.ExactService),
			typeof(Acme.Services.Child.ChildService), typeof(Acme.Services.Child.Deep.DeepService), typeof(Acme.ServicesExtra.SiblingService), typeof(Acme.services.DifferentCaseService)
		];

		private static AssemblyInspector NewInspector()
		{
			var inspector = new AssemblyInspector();
			inspector.FromAssemblyContaining<GlobalNamespaceMarker>();
			return inspector;
		}

		private static ServiceDescriptor[] Finish(ILifestyleSelector selector, ServiceLifetime lifetime) => (lifetime switch
		{
			ServiceLifetime.Transient => selector.LifestyleTransient(),
			ServiceLifetime.Scoped => selector.LifestyleScoped(),
			_ => selector.LifestyleSingleton()
		}).ToArray();

		private static void AssertTypes(ServiceDescriptor[] descriptors, params Type[] expected)
		{
			CollectionAssert.AreEquivalent(expected, descriptors.Select(d => d.ImplementationType).ToArray());
		}

		private static void AssertGlobalOnly(ServiceDescriptor[] descriptors)
		{
			Assert.IsTrue(descriptors.Any(d => d.ImplementationType == typeof(GlobalNamespaceMarker)));
			Assert.IsTrue(descriptors.Any(d => d.ImplementationType == typeof(GlobalNamespaceService)));
			Assert.IsFalse(descriptors.Any(d => d.ImplementationType == typeof(AbstractGlobalNamespaceFixture)));
			Assert.IsTrue(descriptors.All(d => d.ImplementationType!.Namespace == null), "An exact global namespace filter must exclude namespaced types.");
		}
	}
}

namespace Mammoth.Extensions.DependencyInjection.Tests.NamespaceFixtures
{
	public interface IScanFixture { }
}

public class GlobalNamespaceMarker : IScanFixture { }
public class GlobalNamespaceService : IScanFixture { }
public abstract class AbstractGlobalNamespaceFixture : IScanFixture { }

namespace Acme.Services
{
	public class Marker : IScanFixture { }
	public class ExactService : IScanFixture { }
}
namespace Acme.Services.Child
{
	public class ChildService : IScanFixture { }
}
namespace Acme.Services.Child.Deep
{
	public class DeepService : IScanFixture { }
}
namespace Acme.ServicesExtra
{
	public class SiblingService : IScanFixture { }
}
namespace Acme.services
{
	public class DifferentCaseService : IScanFixture { }
}
