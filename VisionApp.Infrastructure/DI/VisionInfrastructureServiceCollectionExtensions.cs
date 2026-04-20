using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace VisionApp.Infrastructure.DI;

public static class VisionInfrastructureServiceCollectionExtensions
{
	public static IServiceCollection AddVisionInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		const bool OFFLINE_MODE = true;

		services.AddImageLoggingModule(configuration);

		if (OFFLINE_MODE)
			services.AddOfflineIO(configuration);
		else
			services.AddOnlineIO(configuration);

		services.AddPlcOutboundModule(configuration);

		services.AddInferenceModule(configuration);
		services.AddInspectionModule(configuration);
		services.AddSinksModule();

		return services;
	}
}
