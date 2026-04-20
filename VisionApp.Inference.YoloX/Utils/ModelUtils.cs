using Microsoft.ML.OnnxRuntime;
using System.Diagnostics;

namespace VisionApp.Inference.YoloX.Utils;

public static class ModelUtils
{
    /// <summary>
    /// Returns default SessionOptions configured for optimized CPU inference.
    /// </summary>
    public static SessionOptions GetDefaultSessionOptions()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        // Use CUDA if available
        try
        {
            if (IsCudaAvailable())
            {
                Debug.WriteLine("Using CUDAExecutionProvider");
                options.AppendExecutionProvider_CUDA(0);
            }
            else
            {
                Debug.WriteLine("CUDA not available, using CPUExecutionProvider");
            }
        }
        catch (Exception)
        {

        }

        return options;
    }

    /// <summary>
    /// Creates an InferenceSession using the specified model path and session options.
    /// </summary>
    public static InferenceSession CreateSession(string modelPath, SessionOptions options = null)
    {
        options ??= GetDefaultSessionOptions();

        if (!File.Exists(modelPath))
            throw new ArgumentException($"Model path does not exist: {modelPath}");

        return new InferenceSession(modelPath, options);
    }

    /// <summary>
    /// Extracts basic model I/O metadata from an InferenceSession.
    /// </summary>
    public static (string inputName, string outputName, int[] inputDims, int[] outputDims)
        GetModelMetadata(InferenceSession session)
    {
        string inputName = session.InputMetadata.Keys.First();
        string outputName = session.OutputMetadata.Keys.First();
        int[] inputDims = session.InputMetadata[inputName].Dimensions;
        int[] outputDims = session.OutputMetadata[outputName].Dimensions;

        return (inputName, outputName, inputDims, outputDims);
    }

    public static bool IsCudaAvailable()
    {
        try
        {
            var providers = OrtEnv.Instance().GetAvailableProviders();
            return providers.Contains("CUDAExecutionProvider");
        }
        catch
        {
            return false;
        }
    }
}

