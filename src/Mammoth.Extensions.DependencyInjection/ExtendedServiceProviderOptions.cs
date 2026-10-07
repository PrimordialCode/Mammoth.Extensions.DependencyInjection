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
		/// Requires <see cref="ServiceProviderOptions.ValidateOnBuild"/> to be true; otherwise provider creation throws <see cref="ArgumentException"/>.
		/// Native DI validates the original registrations before instrumentation, without activating services.
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
		/// When false, warnings use the root ILoggerFactory, if registered. Warning delivery is best-effort:
		/// logger activation or logging failures do not prevent returning the provider.
		/// Direct ILogger&lt;ServiceProviderFactory&gt; registrations are not used for these warnings.
		/// This setting is only relevant if <see cref="DetectIncorrectUsageOfTransientDisposables"/> is set to true.
		/// </summary>
		public bool ThrowOnOpenGenericTransientDisposable { get; set; }

		/// <summary>
		/// <para>
		/// Sometimes it's necessary to exclude some services from the detection of incorrect usage of transient disposables.
		/// </para>
		/// <para>
		/// Patterns match the full name of the registered public service type, including its private decorator layers.
		/// Dependencies registered under other service types are still checked unless separately excluded.
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
