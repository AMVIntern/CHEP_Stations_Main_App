using VisionApp.Core.Domain;

namespace VisionApp.Core.Engine;

/// <summary>
/// Temporary v1 factory for building a CapturePlan in code.
/// Later we'll replace this with recipe.json loading (IRecipeProvider).
/// </summary>
public static class CapturePlanFactory
{
    /// <summary>
    /// V1 example plan:
    /// Ordered: Cam1[1], Cam2[1], Cam1[2], Cam2[2], Cam1[3]
    /// Start:   Cam1[1] or Cam2[1]
    /// End:     Cam1[3]
    /// </summary>
    public static CapturePlan CreateTwoCameraPlan()
    {
        var ordered = new List<TriggerKey>
        {
            // Station 4
            new("S4Cam1", 1),
            new("S4Cam2", 1),

            new("S4Cam1", 2),
            new("S4Cam2", 2),

            new("S4Cam1", 3),
            new("S4Cam2", 3),

            new("S4Cam1", 4),
            new("S4Cam2", 4),

            // Station 5
            new("S5Cam1", 1),
            new("S5Cam2", 1),
            new("S5Cam3", 1),
            //new("S5Cam4", 1),

            new("S5Cam1", 2),
            new("S5Cam2", 2),
            new("S5Cam3", 2),
            //new("S5Cam4", 2),

            new("S5Cam1", 3),
            new("S5Cam2", 3),
            new("S5Cam3", 3),
            //new("S5Cam4", 3),

            new("S5Cam1", 4),
            new("S5Cam2", 4),
            new("S5Cam3", 4),
            //new("S5Cam4", 4),

            new("S5Cam1", 5),
            new("S5Cam2", 5),
            new("S5Cam3", 5),
            //new("S5Cam4", 5),
        };

        var plan = new CapturePlan
        {
            OrderedTriggers = ordered,
            StartTriggers = new HashSet<TriggerKey>
            {
                // Statoion 4
                //new("S4Cam1", 1),
                new("S4Cam2", 1),

                // Station 5
                //new("S5Cam1", 1),
                //new("S5Cam2", 1),
                //new("S5Cam3", 1),
                //new("S5Cam4", 1),
            },
            EndTriggers = new HashSet<TriggerKey>
            {
                // Station 4
                new("S4Cam1", 4),
                new("S4Cam2", 4),

                // Station 5
                new("S5Cam1", 5),
                new("S5Cam2", 5),
                new("S5Cam3", 5),
                //new("S5Cam4", 5),
            }
		};

        return plan;
    }
}
