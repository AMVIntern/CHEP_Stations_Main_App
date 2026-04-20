using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

/// <summary>
/// Allows non-core layers (e.g., WPF) to observe frames as they flow through the pipeline,
/// without coupling Core to UI.
/// 
/// Important: Observers must NOT assume they can keep the original frame.Image alive,
/// because the pipeline may dispose it later. If needed, observers should clone (CopyImage).
/// </summary>
public interface IFrameObserver
{
    Task OnFrameArrivedAsync(FrameArrived frame, CancellationToken ct);
}
