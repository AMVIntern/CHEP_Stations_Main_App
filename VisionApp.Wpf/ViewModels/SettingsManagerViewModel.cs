using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisionApp.Infrastructure.Inspection.DefectAssignment;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels;

public sealed partial class SettingsManagerViewModel : ObservableObject
{
	private readonly IOptionsMonitor<Station4DefectAssignmentOptions> _s4Monitor;
	private readonly IOptionsMonitor<Station5DefectAssignmentOptions> _s5Monitor;
	private readonly ILogger<SettingsManagerViewModel> _logger;
	private readonly ModalStore _modalStore;
	private readonly string _configFilePath;
	private bool _suppressAutoSave;
	private Dictionary<string, double> _baselineStation4 = new(StringComparer.OrdinalIgnoreCase);
	private Dictionary<string, double> _baselineStation5 = new(StringComparer.OrdinalIgnoreCase);

	public ObservableCollection<ClassThresholdEntry> Station4Classes { get; } = new();
	public ObservableCollection<ClassThresholdEntry> Station5Classes { get; } = new();

	[ObservableProperty] private string _statusMessage = string.Empty;

	public SettingsManagerViewModel(
		IOptionsMonitor<Station4DefectAssignmentOptions> s4Monitor,
		IOptionsMonitor<Station5DefectAssignmentOptions> s5Monitor,
		ModalStore modalStore,
		ILogger<SettingsManagerViewModel> logger)
	{
		_s4Monitor = s4Monitor;
		_s5Monitor = s5Monitor;
		_modalStore = modalStore;
		_logger = logger;

		const string ExternalSettingsDir = @"C:\ProgramData\AMV\VisionApp\0.0.1\AppSettings";
		_configFilePath = Path.Combine(ExternalSettingsDir, "appsettings_s4_s5.json");

		// Populate collections from persisted config at startup.
		RefreshFromFile();
	}

	/// <summary>
	/// Reloads threshold values from persisted external config, populating the collections.
	/// Called on startup and by the Reset command.
	/// </summary>
	private void RefreshFromFile()
	{
		_suppressAutoSave = true;
		DetachThresholdHandlers(Station4Classes);
		DetachThresholdHandlers(Station5Classes);
		Station4Classes.Clear();
		Station5Classes.Clear();

		var s4Opts = _s4Monitor.CurrentValue;
		var s5Opts = _s5Monitor.CurrentValue;

		var json = ReadConfigJson();
		var s4FromFile = json?["Station4DefectAssignment"]?.AsObject();
		var s5FromFile = json?["Station5DefectAssignment"]?.AsObject();

		static Dictionary<string, double> ReadThresholds(JsonObject? section)
		{
			var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			var thresholds = section?["ClassThresholds"]?.AsObject();
			if (thresholds == null)
				return map;

			foreach (var kvp in thresholds)
			{
				if (kvp.Value is not JsonValue valueNode)
					continue;

				if (valueNode.TryGetValue<double>(out var value))
					map[kvp.Key] = value;
			}

			return map;
		}

		static string[] ReadStringArray(JsonObject? section, string key)
		{
			var node = section?[key] as JsonArray;
			if (node == null)
				return Array.Empty<string>();

			return node.Select(x => x?.GetValue<string>() ?? string.Empty)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToArray();
		}

		static double ReadDefaultThreshold(JsonObject? section, double fallback)
		{
			if (section?["DefaultThreshold"] is JsonValue valueNode &&
				valueNode.TryGetValue<double>(out var value))
				return value;
			return fallback;
		}

		var s4FileThresholds = ReadThresholds(s4FromFile);
		var s5FileThresholds = ReadThresholds(s5FromFile);
		var s4DefaultThreshold = ReadDefaultThreshold(s4FromFile, s4Opts.DefaultThreshold);
		var s5DefaultThreshold = ReadDefaultThreshold(s5FromFile, s5Opts.DefaultThreshold);

		var s4Labels = ReadStringArray(s4FromFile, "ClassLabels");
		if (s4Labels.Length == 0)
			s4Labels = s4Opts.ClassLabels ?? Array.Empty<string>();

		var s4Ignore = ReadStringArray(s4FromFile, "IgnoreLabels");
		if (s4Ignore.Length == 0)
			s4Ignore = s4Opts.IgnoreLabels ?? Array.Empty<string>();

		// Populate Station4: ClassLabels minus IgnoreLabels
		var s4IgnoreSet = new HashSet<string>(s4Ignore, StringComparer.OrdinalIgnoreCase);
		foreach (var label in s4Labels)
		{
			if (s4IgnoreSet.Contains(label))
				continue; // Skip ignored labels

			var threshold = s4FileThresholds.TryGetValue(label, out var t) ? t : s4DefaultThreshold;
			AddEntryWithAutoSave(Station4Classes, new ClassThresholdEntry(label, threshold));
		}

		var s5Labels = ReadStringArray(s5FromFile, "ClassLabels");
		if (s5Labels.Length == 0)
			s5Labels = s5Opts.ClassLabels ?? Array.Empty<string>();

		// Populate Station5: all ClassLabels (Station5 has no IgnoreLabels)
		foreach (var label in s5Labels)
		{
			var threshold = s5FileThresholds.TryGetValue(label, out var t) ? t : s5DefaultThreshold;
			AddEntryWithAutoSave(Station5Classes, new ClassThresholdEntry(label, threshold));
		}

		CaptureBaselineFromCurrentCollections();

		StatusMessage = "Settings loaded from config";
		_suppressAutoSave = false;
	}

