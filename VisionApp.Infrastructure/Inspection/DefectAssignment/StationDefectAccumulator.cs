using System.Collections.Concurrent;
using VisionApp.Core.Domain;

namespace VisionApp.Infrastructure.Inspection.DefectAssignment;

public sealed class StationDefectAccumulator
{
	private sealed class StationState
	{
		public ConcurrentDictionary<string, int> Counts { get; } = new(StringComparer.OrdinalIgnoreCase);
		public ConcurrentDictionary<TriggerKey, byte> EndSeen { get; } = new();
		public int CompletedFlag; // 0/1
	}

	private readonly ConcurrentDictionary<(Guid CycleId, string StationKey), StationState> _states
		= new();

	public void AddCounts(Guid cycleId, string stationKey, IReadOnlyDictionary<string, int> counts)
	{
		var state = _states.GetOrAdd((cycleId, stationKey), _ => new StationState());
		foreach (var kvp in counts)
			state.Counts.AddOrUpdate(kvp.Key, kvp.Value, (_, old) => old + kvp.Value);
	}

	public bool MarkEndSeen(Guid cycleId, string stationKey, TriggerKey key)
	{
		var state = _states.GetOrAdd((cycleId, stationKey), _ => new StationState());
		return state.EndSeen.TryAdd(key, 0);
	}

	public bool TryComplete(
		Guid cycleId,
		string stationKey,
		IReadOnlyCollection<TriggerKey> requiredEndTriggers,
		out IReadOnlyDictionary<string, int> finalCounts)
	{
		finalCounts = default!;

		if (!_states.TryGetValue((cycleId, stationKey), out var state))
			return false;

		// Check required ends
		foreach (var k in requiredEndTriggers)
		{
			if (!state.EndSeen.ContainsKey(k))
				return false;
		}

		// Ensure only one completion happens
		if (Interlocked.CompareExchange(ref state.CompletedFlag, 1, 0) != 0)
			return false;

		finalCounts = state.Counts.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

		// Remove state to avoid memory growth
		_states.TryRemove((cycleId, stationKey), out _);
		return true;
	}
}
