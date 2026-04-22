using OpenCvSharp;
using VisionApp.Inference.YoloX.Models;

namespace VisionApp.YOLOX.Core;

public class YoloXPostProcessor
{
    private readonly int _numClasses;
    private readonly float[] _confThresholds; // per-class confidence thresholds
    private readonly float _nmsThreshold;
    private readonly bool _decodedOutput; // true if model was exported with --decode_in_inference
    private readonly bool _bestClassOnly; // true = take max class per anchor (matches Python postprocess)

    // Strides never change — reuse the same list
    private static readonly List<int> _strides = new List<int> { 8, 16, 32 };

    // Grid strides depend only on input dimensions, which are fixed per model instance
    private List<GridAndStride> _gridStrides;
    private int _cachedGridWidth;
    private int _cachedGridHeight;

    /// <summary>Single confidence threshold applied to all classes.</summary>
    public YoloXPostProcessor(int numClasses, float conf = 0.5f, float nms = 0.5f, bool decodedOutput = false, bool bestClassOnly = false)
    {
        _numClasses = numClasses;
        _confThresholds = new float[numClasses];
        Array.Fill(_confThresholds, conf);
        _nmsThreshold = nms;
        _decodedOutput = decodedOutput;
        _bestClassOnly = bestClassOnly;
    }

    /// <summary>Per-class confidence thresholds. Array length must equal numClasses.</summary>
    public YoloXPostProcessor(int numClasses, float[] perClassConf, float nms = 0.5f, bool decodedOutput = false, bool bestClassOnly = false)
    {
        _numClasses = numClasses;
        _confThresholds = perClassConf;
        _nmsThreshold = nms;
        _decodedOutput = decodedOutput;
        _bestClassOnly = bestClassOnly;
    }

    public List<PredictionObject> DecodeOutput(
    float[] tensorOutput,
    float scale,
    int imgOriginalWidth,
    int imgOriginalHeight,
    int imageResizedWidth,
    int imageResizedHeight)
    {
        var predictionObjects = new List<PredictionObject>();
        var finalPredictions = new List<PredictionObject>();

        // Use cached grid strides — only recomputed if dimensions change (never in practice)
        if (_gridStrides == null || _cachedGridWidth != imageResizedWidth || _cachedGridHeight != imageResizedHeight)
        {
            _gridStrides = GenerateGridsAndStrides(imageResizedWidth, imageResizedHeight, _strides);
            _cachedGridWidth = imageResizedWidth;
            _cachedGridHeight = imageResizedHeight;
        }

        GenerateYoloXProposals(
            _gridStrides,
            tensorOutput,
            _numClasses,
            ref predictionObjects);
        SortPredictionObjectsByConfidence(ref predictionObjects);
        List<int> pickedIndices = new List<int>();
        NonMaxSuppression(
            ref predictionObjects,
            ref pickedIndices,
            _nmsThreshold);
        // post suppressed final count
        int finalPredictionCount = pickedIndices.Count;
        finalPredictions.Capacity = finalPredictionCount;

        // Loop through the selected indices and save in predictionObjects
        for (int i = 0; i < finalPredictionCount; i++)
        {
            // Get the selected prediction
            var selectedPrediction = predictionObjects[pickedIndices[i]];

            // Adjust offsets to original image dimensions
            float x0 = selectedPrediction.Rect.Left / scale;
            float y0 = selectedPrediction.Rect.Top / scale;
            float x1 = (selectedPrediction.Rect.Left + selectedPrediction.Rect.Width) / scale;
            float y1 = (selectedPrediction.Rect.Top + selectedPrediction.Rect.Height) / scale;

            // Clip values to stay within the original image bounds
            x0 = MathF.Max(MathF.Min(x0, imgOriginalWidth - 1), 0f);
            y0 = MathF.Max(MathF.Min(y0, imgOriginalHeight - 1), 0f);
            x1 = MathF.Max(MathF.Min(x1, imgOriginalWidth - 1), 0f);
            y1 = MathF.Max(MathF.Min(y1, imgOriginalHeight - 1), 0f);

            // Update the prediction object
            var adjustedPrediction = new PredictionObject
            {
                Rect = new Rect2f(x0, y0, x1 - x0, y1 - y0),
                Label = selectedPrediction.Label,
                Probability = selectedPrediction.Probability
            };

            // Add to the final list of prediction objects
            finalPredictions.Add(adjustedPrediction);
        }

        return finalPredictions;
    }

    public List<GridAndStride> GenerateGridsAndStrides(
    int targetWidth,
    int targetHeight,
    List<int> strides)
    {
        var gridStrides = new List<GridAndStride>();

        foreach (var stride in strides)
        {
            int numGridW = targetWidth / stride;
            int numGridH = targetHeight / stride;

            for (int gh = 0; gh < numGridH; gh++)
            {
                for (int gw = 0; gw < numGridW; gw++)
                {
                    gridStrides.Add(new GridAndStride
                    {
                        GridW = gw,
                        GridH = gh,
                        Stride = stride
                    });
                }
            }
        }

        return gridStrides;
    }