	private void DetachThresholdHandlers(IEnumerable<ClassThresholdEntry> entries)
	{
		foreach (var entry in entries)
			entry.PropertyChanged -= ThresholdEntry_PropertyChanged;
	}

	private void AddEntryWithAutoSave(ObservableCollection<ClassThresholdEntry> target, ClassThresholdEntry entry)
	{
		entry.PropertyChanged += ThresholdEntry_PropertyChanged;
		target.Add(entry);
	}

	private void CaptureBaselineFromCurrentCollections()
	{
		_baselineStation4 = Station4Classes
			.ToDictionary(x => x.ClassName, x => x.Threshold, StringComparer.OrdinalIgnoreCase);
		_baselineStation5 = Station5Classes
			.ToDictionary(x => x.ClassName, x => x.Threshold, StringComparer.OrdinalIgnoreCase);
	}

	private void ThresholdEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_suppressAutoSave)
			return;

		if (!string.Equals(e.PropertyName, nameof(ClassThresholdEntry.Threshold), StringComparison.Ordinal))
			return;

		SaveToFile(isAutoSave: true);
	}

	private JsonObject? ReadConfigJson()
	{
		try
		{
			if (!File.Exists(_configFilePath))
				return null;

			var jsonText = File.ReadAllText(_configFilePath);
			return JsonNode.Parse(jsonText)?.AsObject();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed reading config JSON at {Path}", _configFilePath);
			return null;
		}
	}

	[RelayCommand]
	private void Save()
	{
		SaveToFile(isAutoSave: false);
	}

	private void SaveToFile(bool isAutoSave)
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

			StatusMessage = isAutoSave
				? "Threshold updated and saved"
				: "Settings saved successfully. Changes are active for ongoing inspections.";
			_logger.LogInformation("Settings saved to {Path} (AutoSave={AutoSave})", _configFilePath, isAutoSave);

			if (!isAutoSave)
			{
				_modalStore.ShowMessage(
					"Settings Saved",
					"Confidence thresholds have been updated.\nThey now apply to ongoing and future inspections.");
			}
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
		_suppressAutoSave = true;
		try
		{
			foreach (var entry in Station4Classes)
			{
				if (_baselineStation4.TryGetValue(entry.ClassName, out var threshold))
					entry.Threshold = threshold;
			}

			foreach (var entry in Station5Classes)
			{
				if (_baselineStation5.TryGetValue(entry.ClassName, out var threshold))
					entry.Threshold = threshold;
			}
		}
		finally
		{
			_suppressAutoSave = false;
		}

		SaveToFile(isAutoSave: false);
		StatusMessage = "Settings reset to previous values";
	}
}
