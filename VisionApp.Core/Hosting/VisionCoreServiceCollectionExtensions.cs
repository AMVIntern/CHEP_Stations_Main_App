using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using VisionApp.Core.Domain;
using VisionApp.Core.Engine;
using VisionApp.Core.Hosting.Services;

namespace VisionApp.Core.Hosting;

public static class VisionCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core components:
    /// - Channels for the pipeline
    /// - CapturePlan + CycleEngine
    /// - Hosted services that run the pipeline
    /// </summary>
    public static IServiceCollection AddVisionCore(this IServiceCollection services)
    {
        // -------------------------
        // Channels (Singletons)
        // -------------------------
        services.AddSingleton(Channel.CreateUnbounded<TriggerEvent>());
        services.AddSingleton(Channel.CreateUnbounded<CaptureRequest>());
        services.AddSingleton(Channel.CreateUnbounded<InspectionResult>());
        services.AddSingleton(Channel.CreateBounded<RawFrameArrived>(
            new BoundedChannelOptions(30) { FullMode = BoundedChannelFullMode.Wait }));
        services.AddSingleton(Channel.CreateBounded<FrameArrived>(
            new BoundedChannelOptions(30) { FullMode = BoundedChannelFullMode.Wait }));

        // -------------------------
        // Plan + Engine
        // -------------------------
        services.AddSingleton<CapturePlan>(_ => CapturePlanFactory.CreateTwoCameraPlan());
        services.AddSingleton<CycleEngine>();

        // -------------------------
        // Hosted pipeline services
        // -------------------------
        services.AddHostedService<TriggerPumpService>();
        services.AddHostedService<CycleEngineService>();
        services.AddHostedService<CameraDispatcherService>();
        services.AddHostedService<FramePreprocessService>();
        services.AddHostedService<InspectionService>();

        return services;
    }
}
