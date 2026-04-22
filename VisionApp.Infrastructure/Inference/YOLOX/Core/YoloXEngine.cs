using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using System.Diagnostics;
using VisionApp.Inference.YoloX.Models;
using VisionApp.Infrastructure.Inference.YOLOX.Abstractions;
using VisionApp.Infrastructure.Inference.YOLOX.Options;

namespace VisionApp.Infrastructure.Inference.YOLOX.Core;

public sealed class YoloXEngine : IYoloXEngine, IDisposable
{
    private readonly IReadOnlyDictionary<string, YoloXModelPool> _pools;
	private readonly ILogger<YoloXEngine> _logger;

	public YoloXEngine(IOptions<YoloXOptions> options, ILogger<YoloXEngine> logger)
    {
        _logger = logger;
		var dict = new Dictionary<string, YoloXModelPool>(StringComparer.OrdinalIgnoreCase);

        var o = options.Value;
        foreach (var m in o.Models)
        {
            dict[m.Key] = new YoloXModelPool(
                m.Path,
                m.PoolSize,
                confThreshold: o.DefaultConf,
                nmsThreshold: o.DefaultNms);
        }

        _pools = dict;
    }

	public async Task<List<PredictionObject>> InferAsync(string modelKey, Mat imageBgr, CancellationToken ct)
	{
		if (!_pools.TryGetValue(modelKey, out var pool))
			throw new InvalidOperationException($"YOLOX model key not registered: {modelKey}");

		var sw = Stopwatch.StartNew();
		try
		{
			var preds = await pool.InferAsync(imageBgr, ct).ConfigureAwait(false);
			sw.Stop();

			_logger.LogInformation("YOLOX Infer: Model={ModelKey} Ms={Ms:0.0} Preds={Count}",
				modelKey, sw.Elapsed.TotalMilliseconds, preds.Count);

			return preds;
		}
		catch
		{
			sw.Stop();
			_logger.LogError("YOLOX Infer failed: Model={ModelKey} AfterMs={Ms:0.0}", modelKey, sw.Elapsed.TotalMilliseconds);
			throw;
		}
	}

	public void Dispose()
    {
        foreach (var p in _pools.Values)
            p.Dispose();
    }
}
