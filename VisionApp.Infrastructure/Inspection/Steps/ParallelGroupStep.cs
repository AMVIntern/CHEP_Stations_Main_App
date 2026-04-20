using VisionApp.Infrastructure.Inspection.Pipeline;

namespace VisionApp.Infrastructure.Inspection.Steps;

public sealed class ParallelGroupStep : IInspectionStep
{
    public string Name { get; }
    private readonly IReadOnlyList<IInspectionStep> _steps;

    public ParallelGroupStep(string name, IEnumerable<IInspectionStep> steps)
    {
        Name = name;
        _steps = steps.ToList();
    }

    public Task ExecuteAsync(InspectionContext context, CancellationToken ct)
        => Task.WhenAll(_steps.Select(s => s.ExecuteAsync(context, ct)));
}