using Microsoft.Extensions.Logging;
using VisionApp.Core.Domain;
using VisionApp.Core.Engine;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inspection.Composition;

namespace VisionApp.Infrastructure.Inspection.Aggregation;

public sealed class StationCycleAggregator : IInspectionObserver
{
	private readonly CapturePlan _plan;
	private readonly ICameraStationResolver _stations;
	private readonly IStationCycleSink _sink;
	private readonly ILogger<StationCycleAggregator> _logger;

	// stationKey -> end triggers for that station
	private readonly IReadOnlyDictionary<string, HashSet<TriggerKey>> _stationEnd;

	// CycleId -> per-station buffers
	private readonly Dictionary<Guid, CycleBuffer> _cycles = new();
	private readonly object _gate = new();

	public StationCycleAggregator(
		CapturePlan plan,
		ICameraStationResolver stations,
		IStationCycleSink sink,
		ILogger<StationCycleAggregator> logger)
	{
		_plan = plan;
		_stations = stations;
		_sink = sink;
		_logger = logger;

		_stationEnd = BuildStationEndTriggers(plan, stations);
	}

	public async Task OnInspectionCompletedAsync(InspectionResult result, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		var station = _stations.TryGetStationKey(result.Key.CameraId);
		if (string.IsNullOrWhiteSpace(station))
			return;

		if (!_stationEnd.TryGetValue(station, out var stationEndTriggers) || stationEndTriggers.Count == 0)
			return;

		StationCycleCompleted? completed = null;

		lock (_gate)
		{
			if (!_cycles.TryGetValue(result.CycleId, out var cycle))
			{
				cycle = new CycleBuffer(result.CycleId);
				_cycles[result.CycleId] = cycle;
			}

			var stationBuf = cycle.GetOrCreateStation(station);

			// Track station end triggers as they arrive (based on *results*, not raw triggers)
			if (stationEndTriggers.Contains(result.Key))
				stationBuf.EndSeen.Add(result.Key);

			// Accumulate pass + metrics
			stationBuf.OverallPass &= result.Pass;

			if (result.Metrics != null)
			{
				foreach (var kvp in result.Metrics)
				{
					// Sum numeric metrics (counts, timings, etc.)
					stationBuf.Metrics[kvp.Key] = stationBuf.Metrics.TryGetValue(kvp.Key, out var cur)
						? cur + kvp.Value
						: kvp.Value;
				}
			}

			// Completed when ALL end triggers for this station have been seen
			if (!stationBuf.Completed && stationBuf.EndSeen.IsSupersetOf(stationEndTriggers))
			{
				stationBuf.Completed = true;

				completed = new StationCycleCompleted(
					CycleId: result.CycleId,
					StationKey: station,
					OverallPass: stationBuf.OverallPass,
					Metrics: new Dictionary<string, double>(stationBuf.Metrics, StringComparer.OrdinalIgnoreCase),
					CompletedAt: result.CompletedAt);

				// Optional cleanup: if all stations for this cycle are completed, drop the cycle buffer
				if (cycle.AllStationsCompleted())
					_cycles.Remove(result.CycleId);
			}
		}

		// Emit outside lock
		if (completed != null)
		{
			_logger.LogInformation("Station complete: Cycle={CycleId} Station={Station} Pass={Pass} MetricsCount={Count}",
				completed.CycleId, completed.StationKey, completed.OverallPass, completed.Metrics.Count);

			await _sink.PublishAsync(completed, ct).ConfigureAwait(false);
		}
	}

	private static IReadOnlyDictionary<string, HashSet<TriggerKey>> BuildStationEndTriggers(
		CapturePlan plan,
		ICameraStationResolver stations)
	{
		var dict = new Dictionary<string, HashSet<TriggerKey>>(StringComparer.OrdinalIgnoreCase);

		foreach (var key in plan.EndTriggers)
		{
			var station = stations.TryGetStationKey(key.CameraId);
			if (string.IsNullOrWhiteSpace(station))
				continue;

			if (!dict.TryGetValue(station, out var set))
			{
				set = new HashSet<TriggerKey>();
				dict[station] = set;
			}

			set.Add(key);
		}

		return dict;
	}

	private sealed class CycleBuffer
	{
		public Guid CycleId { get; }
		private readonly Dictionary<string, StationBuffer> _stations = new(StringComparer.OrdinalIgnoreCase);

		public CycleBuffer(Guid cycleId) => CycleId = cycleId;

		public StationBuffer GetOrCreateStation(string stationKey)
		{
			if (!_stations.TryGetValue(stationKey, out var s))
			{
				s = new StationBuffer(stationKey);
				_stations[stationKey] = s;
			}
			return s;
		}

		public bool AllStationsCompleted()
			=> _stations.Count > 0 && _stations.Values.All(s => s.Completed);
	}

	private sealed class StationBuffer
	{
		public string StationKey { get; }
		public bool Completed { get; set; }
		public bool OverallPass { get; set; } = true;

		public HashSet<TriggerKey> EndSeen { get; } = new();
		public Dictionary<string, double> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);

		public StationBuffer(string stationKey) => StationKey = stationKey;
	}
}
