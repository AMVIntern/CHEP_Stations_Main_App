using OpenCvSharp;

namespace VisionApp.Inference.YoloX.Models;

public class PredictionObject
{
    public Rect2f Rect { get; set; }  // Equivalent to cv::Rect_<float>
    public int Label { get; set; }
    public float Probability { get; set; }
}
