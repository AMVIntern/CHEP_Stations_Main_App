using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VisionApp.Infrastructure.PlcOutbound;

public sealed class PlcHeartbeatService : BackgroundService
{
	private readonly PlcOutboundOptions _opts;
	private readonly IPlcWriteQueue _queue;
	private readonly ILogger<PlcHeartbeatService> _logger;

	private bool _state;

	public PlcHeartbeatService(
		IOptions<PlcOutboundOptions> opts,
		IPlcWriteQueue queue,
		ILogger<PlcHeartbeatService> logger)
	{
		_opts = opts.Value;
		_queue = queue;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_opts.Enabled || !_opts.Heartbeat.Enabled)
			return;

		if (string.IsNullOrWhiteSpace(_opts.Heartbeat.TagName))
		{
			_logger.LogWarning("PLC heartbeat enabled but TagName is empty.");
			return;
		}

		while (!stoppingToken.IsCancellationRequested)
		{
			_state = !_state;

			await _queue.EnqueueAsync(
				new PlcBoolWrite(_opts.Heartbeat.TagName, _state),
				stoppingToken);

			await Task.Delay(_opts.Heartbeat.IntervalMs, stoppingToken);
		}
	}
}
