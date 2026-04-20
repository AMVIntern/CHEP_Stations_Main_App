using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Core.Hosting.Services;

public sealed class InspectionService : BackgroundService
{
	private readonly IInspectionRunner _runner;
	private readonly ChannelReader<FrameArrived> _frameReader;
	private readonly ChannelWriter<InspectionResult> _inspectionWriter;
	private readonly IInspectionObserver[] _observers;
	private readonly ILogger<InspectionService> _logger;
	private readonly SemaphoreSlim _inflight;

	public InspectionService(
		IInspectionRunner runner,
		Channel<FrameArrived> frameChannel,
		Channel<InspectionResult> inspectionChannel,
		IEnumerable<IInspectionObserver> observers,
		ILogger<InspectionService> logger)
	{
		_runner = runner;
		_frameReader = frameChannel.Reader;
		_inspectionWriter = inspectionChannel.Writer;
		// Materialise to array once so .Any() and .Select() never re-evaluate a
		// DI-provided lazy IEnumerable<T> on every inspected frame.
		_observers = (observers ?? Array.Empty<IInspectionObserver>()).ToArray();
		_logger = logger;

		_inflight = new SemaphoreSlim(
			initialCount: Environment.ProcessorCount,
			maxCount: Environment.ProcessorCount);
	}

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		_logger.LogInformation("InspectionService started.");

		var tasks = new List<Task>();

		try
		{
			await foreach (var frame in _frameReader.ReadAllAsync(ct))
			{
				await _inflight.WaitAsync(ct);

				tasks.Add(Task.Run(async () =>
				{
					try
					{
						InspectionResult result;
						try
						{
							result = await _runner.InspectAsync(frame, ct).ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Inspection failed for {Key}. Emitting FAIL.", frame.Key);
							result = new InspectionResult(
								frame.CycleId, frame.Key,
								Pass: false, Score: 0.0,
								Message: $"Inspection failed: {ex.Message}",
								Metrics: null,
								Visuals: null,
								CompletedAt: DateTimeOffset.UtcNow);
						}

						// Publish to pipeline
						await _inspectionWriter.WriteAsync(result, ct).ConfigureAwait(false);

						// Notify observers (station aggregation, UI, tower lights later, etc.)
						if (_observers.Any())
						{
							await NotifyObserversSafeAsync(result, ct).ConfigureAwait(false);
						}
					}
					finally
					{
						frame.Dispose();
						_inflight.Release();
					}
				}, ct));

				tasks.RemoveAll(t => t.IsCompleted);
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
		finally
		{
			await Task.WhenAll(tasks);
			_logger.LogInformation("InspectionService stopped.");
		}
	}

	public override void Dispose()
	{
		base.Dispose();
		_inflight.Dispose();
	}

	private async Task NotifyObserversSafeAsync(InspectionResult result, CancellationToken ct)
	{
		var notifyTasks = _observers.Select(async obs =>
		{
			try
			{
				await obs.OnInspectionCompletedAsync(result, ct).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Inspection observer {Observer} failed for {Key}", obs.GetType().Name, result.Key);
			}
		});

		await Task.WhenAll(notifyTasks).ConfigureAwait(false);
	}
}
