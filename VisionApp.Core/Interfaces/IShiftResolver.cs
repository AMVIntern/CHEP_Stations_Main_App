using VisionApp.Core.Domain;

namespace VisionApp.Core.Interfaces;

public interface IShiftResolver
{
	ShiftInfo Resolve(DateTimeOffset timestamp);
}
