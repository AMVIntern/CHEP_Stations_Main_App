using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisionApp.Infrastructure.Inspection.DefectAssignment;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private const double ResetThresholdValue = 0.8;

    /// <summary>Live ProgramData config — must stay in sync with <c>App.xaml.cs</c> (<c>appsettings_s1.json</c>).</summary>
    private static readonly string AppSettingsPath = Path.Combine(
        @"C:\ProgramData\AMV\VisionApp\0.0.1\AppSettings",
        "appsettings_s2.json");

    /// <summary>
    /// Static mapping from raw config key to human-readable display name.
    /// Any key not present here falls back to replacing '_' with ' ' and title-casing.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownDisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Raised_Nail"] = "Raised Nail",
            ["Protruding_Nail"] = "Protruding Nail",
            ["Plastic"] = "Plastic",
            ["Staple"] = "Staple",
            ["FreeStandingNail"] = "Free Standing Nail",
            ["Holes"] = "Holes",
            ["Vertical_Cracks"] = "Vertical Cracks",
            ["Horizontal_Cracks"] = "Horizontal Cracks",
            ["Contamination"] = "Contamination",
        };

    private readonly IOptionsMonitor<Station5DefectAssignmentOptions> _optionsMonitor;
    private readonly ModalStore _modalStore;
    private readonly ILogger<SettingsViewModel> _logger;

    public ObservableCollection<DefectThresholdItem> PrimaryItems { get; } = new();
    public ObservableCollection<DefectThresholdItem> SecondaryItems { get; } = new();

    /// <summary>Primary then secondary rows — single list for the confidence UI.</summary>
    public ObservableCollection<DefectThresholdItem> AllThresholdRows { get; } = new();

    private bool _hasSecondaryModel;
    public bool HasSecondaryModel
    {
        get => _hasSecondaryModel;
        private set => SetProperty(ref _hasSecondaryModel, value);
    }

    private string _secondaryModelKey = string.Empty;
    public string SecondaryModelKey
    {
        get => _secondaryModelKey;
        private set => SetProperty(ref _secondaryModelKey, value);
    }

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (!SetProperty(ref _isSaving, value))
                return;
            SaveAsyncCommand.NotifyCanExecuteChanged();
            ResetCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Explicit commands so XAML and the designer always resolve bindings (MVVM Toolkit source-gen is not always visible to design-time).</summary>
    public IAsyncRelayCommand SaveAsyncCommand { get; }
    public IRelayCommand ResetCommand { get; }

    public SettingsViewModel(
        IOptionsMonitor<Station5DefectAssignmentOptions> optionsMonitor,
        ModalStore modalStore,
        ILogger<SettingsViewModel> logger)
    {
        _optionsMonitor = optionsMonitor;
        _modalStore = modalStore;
        _logger = logger;

        SaveAsyncCommand = new AsyncRelayCommand(SaveAsyncExecuteAsync, () => !IsSaving);
        ResetCommand = new RelayCommand(ResetToDefaultExecute, () => !IsSaving);

        LoadFromOptions();
    }

    /// <summary>
    /// Rebuilds rows from the bound <c>Station5DefectAssignment</c> options snapshot (<c>IOptionsMonitor.CurrentValue</c>).
    /// Discards unsaved slider/text edits. Values follow disk after configuration reload (save or <c>reloadOnChange</c>).
    /// </summary>
    private void LoadFromOptions()
    {
        if (IsSaving)
            return;

        // Clear the list the view binds to first so rows are not left pointing at removed primary/secondary items.
        AllThresholdRows.Clear();

        var opts = _optionsMonitor.CurrentValue;
        var defaultT = opts.DefaultThreshold;

        PrimaryItems.Clear();
        SecondaryItems.Clear();

        foreach (var label in opts.ClassLabels)
        {
            var threshold = opts.ClassThresholds.TryGetValue(label, out var t) ? t : defaultT;
            PrimaryItems.Add(new DefectThresholdItem(label, Resolve(label), threshold, isSecondaryModel: false));
        }

        if (opts.SecondaryModel is { } sec && sec.ClassLabels.Length > 0)
        {
            HasSecondaryModel = true;
            SecondaryModelKey = sec.Key;

            foreach (var label in sec.ClassLabels)
            {
                var threshold = sec.ClassThresholds.TryGetValue(label, out var t) ? t : defaultT;
                SecondaryItems.Add(new DefectThresholdItem(label, Resolve(label), threshold, isSecondaryModel: true));
            }
        }
        else
        {
            HasSecondaryModel = false;
            SecondaryModelKey = string.Empty;
        }

        foreach (var item in PrimaryItems)
            AllThresholdRows.Add(item);
        foreach (var item in SecondaryItems)
            AllThresholdRows.Add(item);
    }

    private static string Resolve(string key)
    {
        if (KnownDisplayNames.TryGetValue(key, out var known))
            return known;

        var words = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            w.Length == 0 ? w
            : char.ToUpper(w[0], CultureInfo.InvariantCulture) + w[1..].ToLower(CultureInfo.InvariantCulture)));
    }

    private void ResetToDefaultExecute()
    {
        if (IsSaving)
            return;

        foreach (var item in PrimaryItems)
            item.Threshold = ResetThresholdValue;

        foreach (var item in SecondaryItems)
            item.Threshold = ResetThresholdValue;
    }

    private async Task SaveAsyncExecuteAsync()
    {
        if (IsSaving) return;

        try
        {
            IsSaving = true;
            _logger.LogInformation("[Settings] Saving confidence thresholds to {Path}", AppSettingsPath);

            var primary = PrimaryItems.ToDictionary(i => i.ClassKey, i => i.Threshold);
            var secondary = SecondaryItems.ToDictionary(i => i.ClassKey, i => i.Threshold);

            await Task.Run(() => WriteThresholdsToJson(primary, secondary)).ConfigureAwait(true);

            _logger.LogInformation("[Settings] Save complete.");

            _modalStore.ShowMessage("Settings Saved",
                "Confidence thresholds have been updated.\nThey now apply to ongoing and future inspections.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Settings] Failed to save thresholds.");
            _modalStore.ShowMessage("Save Failed", $"Could not write settings:\n{ex.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static void WriteThresholdsToJson(
        Dictionary<string, double> primaryThresholds,
        Dictionary<string, double> secondaryThresholds)
    {
        var json = File.ReadAllText(AppSettingsPath);

        var doc = JsonNode.Parse(json, null, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        })!;

        var station = doc["Station5DefectAssignment"]
            ?? throw new InvalidOperationException("Station5DefectAssignment section not found in config.");

        PatchThresholdNode(station, "ClassThresholds", primaryThresholds);

        if (secondaryThresholds.Count > 0 && station["SecondaryModel"] is { } secondary)
            PatchThresholdNode(secondary, "ClassThresholds", secondaryThresholds);

        var updated = doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppSettingsPath, updated);
    }

    private static void PatchThresholdNode(JsonNode parent, string property, Dictionary<string, double> values)
    {
        if (parent[property] is not JsonObject obj)
        {
            obj = new JsonObject();
            parent[property] = obj;
        }

        foreach (var (key, value) in values)
            obj[key] = JsonValue.Create(Math.Round(value, 4));
    }
}
