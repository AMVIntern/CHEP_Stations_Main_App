using HalconDotNet;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace VisionApp.Inference.YoloX.Utils;

public static class ImageUtils
{
    /// <summary>
    /// Resize with aspect ratio preserved and letterbox padding (114,114,114).
    /// </summary>
    public static Mat static_resize(Mat img, int inputWidth, int inputHeight)
    {
        if (img is null) throw new ArgumentNullException(nameof(img));
        if (inputWidth <= 0 || inputHeight <= 0) throw new ArgumentOutOfRangeException();
        if (img.Empty())
            throw new ArgumentException("Input image is empty.", nameof(img));

        float r = Math.Min(inputWidth / (float)img.Width, inputHeight / (float)img.Height);

        // Guard: in case img.Width/Height are 0 (corrupt mat), avoid NaN/Inf
        if (float.IsNaN(r) || float.IsInfinity(r))
            throw new InvalidOperationException($"Invalid scale factor r={r} for image {img.Width}x{img.Height}.");

        int unpadWidth = Math.Max(1, (int)Math.Round(r * img.Width));
        int unpadHeight = Math.Max(1, (int)Math.Round(r * img.Height));

        using var scaledImg = new Mat();
        Cv2.Resize(img, scaledImg, new Size(unpadWidth, unpadHeight));

        var resizedMat = new Mat(new Size(inputWidth, inputHeight), MatType.CV_8UC3, new Scalar(114, 114, 114));
        var roi = new Rect(0, 0, scaledImg.Width, scaledImg.Height);

        using (var dstRoi = new Mat(resizedMat, roi)) // ROI view must be disposed
        {
            scaledImg.CopyTo(dstRoi);
        }

        // Caller owns this Mat
        return resizedMat;
    }

    /// <summary>
    /// Crops a list of regions from an image; out-of-bounds ROIs are clamped.
    /// Each returned Mat owns its own pixels and must be disposed by the caller.
    /// </summary>
    public static List<Mat> CropRegions(Mat image, List<Rect> rois)
    {
        if (image is null) throw new ArgumentNullException(nameof(image));
        if (rois is null) throw new ArgumentNullException(nameof(rois));
        if (image.Empty()) throw new ArgumentException("Input image is empty.", nameof(image));

        var crops = new List<Mat>(rois.Count);

        foreach (var roi in rois)
        {
            int x = Math.Clamp(roi.X, 0, image.Width);
            int y = Math.Clamp(roi.Y, 0, image.Height);
            int w = Math.Clamp(roi.Width, 0, image.Width - x);
            int h = Math.Clamp(roi.Height, 0, image.Height - y);

            if (w <= 0 || h <= 0)
                continue;

            var valid = new Rect(x, y, w, h);

            using var view = new Mat(image, valid); // view must be disposed
            crops.Add(view.Clone());                // cloned Mat owns its own buffer
        }

        return crops;
    }

    /// <summary>
    /// Convert a HALCON HImage (1 or 3 channels) to OpenCvSharp Mat (BGR, 8-bit).
    /// </summary>
    public static Mat HImageToMatBGR(HImage hImage)
    {
        if (hImage is null) throw new ArgumentNullException(nameof(hImage));

        // Determine channel count via HALCON operator
        HOperatorSet.CountChannels(hImage, out HTuple chT);
        int channels = chT.I;

        if (channels == 3)
        {
            // 3-channel: pointers for R, G, B + type/size
            hImage.GetImagePointer3(
                out IntPtr rPtr,
                out IntPtr gPtr,
                out IntPtr bPtr,
                out string type,
                out int width,
                out int height);

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Invalid HImage size: {width}x{height}.");

            if (!type.Equals("byte", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Unsupported 3-channel type: {type}");

            int len = width * height;

            var r = new byte[len];
            var g = new byte[len];
            var b = new byte[len];

            Marshal.Copy(rPtr, r, 0, len);
            Marshal.Copy(gPtr, g, 0, len);
            Marshal.Copy(bPtr, b, 0, len);

            using var rMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, r);
            using var gMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, g);
            using var bMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, b);

            var bgr = new Mat();
            Cv2.Merge(new[] { bMat, gMat, rMat }, bgr);
            return bgr;
        }
        else if (channels == 1)
        {
            string type;
            int width, height;

            // Ptr is valid while hImage is alive
            IntPtr ptr = hImage.GetImagePointer1(out type, out width, out height);

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Invalid HImage size: {width}x{height}.");

            int len = width * height;

            if (type.Equals("byte", StringComparison.OrdinalIgnoreCase))
            {
                var gray = new byte[len];
                Marshal.Copy(ptr, gray, 0, len);

                using var grayMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, gray);
                var bgr = new Mat();
                Cv2.CvtColor(grayMat, bgr, ColorConversionCodes.GRAY2BGR);
                return bgr;
            }
            else if (type.Equals("uint2", StringComparison.OrdinalIgnoreCase))
            {
                // 16-bit unsigned
                var tmp = new byte[len * 2];
                Marshal.Copy(ptr, tmp, 0, tmp.Length);

                var u16 = new ushort[len];
                Buffer.BlockCopy(tmp, 0, u16, 0, tmp.Length);

                using var mat16 = Mat.FromPixelData(height, width, MatType.CV_16UC1, u16);
                using var mat8 = new Mat();
                // Simple linear downscale to 8-bit
                mat16.ConvertTo(mat8, MatType.CV_8U, 1.0 / 256.0);

                var bgr = new Mat();
                Cv2.CvtColor(mat8, bgr, ColorConversionCodes.GRAY2BGR);
                return bgr;
            }
            else if (type.Equals("int2", StringComparison.OrdinalIgnoreCase))
            {
                // 16-bit signed
                var tmp = new byte[len * 2];
                Marshal.Copy(ptr, tmp, 0, tmp.Length);

                var s16 = new short[len];
                Buffer.BlockCopy(tmp, 0, s16, 0, tmp.Length);

                using var mat16s = Mat.FromPixelData(height, width, MatType.CV_16SC1, s16);
                using var mat16u = new Mat();

                // Map signed [-32768, 32767] → roughly to [0, 65535] then to 8-bit
                // ConvertScaleAbs gives 8-bit result already
                Cv2.ConvertScaleAbs(mat16s, mat16u, alpha: 1.0, beta: 32768.0);

                var bgr = new Mat();
                Cv2.CvtColor(mat16u, bgr, ColorConversionCodes.GRAY2BGR);
                return bgr;
            }
            else
            {
                throw new NotSupportedException($"Unsupported 1-channel type: {type}");
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported channel count: {channels}");
        }
    }
}
