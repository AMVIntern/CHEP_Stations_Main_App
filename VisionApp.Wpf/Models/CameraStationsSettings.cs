using System;
using System.Collections.Generic;
using System.Text;

namespace VisionApp.Wpf.Models;

public sealed class CameraStationsSettings
{
    public const string SectionName = "CameraStations";
    public Dictionary<string, string> Map { get; set; } = new();
}
