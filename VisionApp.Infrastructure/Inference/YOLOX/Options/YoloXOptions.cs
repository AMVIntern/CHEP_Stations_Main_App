namespace VisionApp.Infrastructure.Inference.YOLOX.Options;

public sealed class YoloXOptions
{
    public List<YoloXModelSpec> Models { get; init; } = new();

    public float DefaultConf { get; init; } = 0.35f;
    public float DefaultNms { get; init; } = 0.45f;
}
