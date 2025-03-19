using System.Reflection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Provides extension methods for IServiceProvider to access internal properties.
	/// Includes methods to retrieve the IsScope property and Disposables collection.
	/// </summary>
	public static partial class ServiceProviderExtensions
	{
		/// <summary>
		/// Retrieves the service provider from a given instance, potentially accessing a private 'Root' property.
		/// This is a very fragile way to obtain internal service provider implementation detail for testing purposes.
		/// Look at the internal ServiceProvider implementation:
		/// https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceProvider.cs
		/// </summary>
		/// <returns>Returns the service provider, which may be the original or the root service provider if accessible.</returns>
		private static IServiceProvider GetServiceProviderEngineScope(IServiceProvider serviceProvider)
		{
			// if it's a TrackerServiceProvider, we need to get the ServiceProvider from it
			var sp = serviceProvider;
			if (sp is ResolutionContextTrackingServiceProviderDecorator trackingServiceProvider)
			{
				sp = trackingServiceProvider.InnerServiceProvider;
			}
			var rootProperty = sp.GetType().GetProperty("Root",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (rootProperty != null)
			{
				sp = (IServiceProvider)rootProperty.GetValue(sp)!;
			}
			return sp;
		}

		/// <summary>
		/// Gets the value of the internal IsScope property from a ServiceProvider
		/// </summary>
		internal static bool GetIsRootScope(this IServiceProvider serviceProvider)
		{
			// if it has a Root "property" that will be the root scope (an instance of ServiceProviderEngineScope)
			// otherwise it's already an instance of ServiceProviderEngineScope and it's a scope.
			var sp = GetServiceProviderEngineScope(serviceProvider);

			// Get the IsScope property using reflection
			var isScopeProperty = sp.GetType().GetProperty("IsRootScope",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			if (isScopeProperty != null)
			{
				return (bool)isScopeProperty.GetValue(sp);
			}

			throw new InvalidOperationException("Internal implementation of ServiceProvider changed: cannot access IsRootScope property.");
		}

		/// <summary>
		/// Gets the internal Disposables collection from a ServiceProvider
		/// </summary>
		internal static IEnumerable<object> GetDisposables(this IServiceProvider serviceProvider)
		{
			// if it has a Root "property" that will be the root scope (an instance of ServiceProviderEngineScope)
			// otherwise it's already an instance of ServiceProviderEngineScope and it's a scope.
			var sp = GetServiceProviderEngineScope(serviceProvider);

			/*
			// Get the Disposables field using reflection
			var disposablesField = sp.GetType().GetField("_disposables",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (disposablesField != null)
			{
				var disposables = disposablesField.GetValue(sp);
				if (disposables is IEnumerable<object> disposablesList)
				{
					return disposablesList;
				}
			}
			*/

			// Check alternative field names (since internal implementation might vary)
			var disposablesField = sp.GetType().GetProperty("Disposables",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (disposablesField != null)
			{
				var disposables = disposablesField.GetValue(sp);
				if (disposables is IEnumerable<object> disposablesList)
				{
					return disposablesList;
				}
			}

			throw new InvalidOperationException("Internal implementation of ServiceProvider changed: cannot access Disposable property.");
		}
	}
}
