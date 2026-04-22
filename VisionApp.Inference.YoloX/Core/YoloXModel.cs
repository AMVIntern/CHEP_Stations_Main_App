using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System.Runtime.InteropServices;
using VisionApp.Inference.YoloX.Models;
using VisionApp.Inference.YoloX.Utils;
using VisionApp.YOLOX.Core;

namespace VisionApp.Inference.YoloX.Core;

public class YoloXModel : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly int[] _inputDims;
    private readonly int[] _outputDims;
    private readonly int _inputWidth;
    private readonly int _inputHeight;
    private readonly YoloXPostProcessor _postProcessor;
    private readonly float[] _inputTensorValues;   // reusable input buffer
    private readonly float[] _outputTensorValues;  // reusable output buffer
    private bool _disposed;

    public int NumClasses { get; }

    public YoloXModel(
        string modelPath,
        float confThreshold = 0.8f,
        float nmsThreshold = 0.5f,
        SessionOptions options = null)
    {
        _session = ModelUtils.CreateSession(modelPath, options);
        (_inputName, _outputName, _inputDims, _outputDims) = ModelUtils.GetModelMetadata(_session);

        _inputHeight = _inputDims[2];
        _inputWidth = _inputDims[3];

        NumClasses = _outputDims[2] - 5;
        _postProcessor = new YoloXPostProcessor(NumClasses, confThreshold, nmsThreshold);

        // Fixed-size buffers (product of dims)
        _inputTensorValues = new float[_inputDims.Aggregate(1, (a, b) => a * b)];
        _outputTensorValues = new float[_outputDims.Aggregate(1, (a, b) => a * b)];
    }

    /// <summary>
    /// Runs YOLOX inference on the calling thread.
    /// IMPORTANT: This instance is NOT thread-safe; use via a model pool (one call at a time per instance).
    /// </summary>
    public Task<List<PredictionObject>> InferAsync(Mat imageBGR, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (imageBGR is null)
            throw new ArgumentNullException(nameof(imageBGR));
        if (imageBGR.IsDisposed)
            throw new ObjectDisposedException(nameof(imageBGR), "Input Mat is already disposed.");

        ct.ThrowIfCancellationRequested();

        // Preprocess
        using var resized = ImageUtils.static_resize(imageBGR, _inputWidth, _inputHeight);

        using var blob = CvDnn.BlobFromImage(
            resized,
            scaleFactor: 1.0,
            size: new Size(),          // keep resized size
            mean: new Scalar(0, 0, 0),
            swapRB: false,
            crop: false
        );

        // Sanity check: blob size must match input tensor size
        int blobElements = checked((int)blob.Total());

        if (blobElements != _inputTensorValues.Length)
        {
            throw new InvalidOperationException(
                $"Blob element count {blobElements} != expected input tensor length {_inputTensorValues.Length}. " +
                $"Blob shape may not match model input shape.");
        }

        // Copy blob data into the managed input buffer
        Marshal.Copy(blob.Data, _inputTensorValues, 0, blobElements);

        long[] inputShape = _inputDims.Select(d => (long)d).ToArray();
        long[] outputShape = _outputDims.Select(d => (long)d).ToArray();

        // Create OrtValues directly over managed arrays (they get pinned)
        using var inputOrt = OrtValue.CreateTensorValueFromMemory(_inputTensorValues, inputShape);
        using var outputOrt = OrtValue.CreateTensorValueFromMemory(_outputTensorValues, outputShape);

        ct.ThrowIfCancellationRequested();

        // Synchronous Run (good: avoids RunAsync crash issues)
        _session.Run(
            runOptions: null,
            inputNames: new[] { _inputName },
            inputValues: new[] { inputOrt },
            outputNames: new[] { _outputName },
            outputValues: new[] { outputOrt }
        );

        ct.ThrowIfCancellationRequested();

        // _outputTensorValues has been filled in-place
        float scale = Math.Min(
            (float)_inputWidth / imageBGR.Width,
            (float)_inputHeight / imageBGR.Height);

        var preds = _postProcessor.DecodeOutput(
            _outputTensorValues, // no extra ToArray()
            scale,
            imageBGR.Width,
            imageBGR.Height,
            _inputWidth,
            _inputHeight
        );

        return Task.FromResult(preds);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(YoloXModel));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _session?.Dispose();
            (_postProcessor as IDisposable)?.Dispose();
        }

        _disposed = true;
    }

    ~YoloXModel()
    {
        Dispose(false);
    }
}
