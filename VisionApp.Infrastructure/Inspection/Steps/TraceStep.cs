using Microsoft.Extensions.Logging;
using VisionApp.Infrastructure.Inspection.Pipeline;

namespace VisionApp.Infrastructure.Inspection.Steps;

public sealed class TraceStep : IInspectionStep
{
    public string Name { get; }
    private readonly ILogger<TraceStep> _logger;

    public TraceStep(string name, ILogger<TraceStep> logger)
    {
        Name = name;
        _logger = logger;
    }

    public Task ExecuteAsync(InspectionContext context, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Trace:{Step}] Cycle={CycleId} Key={Key} Items={ItemCount} WorkingImgInit={Img}",
            Name,
            context.CycleId,
            context.Key,
            context.Items.Count,
            context.WorkingImage?.IsInitialized() == true);

        return Task.CompletedTask;
    }
}
