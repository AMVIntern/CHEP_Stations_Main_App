using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Cameras;
using VisionApp.Infrastructure.Cameras.Halcon;
using VisionApp.Infrastructure.Cameras.Offline;
using VisionApp.Infrastructure.Inference.YOLOX.Abstractions;
using VisionApp.Infrastructure.Inference.YOLOX.Core;
using VisionApp.Infrastructure.Inference.YOLOX.Options;
using VisionApp.Infrastructure.Inspection.Aggregation;
using VisionApp.Infrastructure.Inspection.Assignment;
using VisionApp.Infrastructure.Inspection.Composition;
using VisionApp.Infrastructure.Inspection.DefectAssignment;
using VisionApp.Infrastructure.Logging;
using VisionApp.Infrastructure.PlcOutbound;
using VisionApp.Infrastructure.Preprocessing;
using VisionApp.Infrastructure.Sinks;
using VisionApp.Infrastructure.Triggers;

namespace VisionApp.Infrastructure.DI;

internal static class VisionInfrastructureModules
{
	public static IServiceCollection AddImageLoggingModule(this IServiceCollection services, IConfiguration config)
	{
		services.AddSingleton(_ =>
		{
			var opts = new ImageLoggingOptions();
			config.GetSection(ImageLoggingOptions.SectionName).Bind(opts);
			return opts;
		});

		services.AddSingleton<ImageLogQueue>();
		services.AddHostedService<ImageLogWriterService>();
		services.AddSingleton<IImageLogger, HalconDiskImageLogger>();

		return services;
	}

	/// <summary>
	/// OFFLINE I/O module with folder replay cameras and dummy triggers
	/// </summary>
	public static IServiceCollection AddOfflineIO(this IServiceCollection services, IConfiguration config)
	{
		// Folder replay options
		services.AddSingleton(_ =>
		{
			var opts = new FolderReplayOptions();
			config.GetSection(FolderReplayOptions.SectionName).Bind(opts);
			return opts;
		});

		// Station 1 cameras
		services.AddSingleton<ICamera>(sp =>
		{
			var opts = sp.GetRequiredService<FolderReplayOptions>();
			var logger = sp.GetRequiredService<ILogger<FolderReplayCamera>>();
			return new FolderReplayCamera("Cam1", opts.S1Cam1Folder, opts, logger);
		});

		services.AddSingleton<ICamera>(sp =>
		{
			var opts = sp.GetRequiredService<FolderReplayOptions>();
			var logger = sp.GetRequiredService<ILogger<FolderReplayCamera>>();
			return new FolderReplayCamera("Cam2", opts.S1Cam2Folder, opts, logger);
		});

		services.AddSingleton<ICamera>(sp =>
		{
			var opts = sp.GetRequiredService<FolderReplayOptions>();
			var logger = sp.GetRequiredService<ILogger<FolderReplayCamera>>();
			return new FolderReplayCamera("Cam3", opts.S1Cam3Folder, opts, logger);
		});

		services.AddSingleton<ICamera>(sp =>
		{
			var opts = sp.GetRequiredService<FolderReplayOptions>();
			var logger = sp.GetRequiredService<ILogger<FolderReplayCamera>>();
			return new FolderReplayCamera("Cam4", opts.S1Cam4Folder, opts, logger);
		});

		// Dummy triggers
		services.AddSingleton<ITriggerSource, DummyTriggerSource>();

		services.AddSingleton<ICameraConnectionStatusHub, CameraConnectionStatusHub>();
		services.AddHostedService<OfflineCameraStatusBootstrapService>();

		return services;
	}

	public static IServiceCollection AddOnlineIO(this IServiceCollection services, IConfiguration config)
	{
		// PLC options (+ sanity log)
		services.AddSingleton(sp =>
		{
			var opts = new PlcTriggerOptions();
			config.GetSection(PlcTriggerOptions.SectionName).Bind(opts);

			var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PlcTriggersConfig");
			log.LogInformation("PlcTriggers bound: Enabled={Enabled} Gateway={Gateway} ReadDelayMs={Delay} Groups={Groups}",
				opts.Enabled, opts.Gateway, opts.ReadDelayMs, opts.Groups.Count);

			return opts;
		});

		services.AddSingleton<ITriggerSource, PlcTriggerSource>();

		// HALCON procedure options + engine
		services.AddSingleton(_ =>
		{
			var opts = new HalconProcedureOptions();
			config.GetSection(HalconProcedureOptions.SectionName).Bind(opts);
			return opts;
		});

		services.AddSingleton<HalconEngineProvider>();

		// HALCON camera options
		services.AddSingleton(_ =>
		{
			var opts = new HalconCameraOptions();
			config.GetSection(HalconCameraOptions.SectionName).Bind(opts);
			return opts;
		});

		services.AddSingleton<HalconOpenCloseGate>();
		services.AddSingleton<IFramegrabberFactory, HDevProcedureFramegrabberFactory>();

		// HALCON cameras from config
		services.AddSingleton<IEnumerable<ICamera>>(sp =>
		{
			var opts = sp.GetRequiredService<HalconCameraOptions>();
			if (!opts.Enabled)
				return Enumerable.Empty<ICamera>();

			var factory = sp.GetRequiredService<IFramegrabberFactory>();
			var gate = sp.GetRequiredService<HalconOpenCloseGate>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

			return opts.Cameras
				.Select(cfg => (ICamera)new HalconGigECamera(
					cfg,
					opts,
					factory,
					gate,
					loggerFactory.CreateLogger<HalconGigECamera>()))
				.ToList();
		});

		services.AddHostedService<HalconCameraHealthService>();
		services.AddSingleton<ICameraConnectionStatusHub, CameraConnectionStatusHub>();

		return services;
	}

