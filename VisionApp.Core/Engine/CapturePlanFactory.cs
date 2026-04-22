using VisionApp.Core.Domain;

namespace VisionApp.Core.Engine;

/// <summary>
/// Temporary v1 factory for building a CapturePlan in code.
/// Later we'll replace this with recipe.json loading (IRecipeProvider).
/// </summary>
public static class CapturePlanFactory
{
    public static CapturePlan CreateTwoCameraPlan()
    {
        var ordered = new List<TriggerKey>
        {
            new("Cam1", 1),
            new("Cam2", 1),
            new("Cam3", 1),
            new("Cam4", 1),
            new("Cam1", 2),
            new("Cam2", 2),
            new("Cam3", 2),
            new("Cam4", 2),
            new("Cam1", 3),
            new("Cam2", 3),
            new("Cam3", 3),
            new("Cam4", 3),
            new("Cam1", 4),
            new("Cam2", 4),
            new("Cam3", 4),
            new("Cam4", 4),
            new("Cam1", 5),
            new("Cam2", 5),
            new("Cam3", 5),
            new("Cam4", 5),
        };

        var plan = new CapturePlan
        {
            OrderedTriggers = ordered,
            StartTriggers = new HashSet<TriggerKey>
            {
                new("Cam1", 1),
                new("Cam2", 1),
                new("Cam3", 1),
                new("Cam4", 1),
            },
            EndTriggers = new HashSet<TriggerKey>
            {
                new("Cam1", 5),
                new("Cam2", 5),
                new("Cam3", 5),
                new("Cam4", 5),
            }
		};

        return plan;
    }
}
