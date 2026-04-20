namespace VisionApp.Infrastructure.Cameras.Halcon;

/// <summary>
/// Shared semaphore used to serialize HALCON open/close across cameras.
/// This prevents random driver/native deadlocks in some environments.
/// </summary>
public sealed class HalconOpenCloseGate
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
