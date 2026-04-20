namespace VisionApp.Infrastructure.Inspection.Pipeline;

public interface IInspectionStep
{
    string Name { get; }
    Task ExecuteAsync(InspectionContext context, CancellationToken ct);
}
