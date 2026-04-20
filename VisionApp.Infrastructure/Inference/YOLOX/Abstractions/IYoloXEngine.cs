using OpenCvSharp;
using VisionApp.Inference.YoloX.Models;

namespace VisionApp.Infrastructure.Inference.YOLOX.Abstractions;

public interface IYoloXEngine
{
    Task<List<PredictionObject>> InferAsync(
        string modelKey,
        Mat imageBgr,
        CancellationToken ct);
}
