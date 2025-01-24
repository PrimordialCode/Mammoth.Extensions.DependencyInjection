namespace Mammoth.Extensions.DependencyInjection.Configuration
{
	/// <summary>
	/// Represents a service registration that allows specifying service properties and dependency.
	/// </summary>
	public class ServiceRegistration
	{
		/// <summary>
		/// Optional service key.
		/// </summary>
		public object? ServiceKey { get; set; }

		/// <summary>
		/// Optional service dependencies.
		/// </summary>
		public Dependency[]? DependsOn { get; set; }
	}
}
