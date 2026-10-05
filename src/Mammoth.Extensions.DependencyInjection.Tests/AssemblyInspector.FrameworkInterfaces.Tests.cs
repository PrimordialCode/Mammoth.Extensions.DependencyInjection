using System.Reflection;
using System.Reflection.Emit;
using Mammoth.Extensions.DependencyInjection.Inspector;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class AssemblyInspectorFrameworkInterfaceTests
	{
		[TestMethod]
		[DataRow("Systematic.Contracts", "Application.Contracts", false)]
		[DataRow("System.Contracts", "Application.Contracts", false)]
		[DataRow("Application.mscorlib.Contracts", "Application.Contracts", false)]
		[DataRow("mscorlib.Contracts", "Application.Contracts", false)]
		[DataRow("Mscorlib", "Application.Contracts", false)]
		[DataRow("Ordinary.Contracts", "System", true)]
		[DataRow("Ordinary.Contracts", "System.Contracts", true)]
		[DataRow("Ordinary.Contracts", "System.Contracts.Child", true)]
		[DataRow("Ordinary.Contracts", "Systematic.Contracts", false)]
		[DataRow("Ordinary.Contracts", "Systems", false)]
		[DataRow("Ordinary.Contracts", "system.Contracts", false)]
		[DataRow("Ordinary.Contracts", "Application.System", false)]
		[DataRow("Ordinary.Contracts", "", false)]
		[DataRow("mscorlib", "Application.Contracts", true)]
		public void FrameworkPolicyUsesNamespaceBoundariesAndExactAssemblyName(string assemblyName, string typeNamespace, bool excluded)
		{
			var (contract, implementation) = CreateFixture(assemblyName, typeNamespace);
			Assert.AreEqual(assemblyName, contract.Assembly.GetName().Name);
			Assert.AreEqual(excluded, contract.IsFrameworkType());

			var descriptors = new AssemblyInspector().FromAssembly(implementation.Assembly)
				.BasedOn(contract).WithServiceAllInterfaces().LifestyleTransient().ToArray();
			Assert.AreEqual(excluded ? 0 : 1, descriptors.Length);
			if (!excluded)
			{
				Assert.AreEqual(contract, descriptors[0].ServiceType);
				Assert.AreEqual(implementation, descriptors[0].ImplementationType);
			}
		}

		[TestMethod]
		public void DisposableInterfacesAreExcludedRegardlessOfTheirDefiningAssembly()
		{
#if NETFRAMEWORK
			Assert.AreEqual("Microsoft.Bcl.AsyncInterfaces", typeof(IAsyncDisposable).Assembly.GetName().Name);
#else
			Assert.AreEqual("System.Private.CoreLib", typeof(IAsyncDisposable).Assembly.GetName().Name);
#endif
			Assert.IsTrue(typeof(IDisposable).IsFrameworkType());
			Assert.IsTrue(typeof(IAsyncDisposable).IsFrameworkType());
			var descriptors = new AssemblyInspector().FromAssemblyContaining<DisposableService>()
				.BasedOn<IApplicationContract>().If(t => t == typeof(DisposableService))
				.WithServiceAllInterfaces().LifestyleSingleton().ToArray();
			Assert.HasCount(1, descriptors);
			Assert.AreEqual(typeof(IApplicationContract), descriptors[0].ServiceType);
			Assert.AreEqual(typeof(DisposableService), descriptors[0].ImplementationType);
		}

		[TestMethod]
		public void ExplicitBaseSelectionCanStillRegisterFrameworkInterfaces()
		{
			var descriptors = new AssemblyInspector().FromAssemblyContaining<DisposableService>()
				.BasedOn<IAsyncDisposable>().If(t => t == typeof(DisposableService))
				.WithServiceBase().LifestyleScoped().ToArray();
			Assert.HasCount(1, descriptors);
			Assert.AreEqual(typeof(IAsyncDisposable), descriptors[0].ServiceType);
			Assert.AreEqual(ServiceLifetime.Scoped, descriptors[0].Lifetime);
		}

		[TestMethod]
		public void SelfSelectionCanStillRegisterClassesImplementingFrameworkInterfaces()
		{
			var descriptors = new AssemblyInspector().FromAssemblyContaining<DisposableService>()
				.BasedOn<IAsyncDisposable>().If(t => t == typeof(DisposableService))
				.WithServiceSelf().LifestyleTransient().ToArray();
			Assert.HasCount(1, descriptors);
			Assert.AreEqual(typeof(DisposableService), descriptors[0].ServiceType);
		}

		private static (Type Contract, Type Implementation) CreateFixture(string assemblyName, string typeNamespace)
		{
#if NETFRAMEWORK
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
#else
			var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
#endif
			var module = assembly.DefineDynamicModule("Contracts");
			var prefix = typeNamespace.Length == 0 ? "" : typeNamespace + ".";
			var contract = module.DefineType(prefix + "IApplicationService", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract).CreateTypeInfo()!.AsType();
			var builder = module.DefineType("Application.Implementation.Service", TypeAttributes.Public | TypeAttributes.Class);
			builder.AddInterfaceImplementation(contract);
			builder.DefineDefaultConstructor(MethodAttributes.Public);
			return (contract, builder.CreateTypeInfo()!.AsType());
		}

		public interface IApplicationContract { }

		public class DisposableService : IApplicationContract, IDisposable, IAsyncDisposable
		{
			public void Dispose() { }
			public ValueTask DisposeAsync() => default;
		}
	}
}
