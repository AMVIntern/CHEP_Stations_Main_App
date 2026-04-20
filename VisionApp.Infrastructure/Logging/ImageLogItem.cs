using HalconDotNet;
using VisionApp.Core.Domain;

namespace VisionApp.Infrastructure.Logging;

/// <summary>
/// A single image log unit to be written to disk.
/// Ownership: this object owns the HImage and must dispose it after writing.
/// </summary>
public sealed record ImageLogItem(
    Guid CycleId,
    TriggerKey Key,
    DateTimeOffset CapturedAt,
    string GroupName,
    HImage Image
) : IDisposable
{
    public void Dispose()
    {
        if (Image is null)
            return;

        if (!Image.IsInitialized())
            return;

        Image.Dispose();
    }

    public override string ToString()
        => $"{CapturedAt:O}  Group={GroupName}  Cycle={CycleId}  Key={Key}";
}
