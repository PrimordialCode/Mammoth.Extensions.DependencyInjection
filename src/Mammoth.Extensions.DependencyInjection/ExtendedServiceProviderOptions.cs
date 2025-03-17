﻿using Microsoft.Extensions.DependencyInjection;

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
	}
}
