namespace Mammoth.Extensions.DependencyInjection.Configuration
{
	/// <summary>
	/// A fluent interface to build the dependency array:
	/// Parameter.ForKey("eventStore").Eq("${PmEventStore}")
	/// </summary>
	public static class Parameter
	{
		/// <summary>
		/// A fluent interface to build the dependency array
		/// </summary>
		public class ParameterKey
		{
			private readonly string _parameterName;

			internal ParameterKey(string parameterName)
			{
				_parameterName = parameterName;
			}

			/// <summary>
			/// Create a dependency that will assign a service key to a constructor parameter
			/// </summary>
			public Dependency Eq(string value)
			{
				return new Dependency(Dependency.DependencyType.KeyedServices, _parameterName, value);
			}
		}

		/// <summary>
		/// Create a dependency that will assign a service key to a constructor parameter
		/// </summary>
		public static ParameterKey ForKey(string parameterName)
		{
			return new ParameterKey(parameterName);
		}
	}
}
