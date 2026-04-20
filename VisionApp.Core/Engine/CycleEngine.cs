using VisionApp.Core.Domain;

namespace VisionApp.Core.Engine;

/// <summary>
/// Deterministic, single-threaded cycle state machine.
/// - Consumes TriggerEvent
/// - Emits CaptureRequest
/// - Consumes InspectionResult
/// - Emits CycleCompleted when the cycle is fully finished
///
/// Important: This class contains NO HALCON/PLC/UI logic.
/// </summary>
public sealed class CycleEngine
{
    private enum CycleState
    {
        Idle,
        Running,
        Completing
    }

    private readonly CapturePlan _plan;

    private CycleState _state = CycleState.Idle;

    private Guid _cycleId = Guid.Empty;
    private DateTimeOffset _cycleStartedAt;

    private readonly HashSet<TriggerKey> _seenTriggers = new();
    private readonly List<InspectionResult> _results = new();

    // Multi-end support: cycle "end condition" is satisfied when all configured end triggers are seen.
    private readonly HashSet<TriggerKey> _endSeen = new();

    public CycleEngine(CapturePlan plan)
    {
        _plan = plan;
    }

    public Guid CurrentCycleId => _cycleId;
    public bool IsRunning => _state is CycleState.Running or CycleState.Completing;

    /// <summary>
    /// Handle a trigger event and optionally produce a capture request.
    /// </summary>
    public bool TryHandleTrigger(TriggerEvent trigger, out CaptureRequest? request)
    {
        request = null;

        switch (_state)
        {
            case CycleState.Idle:
                if (_plan.IsStart(trigger.Key) && _plan.Contains(trigger.Key))
                {
                    StartNewCycle(trigger.Timestamp, trigger.Key, out request);
                    return true;
                }
                return false;

            case CycleState.Running:
            case CycleState.Completing:
                // Ignore triggers not in plan
                if (!_plan.Contains(trigger.Key))
                    return false;

                // Never repeat within a cycle
                if (!_seenTriggers.Add(trigger.Key))
                    return false;

                request = new CaptureRequest(_cycleId, trigger.Key);

                // Track end triggers (multi-end)
                if (_plan.IsEnd(trigger.Key))
                {
                    _endSeen.Add(trigger.Key);

                    // Only move to Completing when ALL end triggers have been seen
                    if (AreAllEndTriggersSeen())
                    {
                        _state = CycleState.Completing;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Handle an inspection result and optionally produce a completed-cycle aggregate.
    /// </summary>
    public bool TryHandleInspectionResult(InspectionResult result, out CycleCompleted? completed)
    {
        completed = null;

        // Ignore results if we're idle
        if (_state == CycleState.Idle)
            return false;

        // Ignore results from a different cycle (safety)
        if (result.CycleId != _cycleId)
            return false;

        _results.Add(result);

        // v1 definition of "cycle done":
        // - all end triggers have been seen (end condition satisfied)
        // - AND all expected results have completed
        if (AreAllEndTriggersSeen() && _results.Count == _plan.ExpectedCount)
        {
            completed = new CycleCompleted(
                CycleId: _cycleId,
                OverallPass: _results.All(r => r.Pass),
                Results: _results.ToList(),
                CompletedAt: DateTimeOffset.UtcNow);

            Reset();
            return true;
        }

        return false;
    }

    private void StartNewCycle(DateTimeOffset startedAt, TriggerKey startKey, out CaptureRequest request)
    {
        _cycleId = Guid.NewGuid();
        _cycleStartedAt = startedAt;

        _seenTriggers.Clear();
        _results.Clear();
        _endSeen.Clear();

        _seenTriggers.Add(startKey);
        _state = CycleState.Running;

        // If the start trigger is ALSO an end trigger, mark it seen.
        if (_plan.IsEnd(startKey))
        {
            _endSeen.Add(startKey);

            // If that means the full end condition is already satisfied (single-trigger or start is last),
            // move into Completing immediately.
            if (AreAllEndTriggersSeen())
            {
                _state = CycleState.Completing;
            }
        }

        request = new CaptureRequest(_cycleId, startKey);
    }

    private bool AreAllEndTriggersSeen()
    {
        // If plan has no end triggers, treat as invalid config (fail fast).
        // You should prevent this via CapturePlan.Validate().
        return _plan.EndTriggers.Count > 0 && _endSeen.IsSupersetOf(_plan.EndTriggers);
    }

    private void Reset()
    {
        _state = CycleState.Idle;
        _cycleId = Guid.Empty;
        _cycleStartedAt = default;

        _seenTriggers.Clear();
        _results.Clear();
        _endSeen.Clear();
    }
}
