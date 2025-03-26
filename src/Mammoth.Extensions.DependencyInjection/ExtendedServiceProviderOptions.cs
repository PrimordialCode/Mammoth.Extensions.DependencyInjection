using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Extends ServiceProviderOptions:
	/// - Include a property for detecting incorrect usage of transient disposables.
	///   This helps ensure proper resource management and avoid memory leaks
	/// </summary>
	public class ExtendedServiceProviderOptions : ServiceProviderOptions
	{
		/// <summary>
		/// <para>
		/// Indicates whether incorrect usage of transient disposables is detected.
		/// If set to true, the service provider will throw an exception if a transient disposable is resolved from the root scope.
		/// </para>
		/// <para>
		/// WARNING: use this setting only in debug build as it "patches" the service collection and uses
		/// reflection to access internal properties of the service provider.
		/// </para>
		/// </summary>
		public bool DetectIncorrectUsageOfTransientDisposables { get; set; }

		/// <summary>
		/// Indicates whether a singleton can resolve transient disposable objects.
		/// This setting is only relevant if <see cref="DetectIncorrectUsageOfTransientDisposables"/> is set to true.
		/// </summary>
		public bool AllowSingletonToResolveTransientDisposables { get; set; }

		/// <summary>
		/// Throw an exception if a open generic transient disposable is registered.
		/// Open Generic cannot be tracked, so it's better to use only closed type registrations.
		/// This setting is only relevant if <see cref="DetectIncorrectUsageOfTransientDisposables"/> is set to true.
		/// </summary>
		public bool ThrowOnOpenGenericTransientDisposable { get; set; }

		/// <summary>
		/// <para>
		/// Sometimes it's necessary to exclude some services from the detection of incorrect usage of transient disposables.
		/// </para>
		/// <para>
		/// We can specify some patterns to exclude services from the detection. All the services (ServiceType) that matches any of the patterns will be excluded from the check.
		/// </para>
		/// <para>
		/// Some AspNetCore services are registered as transient disposables, but they are managed by the framework.
		/// </para>
		/// </summary>
		/// <remarks>
		/// These might be potential memory leaks and should be reviewed carefully.
		/// </remarks>
		public IEnumerable<string>? DetectIncorrectUsageOfTransientDisposablesExclusionPatterns { get; set; }
	}
}
