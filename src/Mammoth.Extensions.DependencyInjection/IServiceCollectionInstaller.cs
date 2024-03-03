using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection
{
	/// <summary>
	/// Represents an installer for configuring services in the <see cref="IServiceCollection"/>.
	/// </summary>
	public interface IServiceCollectionInstaller
	{
		/// <summary>
		/// Installs the services into the specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="serviceCollection">The <see cref="IServiceCollection"/> to install the services into.</param>
		void Install(IServiceCollection serviceCollection);
	}
}
