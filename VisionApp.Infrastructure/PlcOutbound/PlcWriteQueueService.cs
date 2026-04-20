using libplctag;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VisionApp.Infrastructure.PlcOutbound;

public sealed class PlcWriteQueueService : BackgroundService, IPlcWriteQueue
{
	private readonly ILogger<PlcWriteQueueService> _logger;
	private readonly PlcOutboundOptions _opts;

	private readonly Channel<PlcWriteEntry> _ch =
		Channel.CreateBounded<PlcWriteEntry>(new BoundedChannelOptions(4096)
		{
			SingleReader = true,
			SingleWriter = false,
			FullMode = BoundedChannelFullMode.DropOldest
		});

	// Cache Tag objects by name
	private readonly ConcurrentDictionary<string, Tag> _tagCache =
		new(StringComparer.OrdinalIgnoreCase);

	public PlcWriteQueueService(IOptions<PlcOutboundOptions> opts, ILogger<PlcWriteQueueService> logger)
	{
		_opts = opts.Value;
		_logger = logger;
	}

	public ValueTask EnqueueAsync(PlcWriteEntry entry, CancellationToken ct)
	{
		if (!_opts.Enabled)
			return ValueTask.CompletedTask;

		return _ch.Writer.WriteAsync(entry, ct);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_opts.Enabled)
			return;

		while (await _ch.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
		{
			while (_ch.Reader.TryRead(out var entry))
			{
				try
				{
					switch (entry)
					{
						case PlcBoolWrite b:
							WriteBool(b.TagName, b.Value);
							break;
						case PlcDintWrite d:
							WriteDint(d.TagName, d.Value);
							break;
						default:
							_logger.LogWarning("PLC write queue: unknown entry type {Type} — skipped.", entry.GetType().Name);
							break;
					}
				}
				catch (Exception ex)
				{
					// Don't crash the app. Log occasionally.
					_logger.LogWarning(ex, "PLC write failed: {Tag}", entry.TagName);
				}
			}
		}
	}

	private void WriteBool(string tagName, bool value)
	{
		bool isNew = !_tagCache.ContainsKey(tagName);
		var tag = _tagCache.GetOrAdd(tagName, CreateBoolTag);

		if (isNew)
			_logger.LogDebug("PLC tag created (BOOL): {Tag} (Gateway={Gateway})", tagName, _opts.Gateway);

		tag.SetBit(0, value);
		tag.Write();

		if (tag.GetStatus() != 0)
		{
			_tagCache.TryRemove(tagName, out var bad);
			try { bad?.Dispose(); } catch { /* ignore */ }
			throw new InvalidOperationException($"PLC write status={tag.GetStatus()} tag={tagName}");
		}

		_logger.LogDebug("PLC write OK (BOOL): {Tag} = {Value}", tagName, value);
	}

	private void WriteDint(string tagName, int value)
	{
		bool isNew = !_tagCache.ContainsKey(tagName);
		var tag = _tagCache.GetOrAdd(tagName, CreateDintTag);

		if (isNew)
			_logger.LogDebug("PLC tag created (DINT): {Tag} (Gateway={Gateway})", tagName, _opts.Gateway);

		tag.SetInt32(0, value);
		tag.Write();

		if (tag.GetStatus() != 0)
		{
			_tagCache.TryRemove(tagName, out var bad);
			try { bad?.Dispose(); } catch { /* ignore */ }
			throw new InvalidOperationException($"PLC write status={tag.GetStatus()} tag={tagName}");
		}

		_logger.LogDebug("PLC write OK (DINT): {Tag} = {Value}", tagName, value);
	}

	private Tag CreateBoolTag(string name)
	{
		var tag = new Tag
		{
			Gateway = _opts.Gateway,
			Path = _opts.Path,
			PlcType = PlcType.ControlLogix,
			Protocol = Protocol.ab_eip,
			Name = name,
			ElementSize = 1,
			ElementCount = 1,
		};

		tag.Initialize();
		return tag;
	}

	private Tag CreateDintTag(string name)
	{
		var tag = new Tag
		{
			Gateway = _opts.Gateway,
			Path = _opts.Path,
			PlcType = PlcType.ControlLogix,
			Protocol = Protocol.ab_eip,
			Name = name,
			ElementSize = 4,
			ElementCount = 1,
		};

		tag.Initialize();
		return tag;
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		foreach (var kvp in _tagCache)
		{
			try { kvp.Value.Dispose(); } catch { /* ignore */ }
		}
		_tagCache.Clear();

		return base.StopAsync(cancellationToken);
	}
}
