using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using VisionApp.Core.Hosting;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.DI;
using VisionApp.Wpf.Models;
using VisionApp.Wpf.Services;
using VisionApp.Wpf.Stores;
using VisionApp.Wpf.ViewModels;

namespace VisionApp.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            const string ExternalSettingsDir = @"C:\ProgramData\AMV\VisionApp\0.0.1\AppSettings";
            const string LogsDir = @"C:\ProgramData\AMV\VisionApp\0.0.1\Logs";
            const string SettingsFileName = "appsettings_s4_s5.json";
            var externalSettingsPath = Path.Combine(ExternalSettingsDir, SettingsFileName);

            // Ensure logs directory exists
            try
            {
                Directory.CreateDirectory(LogsDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create logs directory: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Seed external settings file from the bundled default if it doesn't exist yet.
            if (!File.Exists(externalSettingsPath))
            {
                Directory.CreateDirectory(ExternalSettingsDir);
                var bundledPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
                if (File.Exists(bundledPath))
                    File.Copy(bundledPath, externalSettingsPath);
            }

            // Configure Serilog for file logging
            var logFilePath = Path.Combine(LogsDir, "VisionApp-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: null,
                    retainedFileCountLimit: 30)
                .CreateLogger();

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Bundled appsettings.json (shipped with the app) — base/defaults.
                    config.AddJsonFile(SettingsFileName, optional: false, reloadOnChange: false);

                    // External appsettings.json (ProgramData) — overrides bundled values.
                    // This is the live config file operators should edit.
                    config.AddJsonFile(externalSettingsPath, optional: false, reloadOnChange: true);
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddSerilog(Log.Logger);
                    logging.AddDebug(); // also show in Visual Studio Output window
                })
                .ConfigureServices((context, services) =>
                {
                    // Core pipeline
                    services.AddVisionCore();

                    // Dummy infrastructure (PLC/HALCON later)
                    services.AddVisionInfrastructure(context.Configuration);

                    // -------------------------
                    // WPF UI plumbing
                    // -------------------------

                    // WPF Dispatcher (current application dispatcher)
                    services.AddSingleton<Dispatcher>(_ => Current.Dispatcher);

                    // Bind UI-only title config + camera->station mapping (both are in Models)
                    services.Configure<UiFrameTitlesSettings>(
                        context.Configuration.GetSection(UiFrameTitlesSettings.SectionName));

                    services.Configure<CameraStationsSettings>(
                        context.Configuration.GetSection(CameraStationsSettings.SectionName));

                    services.Configure<UiSecuritySettings>(
                        context.Configuration.GetSection(UiSecuritySettings.SectionName));

                    // Title provider
                    services.AddSingleton<IFrameTitleProvider, FrameTitleProvider>();

                    // Store that holds tiles for the current cycle
                    services.AddSingleton<CycleFramesStore>();

                    // Modal Store
                    services.AddSingleton<ModalStore>();

                    // Production counter sink (tracks per-shift metrics)
                    services.AddSingleton<IResultSink, ShiftProductionCounterSink>();

                    // Observer to push frames into the store (called by CameraDispatcherService)
                    services.AddSingleton<IFrameObserver, SmartWindowFrameObserver>();
					services.AddSingleton<IInspectionObserver, SmartWindowInspectionObserver>();

                    // Camera connection observer service
					services.AddSingleton<CameraConnectionStore>();
					services.AddHostedService<SmartWindowCameraConnectionObserver>();

                    // PLC health status store + observer
                    services.AddSingleton<PlcStatusStore>();
                    services.AddSingleton<IPlcStatusObserver, SmartWindowPlcStatusObserver>();

                    // Production metrics store
                    services.AddSingleton<ProductionCounterStore>();

                    // Footer status VM (uses CameraConnectionStore + ProductionCounterStore)
                    services.AddSingleton<StatusFooterViewModel>();

                    // Image viewer service
                    services.AddSingleton<ImageViewerService>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<SettingsManagerViewModel>();
                    services.AddSingleton<ShellViewModel>();

                    services.AddSingleton<NavigationStateService>();
                    services.AddSingleton<NavigationBarViewModel>();

                    services.AddSingleton<MainWindowViewModel>();

                    services.AddSingleton<MainWindow>(sp =>
                        new MainWindow(sp.GetRequiredService<MainWindowViewModel>())
                    );

                })
                .Build();

            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                    _host = null;
                }
            }
            finally
            {
                Log.CloseAndFlush();
                base.OnExit(e);
            }
        }
    }
}
