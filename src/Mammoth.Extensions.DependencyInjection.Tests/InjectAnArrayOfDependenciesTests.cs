using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests
{
	[TestClass]
	public class InjectAnArrayOfDependenciesTests
	{
		[TestMethod]
		public void IEnumerable_is_resolved_as_an_Array()
		{
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddTransient<IService, ServiceA>();
			serviceCollection.AddTransient<IService, ServiceB>();
			serviceCollection.AddTransient<MyConsumer>();
			using var sp = serviceCollection.BuildServiceProvider();

			var consumer = sp.GetService<MyConsumer>();
			Assert.IsNotNull(consumer);
			Assert.IsTrue(consumer.GetIEnumerableType() == typeof(IService[]));
		}

		public interface IService
		{
			void Execute();
		}

		public class ServiceA : IService
		{
			public void Execute()
			{
				Console.WriteLine("Executing ServiceA");
			}
		}

		public class ServiceB : IService
		{
			public void Execute()
			{
				Console.WriteLine("Executing ServiceB");
			}
		}

		public class MyConsumer
		{
			private readonly IEnumerable<IService> _services;

			public MyConsumer(IEnumerable<IService> services)
			{
				_services = services;
			}

			/***
			 * Dependency Resolution Exception.
			 * 
			 * Unable to resolve service for type 'Mammoth.Extensions.DependencyInjection.Tests.ArrayOfDependencyResolutionTests+IService[]' 
			 * while attempting to activate 'Mammoth.Extensions.DependencyInjection.Tests.ArrayOfDependencyResolutionTests+MyConsumer'.
			 * 
			 * Microsoft.Extensions.DependencyInjection is not able to resolve an array of dependencies,
			 * but it will work with IEnumerable<T>.
			 * 
			 * It's worth noting that the ServiceProvider will inject an 'IService[]'
			 * (an array) into the IEnumerable<T> constructor parameter.
			 * 
			public MyConsumer(IService[] services)
			{
				_services = services;
			}
			*/

			public void ExecuteAll()
			{
				foreach (var service in _services)
				{
					service.Execute();
				}
			}

			public Type GetIEnumerableType()
			{
				return _services.GetType();
			}
		}
	}
}
