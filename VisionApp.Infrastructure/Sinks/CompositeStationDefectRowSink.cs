using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Sinks;

public sealed class CompositeStationDefectRowSink : IStationDefectRowSink
{
	private readonly IReadOnlyList<IStationDefectRowSink> _sinks;

	public CompositeStationDefectRowSink(IEnumerable<IStationDefectRowSink> sinks)
		=> _sinks = sinks.ToList();

	public async Task WriteAsync(StationDefectRow row, CancellationToken ct)
	{
		foreach (var s in _sinks)
			await s.WriteAsync(row, ct).ConfigureAwait(false);
	}
}
