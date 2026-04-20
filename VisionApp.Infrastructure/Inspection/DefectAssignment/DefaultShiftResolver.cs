using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

public sealed class DefaultShiftResolver : IShiftResolver
{
	private readonly ShiftOptions _opt;
	private readonly TimeZoneInfo _tz;

	public DefaultShiftResolver(IOptions<ShiftOptions> options)
	{
		_opt = options.Value;
		_tz = TimeZoneInfo.FindSystemTimeZoneById(_opt.TimeZoneId);
	}

	public ShiftInfo Resolve(DateTimeOffset timestamp)
	{
		var local = TimeZoneInfo.ConvertTime(timestamp, _tz);
		var t = local.TimeOfDay;

		int shift =
			t >= _opt.Shift3Start ? 3 :
			t >= _opt.Shift2Start ? 2 :
			t >= _opt.Shift1Start ? 1 : 3; // before Shift1Start => still shift 3

		// Shift-date rule: early morning before Shift1Start belongs to previous day
		var calendarDate = DateOnly.FromDateTime(local.DateTime);
		var shiftDate = calendarDate;
		if (t < _opt.Shift1Start)
			shiftDate = shiftDate.AddDays(-1);

		return new ShiftInfo(shift, shiftDate, calendarDate);
	}
}
