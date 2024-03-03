﻿using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure.Nested;
using Mammoth.Cqrs.Infrastructure.Tests.Infrastructure.Nested.Subnamespace;
using Mammoth.Extensions.DependencyInjection.Inspector;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class AssemblyInspectorTests
	{
		[TestMethod]
		public void ServiceDescriptors_BasedOn_WithServiceBase_Interface()
		{
			// Create service descriptors for all types implementing ITestService
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<ITestService>()
				.BasedOn(typeof(ITestService))
				.WithServiceBase()
				.LifestyleTransient();

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(5, descriptors.Count());
			Assert.IsTrue(descriptors.All(d => d.ServiceType == typeof(ITestService)));
			Assert.IsTrue(descriptors.Any(d => d.ImplementationType == typeof(TestService)));
			Assert.IsTrue(descriptors.Any(d => d.ImplementationType == typeof(AnotherTestService)));
			Assert.IsTrue(descriptors.Any(d => d.ImplementationType == typeof(TestServiceDecorator1)));
			Assert.IsTrue(descriptors.Any(d => d.ImplementationType == typeof(TestServiceDecorator2)));
			Assert.IsTrue(descriptors.Any(d => d.ImplementationType == typeof(TestServiceDecorator3)));
		}

		[TestMethod]
		public void ServiceDescriptors_BasedOn_WithServiceBase_Single_Concrete_Type()
		{
			// Create service descriptors for all types implementing TestService
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<TestService>()
				.BasedOn(typeof(TestService))
				.WithServiceBase()
				.LifestyleTransient();

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(1, descriptors.Count());
			var descriptor = descriptors.Single();
			Assert.IsTrue(descriptor.ServiceType == typeof(TestService));
			Assert.IsTrue(descriptor.ImplementationType == typeof(TestService));
		}

		[TestMethod]
		public void ServiceDescriptors_BasedOn_WithServiceAllInterfaces()
		{
			// Create service descriptors for all types implementing ITestService
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<TestService>()
				.BasedOn(typeof(ITestService))
				.WithServiceAllInterfaces()
				.LifestyleTransient();

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(7, descriptors.Count());
			// 5 descriptors should match ITransientService and 2 descriptors should match IAnotherInterface
			var iTransientServiceDescriptors = descriptors.Where(d => d.ServiceType == typeof(ITestService)).ToArray();
			Assert.AreEqual(5, iTransientServiceDescriptors.Length);
			Assert.IsTrue(iTransientServiceDescriptors.Any(d => d.ImplementationType == typeof(TestService)));
			Assert.IsTrue(iTransientServiceDescriptors.Any(d => d.ImplementationType == typeof(AnotherTestService)));
			Assert.IsTrue(iTransientServiceDescriptors.Any(d => d.ImplementationType == typeof(TestServiceDecorator1)));
			Assert.IsTrue(iTransientServiceDescriptors.Any(d => d.ImplementationType == typeof(TestServiceDecorator2)));
			Assert.IsTrue(iTransientServiceDescriptors.Any(d => d.ImplementationType == typeof(TestServiceDecorator3)));
			var iAnotherInterfaceDescriptors = descriptors.Where(d => d.ServiceType == typeof(IAnotherInterface)).ToArray();
			Assert.AreEqual(2, iAnotherInterfaceDescriptors.Length);
			Assert.IsTrue(iAnotherInterfaceDescriptors.Any(d => d.ImplementationType == typeof(TestService)));
			Assert.IsTrue(iAnotherInterfaceDescriptors.Any(d => d.ImplementationType == typeof(AnotherTestService)));
		}

		[TestMethod]
		public void ServiceDescriptors_BasedOn_WithServiceSelf()
		{
			// Create service descriptors for all types implementing ITestService
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<ITestService>()
				.BasedOn(typeof(ITestService))
				.WithServiceSelf()
				.LifestyleTransient();

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(5, descriptors.Count());
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(TestService)
				&& d.ImplementationType == typeof(TestService)));
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(AnotherTestService)
				&& d.ImplementationType == typeof(AnotherTestService)));
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(TestServiceDecorator1)
				&& d.ImplementationType == typeof(TestServiceDecorator1)));
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(TestServiceDecorator2)
				&& d.ImplementationType == typeof(TestServiceDecorator2)));
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(TestServiceDecorator3)
				&& d.ImplementationType == typeof(TestServiceDecorator3)));
		}

		[TestMethod]
		public void ServiceDescriptors_InSameNamespaceAs_WithServiceSelf()
		{
			// Create service descriptors for all types implementing ITestService
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<ITestService>()
				.InSameNamespaceAs<NestedTransientService1>()
				.WithServiceSelf()
				.LifestyleTransient();

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(2, descriptors.Count());
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(NestedTransientService1)
				&& d.ImplementationType == typeof(NestedTransientService1)));
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(NestedTransientService2)
				&& d.ImplementationType == typeof(NestedTransientService2)));
		}

		[TestMethod]
		public void ServiceDescriptors_InSameNamespaceAs_IncludeSubnamespaces_WithServiceSelf()
		{
			// Create service descriptors for all types implementing ITestService
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<ITestService>()
				.InSameNamespaceAs<NestedTransientService1>(includeSubnamespaces: true)
				.WithServiceSelf()
				.LifestyleTransient();

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(3, descriptors.Count());
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(NestedTransientService1)
				&& d.ImplementationType == typeof(NestedTransientService1)));
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(NestedTransientService2)
				&& d.ImplementationType == typeof(NestedTransientService2)));
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(SubnamespaceTransientService1)
				&& d.ImplementationType == typeof(SubnamespaceTransientService1)));
		}

		[TestMethod]
		public void ServiceDescriptors_If_WithServiceSelf()
		{
			// Create service descriptors for all types implementing ITestService
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<ITestService>()
				.If(t => t.Name == nameof(NestedTransientService1))
				.WithServiceSelf()
				.LifestyleTransient();

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(1, descriptors.Count());
			Assert.IsTrue(descriptors.Any(d => d.ServiceType == typeof(NestedTransientService1)
				&& d.ImplementationType == typeof(NestedTransientService1)));
		}

		[TestMethod]
		public void ServiceDescriptors_Cannot_Set_If_Twice()
		{
			Assert.ThrowsException<NotSupportedException>(() =>
			{
				var descriptors = new AssemblyInspector()
					.FromAssemblyContaining<ITestService>()
					.If(t => t.Name == nameof(NestedTransientService1))
					.If(t => t.Name == nameof(NestedTransientService2))
					.WithServiceSelf()
					.LifestyleTransient();
			});
		}

		/// <summary>
		/// AssebmlyInspector using dependsOn will create ServiceDescriptrs with ImplementationFactory
		/// that will be used to resolve all the configured parameters.
		/// </summary>
		[TestMethod]
		public void ServiceDescriptors_DependsOn()
		{
			var descriptors = new AssemblyInspector()
				.FromAssemblyContaining<TestService>()
				.BasedOn(typeof(TestService))
				.WithServiceBase()
				.LifestyleTransient(dependsOn: new Dependency[]
				{
					Parameter.ForKey("param").Eq("nonexisting")
				});

			Assert.IsNotNull(descriptors);
			Assert.AreEqual(1, descriptors.Count());
			var descriptor = descriptors.Single();
			Assert.IsTrue(descriptor.ServiceType == typeof(TestService));
			Assert.AreEqual(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient, descriptor.Lifetime);
			Assert.IsNull(descriptor.ImplementationType);
			Assert.IsNotNull(descriptor.ImplementationFactory);
		}
	}
}
