namespace Mammoth.Extensions.DependencyInjection.Configuration
{
	// DependsOn is an array of dependecies that will be used to select the implementation to use
	// syntax will be like:
	// Parameter.ForKey("eventStore").Eq("${PmEventStore}"),
	// Dependency.OnValue("undispatchedMessageHeader", "UndispatchedMessage_")

	/// <summary>
	/// A dependency that will be assigned to a constructor parameter
	/// </summary>
	public class Dependency
	{
		/// <summary>
		/// Dependency Type
		/// </summary>
		public enum DependencyType
		{
			/// <summary>
			/// Value will contain the service key to resolve
			/// </summary>
			KeyedServices,
			/// <summary>
			/// Value will contain the actual value to be assigned to the dependency
			/// </summary>
			Value
		}

		/// <summary>
		/// Dependency Type
		/// </summary>
		public DependencyType T { get; set; }

		/// <summary>
		/// Constructor parameter name
		/// </summary>
		public string ParameterName { get; set; }

		/// <summary>
		/// Dependency value, it can be a service key or a value
		/// depending on the DependencyType.
		/// </summary>
		public object Value { get; set; }

		internal Dependency(DependencyType t, string parameterName, object value)
		{
			T = t;
			ParameterName = parameterName;
			Value = value;
		}

		/// <summary>
		/// Create a dependency that will assign a value to a constructor parameter
		/// </summary>
		public static Dependency OnValue(string parameterName, object value)
		{
			return new Dependency(DependencyType.Value, parameterName, value);
		}
	}
}
