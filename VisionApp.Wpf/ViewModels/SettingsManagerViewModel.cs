using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisionApp.Infrastructure.Inspection.DefectAssignment;

namespace VisionApp.Wpf.ViewModels;

public sealed partial class SettingsManagerViewModel : ObservableObject
{
	private readonly IOptionsMonitor<Station4DefectAssignmentOptions> _s4Monitor;
	private readonly IOptionsMonitor<Station5DefectAssignmentOptions> _s5Monitor;
	private readonly ILogger<SettingsManagerViewModel> _logger;
	private readonly string _configFilePath;

	public ObservableCollection<ClassThresholdEntry> Station4Classes { get; } = new();
	public ObservableCollection<ClassThresholdEntry> Station5Classes { get; } = new();

	[ObservableProperty] private string _statusMessage = string.Empty;

	public SettingsManagerViewModel(
		IOptionsMonitor<Station4DefectAssignmentOptions> s4Monitor,
		IOptionsMonitor<Station5DefectAssignmentOptions> s5Monitor,
		ILogger<SettingsManagerViewModel> logger)
	{
		_s4Monitor = s4Monitor;
		_s5Monitor = s5Monitor;
		_logger = logger;

		const string ExternalSettingsDir = @"C:\ProgramData\AMV\VisionApp\0.0.1\AppSettings";
        _configFilePath = Path.Combine(ExternalSettingsDir, "appsettings_s2.json");

        // Populate collections from config at startup
        RefreshFromConfig();
	}

	/// <summary>
	/// Reloads threshold values from the current options, populating the collections.
	/// Called on startup and by the Reset command.
	/// </summary>
	private void RefreshFromConfig()
	{
		Station4Classes.Clear();
		Station5Classes.Clear();

		var s4Opts = _s4Monitor.CurrentValue;
		var s5Opts = _s5Monitor.CurrentValue;

		// Populate Station4: ClassLabels minus IgnoreLabels
		var s4IgnoreSet = new HashSet<string>(s4Opts.IgnoreLabels ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		foreach (var label in s4Opts.ClassLabels ?? Array.Empty<string>())
		{
			if (s4IgnoreSet.Contains(label))
				continue; // Skip ignored labels

			var threshold = s4Opts.ClassThresholds.TryGetValue(label, out var t) ? t : s4Opts.DefaultThreshold;
			Station4Classes.Add(new ClassThresholdEntry(label, threshold));
		}

		// Populate Station5: all ClassLabels (Station5 has no IgnoreLabels)
		foreach (var label in s5Opts.ClassLabels ?? Array.Empty<string>())
		{
			var threshold = s5Opts.ClassThresholds.TryGetValue(label, out var t) ? t : s5Opts.DefaultThreshold;
			Station5Classes.Add(new ClassThresholdEntry(label, threshold));
		}

		StatusMessage = "Settings loaded from config";
	}

	[RelayCommand]
	private void Save()
	{
		try
		{
			// Read the current config file
			if (!File.Exists(_configFilePath))
			{
				StatusMessage = "Error: Config file not found at " + _configFilePath;
				_logger.LogError("Config file not found: {Path}", _configFilePath);
				return;
			}

			var jsonText = File.ReadAllText(_configFilePath);
			var json = JsonNode.Parse(jsonText)?.AsObject();

			if (json == null)
			{
				StatusMessage = "Error: Failed to parse config file";
				_logger.LogError("Failed to parse JSON");
				return;
			}

			// Update Station4 ClassThresholds
			var s4Node = json["Station4DefectAssignment"]?.AsObject();
			if (s4Node != null)
			{
				var s4Thresholds = new JsonObject();
				foreach (var entry in Station4Classes)
					s4Thresholds[entry.ClassName] = JsonValue.Create(entry.Threshold);

				s4Node["ClassThresholds"] = s4Thresholds;
			}

			// Update Station5 ClassThresholds
			var s5Node = json["Station5DefectAssignment"]?.AsObject();
			if (s5Node != null)
			{
				var s5Thresholds = new JsonObject();
				foreach (var entry in Station5Classes)
					s5Thresholds[entry.ClassName] = JsonValue.Create(entry.Threshold);

				s5Node["ClassThresholds"] = s5Thresholds;
			}

			// Write back to file with formatting
			var options = new JsonSerializerOptions { WriteIndented = true };
			var updatedJson = json.ToJsonString(options);
			File.WriteAllText(_configFilePath, updatedJson);

			StatusMessage = "Settings saved successfully. Changes will take effect on next inspection cycle.";
			_logger.LogInformation("Settings saved to {Path}", _configFilePath);
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error saving settings: {ex.Message}";
			_logger.LogError(ex, "Failed to save settings");
		}
	}

	[RelayCommand]
	private void Reset()
	{
		RefreshFromConfig();
		StatusMessage = "Settings reset to current config values";
	}
}
