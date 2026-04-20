namespace VisionApp.Core.Domain;

public sealed class PreprocessOptions
{
    public const string SectionName = "Preprocess";
    public bool Enabled { get; init; } = true;
}
