using libplctag;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Infrastructure.Inspection.Composition;
using VisionApp.Infrastructure.PlcOutbound;

namespace VisionApp.Infrastructure.Triggers;

public sealed class PlcTriggerSource : ITriggerSource
{
    private readonly PlcTriggerOptions _options;
    private readonly IPalletIdStore _palletIdStore;
    private readonly ICameraStationResolver _stationResolver;
    private readonly ILogger<PlcTriggerSource> _logger;

    private readonly List<GroupMonitor> _groups = new();

    public PlcTriggerSource(
        PlcTriggerOptions options,
        IPalletIdStore palletIdStore,
        ICameraStationResolver stationResolver,
        ILogger<PlcTriggerSource> logger)
    {
        _options = options;
        _palletIdStore = palletIdStore;
        _stationResolver = stationResolver;
        _logger = logger;
    }

    public async IAsyncEnumerable<TriggerEvent> ReadAllAsync(
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("PLC TriggerSource disabled (PlcTriggers.Enabled=false).");
            yield break;
        }

        try { Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; } catch { }

        BuildGroups();

        await Task.Delay(3000, ct);

        var sw = Stopwatch.StartNew();
        long ToMs(long ticks) => (long)(ticks * 1000.0 / Stopwatch.Frequency);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.ReadDelayMs));

        _logger.LogInformation("PLC TriggerSource started. Groups={Count}", _groups.Count);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                bool tickOk;

                try
                {
                    tickOk = await timer.WaitForNextTickAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    yield break; // normal shutdown
                }

                if (!tickOk)
                    break;

                foreach (var group in _groups)
                {
                    IEnumerable<TriggerEvent> fired;

                    try
                    {
                        fired = group.PollOnce(sw, ToMs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "PLC group poll failed for CameraId={CameraId}", group.CameraIdsText);
                        continue;
                    }

                    foreach (var trig in fired)
                        yield return trig;
                }
            }
        }
        finally
        {
            foreach (var g in _groups)
                g.Dispose();

            _groups.Clear();
            _logger.LogInformation("PLC TriggerSource stopped.");
        }
    }

    private void BuildGroups()
    {
        _groups.Clear();

        foreach (var g in _options.Groups)
        {
            if (string.IsNullOrWhiteSpace(g.BaseTag))
                throw new InvalidOperationException($"PLC group for CameraId '{g.CameraIds}' has empty BaseTag.");

            if (g.TriggerCount <= 0)
                throw new InvalidOperationException($"PLC group '{g.CameraIds}' TriggerCount must be > 0.");

            var monitor = new GroupMonitor(_options, g, _palletIdStore, _stationResolver, _logger);
            monitor.InitializeTags();
            _groups.Add(monitor);
        }
    }

    // -----------------------------------------------------------------------
    // Per-group monitoring (BaseTag.1 .. BaseTag.N)
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // Per-group monitoring (BaseTag.1 .. BaseTag.N)
    // One PLC BaseTag can fan-out to multiple cameras (CameraIds)
    // -----------------------------------------------------------------------
    private sealed class GroupMonitor : IDisposable
    {
        private readonly PlcTriggerOptions _opts;
        private readonly PlcTriggerGroup _group;
        private readonly IPalletIdStore _palletIdStore;
        private readonly string? _stationKey;
        private readonly ILogger _logger;

        // Helpful for error logs
        public string CameraIdsText => string.Join(",", _group.CameraIds);

        private Tag[] _tags = Array.Empty<Tag>();
        private Tag? _palletIdTag;

        private int[] _prev = Array.Empty<int>();
        private bool[] _armed = Array.Empty<bool>();
        private long[] _lastLowTicks = Array.Empty<long>();

        private volatile bool _synced;

        public GroupMonitor(
            PlcTriggerOptions opts,
            PlcTriggerGroup group,
            IPalletIdStore palletIdStore,
            ICameraStationResolver stationResolver,
            ILogger logger)
        {
            _opts = opts;
            _group = group;
            _palletIdStore = palletIdStore;
            _logger = logger;

            // Resolve station key from first camera in the group
            _stationKey = stationResolver.TryGetStationKey(group.CameraIds.FirstOrDefault() ?? "");
        }

        public void InitializeTags()
        {
            if (_group.CameraIds is null || _group.CameraIds.Count == 0)
                throw new InvalidOperationException($"PLC group BaseTag='{_group.BaseTag}' has no CameraIds configured.");

            _tags = new Tag[_group.TriggerCount];
            _prev = new int[_group.TriggerCount];
            _armed = new bool[_group.TriggerCount];
            _lastLowTicks = new long[_group.TriggerCount];

            for (int i = 0; i < _group.TriggerCount; i++)
            {
                _tags[i] = new Tag
                {
                    Gateway = _opts.Gateway,
                    Path = _opts.Path,
                    PlcType = PlcType.ControlLogix,
                    Protocol = Protocol.ab_eip,
                    Name = $"{_group.BaseTag}.{i + 1}",
                    ElementSize = 1,
                    ElementCount = 1,
                };
            }

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < _tags.Length; i++)
            {
                var t = _tags[i];

                try
                {
                    t.Initialize();
                    t.Read();

                    if (t.GetStatus() != 0)
                    {
                        _prev[i] = 0;
                        _armed[i] = false;
                        _lastLowTicks[i] = sw.ElapsedTicks;

                        _logger.LogWarning("PLC init error: {Tag} status={Status}", t.Name, t.GetStatus());
                        continue;
                    }

                    int initial = t.GetBit(0) ? 1 : 0;
                    _prev[i] = initial;
                    _armed[i] = (initial == 0);
                    _lastLowTicks[i] = initial == 0 ? sw.ElapsedTicks : 0;

                    _logger.LogInformation("PLC connected: {Tag} initial={Initial}", t.Name, initial);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PLC init exception: {Tag}", t.Name);
                }
            }

            // If sync not required, accept triggers immediately.
            _synced = !_group.RequireSyncOnTrigger1;

            // Initialize PalletID tag if configured
            if (!string.IsNullOrWhiteSpace(_group.PalletIdControlTag))
            {
                _palletIdTag = new Tag
                {
                    Gateway = _opts.Gateway,
                    Path = _opts.Path,
                    PlcType = PlcType.ControlLogix,
                    Protocol = Protocol.ab_eip,
                    Name = _group.PalletIdControlTag,
                    ElementSize = 4,
                    ElementCount = 1,
                };

                try
                {
                    _palletIdTag.Initialize();
                    _palletIdTag.Read();

                    if (_palletIdTag.GetStatus() == 0)
                    {
                        _logger.LogInformation("PLC PalletID tag connected: {Tag}", _group.PalletIdControlTag);
                    }
                    else
                    {
                        _logger.LogWarning("PLC PalletID tag init error: {Tag} status={Status}",
                            _group.PalletIdControlTag, _palletIdTag.GetStatus());
                        _palletIdTag.Dispose();
                        _palletIdTag = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PLC PalletID tag init exception: {Tag}", _group.PalletIdControlTag);
                    try { _palletIdTag?.Dispose(); } catch { }
                    _palletIdTag = null;
                }
            }
        }

        public IEnumerable<TriggerEvent> PollOnce(Stopwatch sw, Func<long, long> toMs)
        {
            // Typically 0 or a few triggers per poll.
            List<TriggerEvent>? fired = null;

            for (int i = 0; i < _tags.Length; i++)
            {
                var tag = _tags[i];

                try
                {
                    tag.Read();
                    var st = tag.GetStatus();
                    if (st != 0)
                        continue;

                    int value = tag.GetBit(0) ? 1 : 0;

                    // sustained LOW => re-arm
                    if (value == 0)
                    {
                        if (_lastLowTicks[i] == 0)
                            _lastLowTicks[i] = sw.ElapsedTicks;

                        var lowDur = toMs(sw.ElapsedTicks - _lastLowTicks[i]);
                        if (lowDur >= _opts.MinLowMs)
                            _armed[i] = true;
                    }
                    else
                    {
                        _lastLowTicks[i] = 0;
                    }

                    // Rising edge?
                    if (_prev[i] == 0 && value == 1 && _armed[i])
                    {
                        _armed[i] = false;

                        int trigIndex = i + 1;

                        // Sync gating: ignore 2..N until 1 is seen after startup
                        if (_group.RequireSyncOnTrigger1 && !_synced)
                        {
                            if (trigIndex == 1)
                                _synced = true;
                            else
                            {
                                _prev[i] = value;
                                continue;
                            }
                        }

                        // Read PalletID from PLC on first trigger of each cycle
                        if (trigIndex == 1 && _palletIdTag != null && _stationKey != null)
                        {
                            try
                            {
                                _palletIdTag.Read();
                                int palletId = _palletIdTag.GetStatus() == 0 ? _palletIdTag.GetInt32(0) : 0;
                                _palletIdStore.Set(_stationKey, palletId);
                                _logger.LogDebug("PalletID read: Station={Station} Tag={Tag} Value={Value}",
                                    _stationKey, _group.PalletIdControlTag, palletId);
                            }
                            catch (Exception ex)
                            {
                                _palletIdStore.Set(_stationKey, 0);
                                _logger.LogWarning(ex, "PalletID read failed for Station={Station}, defaulting to 0.", _stationKey);
                            }
                        }

                        // ✅ Fan-out: one PLC edge -> triggers for ALL cameras in the group
                        fired ??= new List<TriggerEvent>(_group.CameraIds.Count);

                        var ts = DateTimeOffset.UtcNow;
                        foreach (var camId in _group.CameraIds)
                        {
                            if (string.IsNullOrWhiteSpace(camId))
                                continue;

                            fired.Add(new TriggerEvent(
                                new TriggerKey(camId.Trim(), trigIndex),
                                ts));
                        }
                    }

                    _prev[i] = value;
                }
                catch
                {
                    // swallow per-tag exceptions to keep polling alive
                }
            }

            if (fired is null)
                return Array.Empty<TriggerEvent>();

            return fired;
        }

        public void Dispose()
        {
            foreach (var t in _tags)
            {
                try { t?.Dispose(); } catch { }
            }

            try { _palletIdTag?.Dispose(); } catch { }
        }
    }

}
