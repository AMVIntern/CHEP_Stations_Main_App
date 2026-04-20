namespace VisionApp.Core.Interfaces;

public interface IPlcStatusObserver
{
	/// <summary>
	/// Called when PLC health status changes. <paramref name="isHealthy"/> is true when
	/// the PLC acknowledged the heartbeat by resetting the tag to false within the ack window.
	/// </summary>
	Task OnPlcStatusChangedAsync(bool isHealthy, CancellationToken ct);
}