    public void GenerateYoloXProposals(
        List<GridAndStride> gridStrides,
        float[] tensorOutput,
        int numClasses,
        ref List<PredictionObject> predictionObjects)
    {
        predictionObjects.Clear();
        int num_grid_points = gridStrides.Count;
        for (int grid_index = 0; grid_index < num_grid_points; grid_index++)
        {
            int tensor_index = grid_index * (numClasses + 5);
            // Fix: Skip if accessing tensorOutput would go out of bounds
            if ((tensor_index + 5 + numClasses) > tensorOutput.Length)
                continue;

            float x_center, y_center, w, h;

            if (_decodedOutput)
            {
                // Model exported with --decode_in_inference: output is already decoded coordinates
                x_center = tensorOutput[tensor_index + 0];
                y_center = tensorOutput[tensor_index + 1];
                w = tensorOutput[tensor_index + 2];
                h = tensorOutput[tensor_index + 3];
            }
            else
            {
                var gs = gridStrides[grid_index];
                // Raw output: apply grid offset and stride decoding
                x_center = (tensorOutput[tensor_index + 0] + gs.GridW) * gs.Stride;
                y_center = (tensorOutput[tensor_index + 1] + gs.GridH) * gs.Stride;
                w = MathF.Exp(tensorOutput[tensor_index + 2]) * gs.Stride;
                h = MathF.Exp(tensorOutput[tensor_index + 3]) * gs.Stride;
            }

            // get left top corner of bbox using x and y centers
            float x0 = x_center - (w * 0.5f);
            float y0 = y_center - (h * 0.5f);

            // get bbox objectness
            float bbox_objectness = tensorOutput[tensor_index + 4];

            if (_bestClassOnly)
            {
                // Matches Python postprocess: take max class per anchor only, threshold is >=
                int bestClass = 0;
                float bestClassScore = tensorOutput[tensor_index + 5];
                for (int class_index = 1; class_index < numClasses; class_index++)
                {
                    float score = tensorOutput[tensor_index + 5 + class_index];
                    if (score > bestClassScore) { bestClassScore = score; bestClass = class_index; }
                }
                float box_prob = bbox_objectness * bestClassScore;
                if (box_prob >= _confThresholds[bestClass])
                {
                    predictionObjects.Add(new PredictionObject
                    {
                        Rect = new Rect2f(x0, y0, w, h),
                        Label = bestClass,
                        Probability = box_prob
                    });
                }
            }
            else
            {
                // loop through class probabilities, applying per-class confidence threshold
                for (int class_index = 0; class_index < numClasses; class_index++)
                {
                    float box_class_score = tensorOutput[tensor_index + 5 + class_index];
                    float box_prob = bbox_objectness * box_class_score;

                    if (box_prob > _confThresholds[class_index])
                    {
                        var prediction = new PredictionObject
                        {
                            Rect = new Rect2f(x0, y0, w, h),
                            Label = class_index,
                            Probability = box_prob
                        };
                        predictionObjects.Add(prediction);
                    }
                }
            }
        }
    }

    public void SortPredictionObjectsByConfidence(ref List<PredictionObject> predictionObjects)
    {
        predictionObjects.Sort((a, b) => b.Probability.CompareTo(a.Probability));
    }

    public void NonMaxSuppression(
        ref List<PredictionObject> predictionObject,
        ref List<int> pickedIndices,
        float nmsThreshold)
    {
        pickedIndices.Clear();

        int numPredictions = predictionObject.Count;

        // Plain array — no double-allocation like List<float>(new float[n])
        float[] areas = new float[numPredictions];

        // calculate areas
        for (int i = 0; i < numPredictions; i++)
        {
            areas[i] = predictionObject[i].Rect.Width * predictionObject[i].Rect.Height;
        }

        // Loop through all bounding boxes
        for (int i = 0; i < numPredictions; i++)
        {
            var currentBox = predictionObject[i];
            bool keep = true;

            // Compare with picked boxes of the same class only (per-class NMS, matches Python batched_nms)
            foreach (var pickedIndex in pickedIndices)
            {
                var pickedBox = predictionObject[pickedIndex];
                if (pickedBox.Label != currentBox.Label)
                    continue;
                // Calculate intersection
                var intersection = currentBox.Rect.Intersect(pickedBox.Rect);
                float intersectionArea = MathF.Max(0f, intersection.Width) * MathF.Max(0f, intersection.Height);
                // Calculate union
                float unionArea = areas[i] + areas[pickedIndex] - intersectionArea;
                // Calculate IoU (Intersection over Union)
                float iou = unionArea > 0 ? intersectionArea / unionArea : 0;

                // If IoU exceeds the threshold, discard the box
                if (iou > nmsThreshold)
                {
                    keep = false;
                    break;
                }
            }

            // If the box is to be kept, add its index to the picked list
            if (keep)
            {
                pickedIndices.Add(i);
            }
        }
    }
}
