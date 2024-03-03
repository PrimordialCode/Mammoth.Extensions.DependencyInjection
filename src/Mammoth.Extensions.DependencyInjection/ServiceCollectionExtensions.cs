using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Provides extension methods for the <see cref="IServiceCollection"/> interface.
	/// </summary>
	public static partial class ServiceCollectionExtensions
	{
		/// <summary>
		/// Installs the services into the specified <see cref="IServiceCollection"/> using the provided installers.
		/// </summary>
		/// <param name="serviceCollection">The <see cref="IServiceCollection"/> to install the services into.</param>
		/// <param name="installers">The installers that configure the services.</param>
		public static void Install(this IServiceCollection serviceCollection, params IServiceCollectionInstaller[] installers)
		{
			foreach (var item in installers)
			{
				item.Install(serviceCollection);
			}
		}
	}
}