	public static IServiceCollection AddInferenceModule(this IServiceCollection services, IConfiguration config)
	{
		// Bind YOLOX options from appsettings.json ("YoloX": {...})
		services.Configure<YoloXOptions>(config.GetSection("YoloX"));

		// Singleton engine that owns pools
		services.AddSingleton<IYoloXEngine, YoloXEngine>();

		return services;
	}

	public static IServiceCollection AddInspectionModule(this IServiceCollection services, IConfiguration config)
	{
		// --------------------------
		// Preprocess configuration
		// --------------------------
		services.AddOptions<PreprocessOptions>()
			.Bind(config.GetSection(PreprocessOptions.SectionName));

		// Camera name -> Station mapping
		services.AddOptions<CameraStationOptions>()
			.Bind(config.GetSection(CameraStationOptions.SectionName));

		services.AddSingleton<ICameraStationResolver, CameraStationResolver>();

		// Concrete preprocessors
		services.AddSingleton<GammaPreprocessor>();
		services.AddSingleton<PassThroughPreprocessor>();

		// Switchable wrapper used by FramePreprocessService
		services.AddSingleton<IFramePreprocessor, SwitchablePreprocessor>();

		// --------------------------
		// Inspection pipelines
		// --------------------------
		services.AddSingleton<IStationPipelineBuilder, Station4PipelineBuilder>();
		services.AddSingleton<IStationPipelineBuilder, Station5PipelineBuilder>();

		// Router + per-trigger runners
		services.AddSingleton<IInspectionRunner>(sp => InspectionBootstrapperV1.Build(sp));

		// --------------------------
		// Defect assignment (post-processing) + observer
		// --------------------------
		services.AddOptions<Station5DefectAssignmentOptions>()
			.Bind(config.GetSection(Station5DefectAssignmentOptions.SectionName));

		services.AddSingleton<IDefectElementLocator, ConfigDefectElementLocator>();

		// CSV options + concrete CSV sink
		services.AddOptions<Station5DefectReportCsvOptions>()
			.Bind(config.GetSection(Station5DefectReportCsvOptions.SectionName));

		services.AddSingleton<Station5DefectReportCsvSink>();

		// Fanout sink is the ONE sink used by the observer:
		// It calls CSV + PLC (PLC sink is registered in AddPlcOutboundModule)
		services.AddSingleton<FanoutStationDefectRowSink>();
		services.AddSingleton<IStationDefectRowSink>(sp => sp.GetRequiredService<FanoutStationDefectRowSink>());

		// Accumulator + observer that listens to InspectionResult stream
		services.AddSingleton<StationDefectAccumulator>();
		services.AddSingleton<IInspectionObserver, Station5DefectAssignmentObserver>();

		// Shift resolver
		services.AddOptions<ShiftOptions>()
			.Bind(config.GetSection(ShiftOptions.SectionName));

		services.AddSingleton<IShiftResolver, DefaultShiftResolver>();

		// --------------------------
		// Station 4 defect assignment + Station 4 CSV
		// --------------------------
		services.AddOptions<Station4DefectAssignmentOptions>()
			.Bind(config.GetSection(Station4DefectAssignmentOptions.SectionName));

		services.AddOptions<Station4DefectReportCsvOptions>()
			.Bind(config.GetSection(Station4DefectReportCsvOptions.SectionName));

		services.AddSingleton<Station4DefectReportCsvSink>();

		// Add Station4 observer (runs alongside Station5 observer)
		services.AddSingleton<IInspectionObserver, Station4DefectAssignmentObserver>();


		return services;
	}

	public static IServiceCollection AddPlcOutboundModule(this IServiceCollection services, IConfiguration config)
	{
		// Options
		services.AddOptions<PlcOutboundOptions>()
			.Bind(config.GetSection(PlcOutboundOptions.SectionName));

		// PalletID store (populated by trigger source, consumed by defect sink)
		services.AddSingleton<IPalletIdStore, PalletIdStore>();

		// PLC write queue (BackgroundService) + interface
		services.AddSingleton<PlcWriteQueueService>();
		services.AddSingleton<IPlcWriteQueue>(sp => sp.GetRequiredService<PlcWriteQueueService>());
		services.AddHostedService(sp => sp.GetRequiredService<PlcWriteQueueService>());

		// Heartbeat toggler (BackgroundService)
		services.AddHostedService<PlcHeartbeatService>();

		// PLC sink used by FanoutStationDefectRowSink (concrete only)
		services.AddSingleton<PlcStationDefectBoolSink>();

		return services;
	}

	public static IServiceCollection AddSinksModule(this IServiceCollection services)
	{
		services.AddSingleton<IResultSink, NullResultSink>();
		return services;
	}
}
