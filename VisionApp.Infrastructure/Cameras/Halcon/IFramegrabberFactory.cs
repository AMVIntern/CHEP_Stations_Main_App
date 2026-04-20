using HalconDotNet;

namespace VisionApp.Infrastructure.Cameras.Halcon;

public interface IFramegrabberFactory
{
    HTuple Open(string cameraName);
    void Close(HTuple acqHandle);
}
