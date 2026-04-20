using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Inspection.Composition;

public interface IStationPipelineBuilder
{
	/// <summary>Station identifier, e.g. "Station4" or "Station5".</summary>
	string StationKey { get; }

	/// <summary>
	/// Build the inspection runner for a given TriggerKey belonging to this station.
	/// Return null if this station does not handle the key.
	/// </summary>
	IInspectionRunner? TryBuildFor(TriggerKey key);
}
