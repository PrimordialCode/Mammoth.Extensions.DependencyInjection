namespace Mammoth.Cqrs.Infrastructure.Tests.Infrastructure
{
	public interface ITestService
	{
		bool WasDisposed();

		T? GetById<T>(string id) where T : class;
	}

	public interface IAnotherInterface;

	public class TestService : IDisposable, ITestService, IAnotherInterface
	{
		private bool disposedValue;

		public virtual bool WasDisposed()
		{
			return disposedValue;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects)
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~SingletonService()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		public virtual T? GetById<T>(string id) where T : class
		{
			return default;
		}
	}

	public class AnotherTestService : IDisposable, ITestService, IAnotherInterface
	{
		private bool disposedValue;

		public bool WasDisposed()
		{
			return disposedValue;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects)
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~SingletonService()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		public T? GetById<T>(string id) where T : class
		{
			return default;
		}
	}

	public interface IKeyedService;

	public class KeyedService1 : IKeyedService;

	public class KeyedService2 : IKeyedService;

	public class ExternalService;

	public class TestServiceConcreteDecorator : TestService
	{
		public TestService Inner { get; }

		public TestServiceConcreteDecorator(TestService inner)
		{
			Inner = inner;
		}

		public override T? GetById<T>(string id) where T : class
		{
			return Inner.GetById<T>(id);
		}

		public override bool WasDisposed()
		{
			return Inner.WasDisposed();
		}
	}

	namespace Nested
	{
		public interface INestedAnotherInterface ;

		public class NestedTransientService1 : IAnotherInterface;

		public class NestedTransientService2 : INestedAnotherInterface;

		namespace SubNamespace
		{
			public class SubNamespaceTransientService1 : INestedAnotherInterface;
		}
	}
}
