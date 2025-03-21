using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

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
		return obj is ServiceIdentifier serviceIdentifier && Equals(serviceIdentifier);
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
