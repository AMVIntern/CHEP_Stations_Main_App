using OpenCvSharp;
using System.Collections.Concurrent;
using VisionApp.Inference.YoloX.Core;
using VisionApp.Inference.YoloX.Models;

namespace VisionApp.Infrastructure.Inference.YOLOX.Core;

internal sealed class YoloXModelPool : IDisposable
{
    private readonly string _path;
    private readonly float _confThreshold;
    private readonly float _nmsThreshold;
    private readonly ConcurrentBag<YoloXModel> _bag = new();
    private readonly SemaphoreSlim _slots;

    public YoloXModelPool(string path, int poolSize, float confThreshold, float nmsThreshold)
    {
        _path = path;
        _confThreshold = confThreshold;
        _nmsThreshold = nmsThreshold;
        _slots = new SemaphoreSlim(poolSize, poolSize);
    }

    public async Task<List<PredictionObject>> InferAsync(Mat bgr, CancellationToken ct)
    {
        await _slots.WaitAsync(ct).ConfigureAwait(false);
        YoloXModel? model = null;

        try
        {
            if (!_bag.TryTake(out model))
                model = new YoloXModel(_path, _confThreshold, _nmsThreshold);

            return await model.InferAsync(bgr, ct).ConfigureAwait(false);
        }
        finally
        {
            if (model != null)
                _bag.Add(model);

            _slots.Release();
        }
    }

    public void Dispose()
    {
        while (_bag.TryTake(out var m))
            m.Dispose();

        _slots.Dispose();
    }
}
