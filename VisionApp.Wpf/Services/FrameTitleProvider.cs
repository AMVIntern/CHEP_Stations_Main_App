using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Wpf.Models;


namespace VisionApp.Wpf.Services;

public interface IFrameTitleProvider
{
    string GetTitle(TriggerKey key);
}

public sealed class FrameTitleProvider : IFrameTitleProvider
{
    private readonly UiFrameTitlesSettings _ui;
    private readonly CameraStationsSettings _cameraStations;

    public FrameTitleProvider(
        IOptions<UiFrameTitlesSettings> uiOptions,
        IOptions<CameraStationsSettings> cameraStationsOptions)
    {
        _ui = uiOptions.Value;
        _cameraStations = cameraStationsOptions.Value;
    }

    public string GetTitle(TriggerKey key)
    {
        string fallback = key.ToString();

        if (_ui is null || !_ui.Enabled)
            return fallback;

        string stationKey = ResolveStationKey(key.CameraId);
        if (string.IsNullOrWhiteSpace(stationKey))
            return fallback;

        var stationCfg = _ui.Stations.FirstOrDefault(s =>
            s != null &&
            string.Equals(s.StationKey, stationKey, StringComparison.OrdinalIgnoreCase));

        if (stationCfg is null)
            return fallback;

        string joiner = string.IsNullOrWhiteSpace(_ui.Joiner) ? "-" : _ui.Joiner;

        string elementsPart = string.Empty;
        if (stationCfg.CameraElements.TryGetValue(key.CameraId, out var elements) &&
            elements != null && elements.Count > 0)
        {
            elementsPart = string.Join(joiner, elements.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        string bearerPart = string.Empty;
        string idxKey = key.Index.ToString();
        if (stationCfg.BearerByTriggerIndex.TryGetValue(idxKey, out var bearer) &&
            !string.IsNullOrWhiteSpace(bearer))
        {
            bearerPart = bearer;
        }

        if (string.IsNullOrWhiteSpace(elementsPart) && string.IsNullOrWhiteSpace(bearerPart))
            return fallback;

        if (string.IsNullOrWhiteSpace(bearerPart))
            return elementsPart;

        if (string.IsNullOrWhiteSpace(elementsPart))
            return bearerPart;

        return elementsPart + joiner + bearerPart;
    }

    private string ResolveStationKey(string cameraId)
    {
        if (_cameraStations?.Map != null &&
            _cameraStations.Map.TryGetValue(cameraId, out var stationKey) &&
            !string.IsNullOrWhiteSpace(stationKey))
        {
            return stationKey;
        }

        return string.Empty;
    }
}
