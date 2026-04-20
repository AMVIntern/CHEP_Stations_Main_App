using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

public interface IInspectionObserver
{
	Task OnInspectionCompletedAsync(InspectionResult result, CancellationToken ct);
}
