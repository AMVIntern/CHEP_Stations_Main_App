namespace VisionApp.Infrastructure.Inference.YOLOX.Options;

public sealed class YoloXModelSpec
{
    public required string Key { get; init; }   // e.g. "PlasticDetector"
    public required string Path { get; init; }  // full onnx path
    public int PoolSize { get; init; } = 1;     // bump for parallelism
}
