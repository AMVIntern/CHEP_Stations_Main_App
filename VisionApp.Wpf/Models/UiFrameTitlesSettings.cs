using System;
using System.Collections.Generic;
using System.Text;

namespace VisionApp.Wpf.Models;

    public sealed class UiFrameTitlesSettings
{
    public const string SectionName = "UiFrameTitles";
    public bool Enabled { get; set; } = true;
    public string Joiner { get; set; } = "-";
    public List<UiStationFrameTitleSettings> Stations { get; set; } = new List<UiStationFrameTitleSettings>();
}

public sealed class UiStationFrameTitleSettings
{
    public string StationKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Dictionary<string, string> BearerByTriggerIndex { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, List<string>> CameraElements { get; set; } = new Dictionary<string, List<string>>();
}
