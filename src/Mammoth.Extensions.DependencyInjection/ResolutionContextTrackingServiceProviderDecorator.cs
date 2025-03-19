using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

/*
 * Helper classes and decorators to track the services being resolved.
 * While detecting incorrect usage of transient disposables,
 * we need resolution context tracking to know if we are resolving a transient disposable service
 * from a Singleton object (which is allowed because the singleton objects belongs to the root scope).
 */

/// <summary>
/// Service identifier (implementation tool from dotnet repository).
/// </summary>
internal readonly struct ServiceIdentifier : IEquatable<ServiceIdentifier>
{
	public object? ServiceKey { get; }

	public Type ServiceType { get; }

	public ServiceIdentifier(Type serviceType)
	{
		ServiceType = serviceType;
	}

	public ServiceIdentifier(object? serviceKey, Type serviceType)
	{
		ServiceKey = serviceKey;
		ServiceType = serviceType;
	}

	public static ServiceIdentifier FromDescriptor(ServiceDescriptor serviceDescriptor)
		=> new ServiceIdentifier(serviceDescriptor.ServiceKey, serviceDescriptor.ServiceType);

	public static ServiceIdentifier FromServiceType(Type type) => new ServiceIdentifier(null, type);

	public bool Equals(ServiceIdentifier other)
	{
		if (ServiceKey == null && other.ServiceKey == null)
		{
			return ServiceType == other.ServiceType;
		}
		else if (ServiceKey != null && other.ServiceKey != null)
		{
			return ServiceType == other.ServiceType && ServiceKey.Equals(other.ServiceKey);
		}
		return false;
	}

	public override bool Equals(object? obj)
	{
		return obj is ServiceIdentifier && Equals((ServiceIdentifier)obj);
	}

	public override int GetHashCode()
	{
		if (ServiceKey == null)
		{
			return ServiceType.GetHashCode();
		}
		unchecked
		{
			return (ServiceType.GetHashCode() * 397) ^ ServiceKey.GetHashCode();
		}
	}

	public bool IsConstructedGenericType => ServiceType.IsConstructedGenericType;

	public ServiceIdentifier GetGenericTypeDefinition() => new ServiceIdentifier(ServiceKey, ServiceType.GetGenericTypeDefinition());

	public override string? ToString()
	{
		if (ServiceKey == null)
		{
			return ServiceType.ToString();
		}

		return $"({ServiceKey}, {ServiceType})";
	}
}

/// <summary>
/// Holds an AsyncLocal stack for types or markers being resolved. Provides access to the current stack of
/// ServiceIdentifiers.
/// </summary>
internal static class ResolutionContext
{
	// An AsyncLocal stack that holds the types (or keyed markers) currently being resolved.
	private static readonly AsyncLocal<Stack<ServiceIdentifier>?> _currentStack = new AsyncLocal<Stack<ServiceIdentifier>?>();

	/// <summary>
	/// Returns the current stack of ServiceIdentifier. If the stack is not initialized, it creates a new one.
	/// </summary>
	public static Stack<ServiceIdentifier> CurrentStack
	{
		get => _currentStack.Value ??= new Stack<ServiceIdentifier>();
	}
}

/// <summary>
/// Manages a service scope while tracking resolution context. It wraps an inner service scope and provides a decorated
/// service provider.
/// </summary>
public class ResolutionContextTrackingServiceScope : IServiceScope
{
	private readonly IServiceScope _innerScope;

	/// <summary>
	/// Initializes a service scope for tracking resolution contexts. It decorates the service provider for enhanced
	/// tracking functionality.
	/// </summary>
	public ResolutionContextTrackingServiceScope(IServiceScope innerScope)
	{
		_innerScope = innerScope;
		ServiceProvider = new ResolutionContextTrackingServiceProviderDecorator(_innerScope.ServiceProvider);
	}

	/// <inheritdoc/>
	public IServiceProvider ServiceProvider { get; }

	/// <inheritdoc/>
	public void Dispose()
	{
		_innerScope.Dispose();
		GC.SuppressFinalize(this);
	}
}

/// <summary>
/// Decorates an IServiceProvider to track service resolutions. It intercepts both keyed and non-keyed service requests
/// for tracking.
/// </summary>
public class ResolutionContextTrackingServiceProviderDecorator : IServiceProvider, IKeyedServiceProvider, IDisposable, IAsyncDisposable, IServiceScopeFactory
{
	internal readonly IServiceProvider InnerServiceProvider;

	/// <summary>
	/// Decorates an existing service provider to track resolution contexts.
	/// </summary>
	public ResolutionContextTrackingServiceProviderDecorator(IServiceProvider inner)
	{
		InnerServiceProvider = inner;
	}

	/// <summary>
	/// Intercepts non‐keyed resolutions.
	/// </summary>
	public object GetService(Type serviceType)
	{
		// Push the requested service type onto the resolution stack.
		ResolutionContext.CurrentStack.Push(ServiceIdentifier.FromServiceType(serviceType));
		try
		{
			return InnerServiceProvider.GetService(serviceType);
		}
		finally
		{
			ResolutionContext.CurrentStack.Pop();
		}
	}

	/// <summary>
	/// Intercepts keyed resolutions. The key is combined with the service type for tracking.
	/// </summary>
	public object? GetKeyedService(Type serviceType, object? serviceKey)
	{
		// Push a keyed resolution marker onto the resolution stack.
		ResolutionContext.CurrentStack.Push(new ServiceIdentifier(serviceKey, serviceType));
		try
		{
			// If the inner provider supports keyed resolution, use it.
			if (InnerServiceProvider is IKeyedServiceProvider keyedProvider)
			{
				return keyedProvider.GetKeyedService(serviceType, serviceKey);
			}
			else
			{
				// Fallback: use non-keyed resolution.
				return InnerServiceProvider.GetService(serviceType);
			}
		}
		finally
		{
			ResolutionContext.CurrentStack.Pop();
		}
	}

	/// <summary>
	/// Similar to GetKeyedService but throws if no service is found.
	/// </summary>
	public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
	{
		return GetKeyedService(serviceType, serviceKey)
			?? throw new InvalidOperationException($"No keyed service found for type {serviceType} with key {serviceKey}.");
	}

	/// <summary>
	/// Implements scope creation. The scope’s ServiceProvider is wrapped so that resolutions inside the scope are tracked.
	/// </summary>
	public IServiceScope CreateScope()
	{
		// Get the inner scope factory.
		if (InnerServiceProvider.GetService(typeof(IServiceScopeFactory)) is not IServiceScopeFactory scopeFactory)
		{
			throw new InvalidOperationException("The inner provider does not support scope creation.");
		}
		var innerScope = scopeFactory.CreateScope();
		return new ResolutionContextTrackingServiceScope(innerScope);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (InnerServiceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		if (InnerServiceProvider is IAsyncDisposable disposable)
		{
			return disposable.DisposeAsync();
		}
		return default;
	}
}