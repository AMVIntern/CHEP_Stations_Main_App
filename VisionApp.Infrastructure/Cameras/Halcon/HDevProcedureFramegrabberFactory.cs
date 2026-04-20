using HalconDotNet;
using Microsoft.Extensions.Logging;

namespace VisionApp.Infrastructure.Cameras.Halcon;

public sealed class HDevProcedureFramegrabberFactory : IFramegrabberFactory
{
    private readonly HalconProcedureOptions _procOpts;
    private readonly HalconEngineProvider _engineProvider;
    private readonly ILogger<HDevProcedureFramegrabberFactory> _logger;

    public HDevProcedureFramegrabberFactory(
        HalconProcedureOptions procOpts,
        HalconEngineProvider engineProvider,
        ILogger<HDevProcedureFramegrabberFactory> logger)
    {
        _procOpts = procOpts;
        _engineProvider = engineProvider;
        _logger = logger;
    }

    public HTuple Open(string cameraName)
    {
        var procedure = new HDevProcedure(_procOpts.StartProcName);
        var call = new HDevProcedureCall(procedure);

        call.SetInputCtrlParamTuple("CameraName", cameraName);
        call.Execute();

        var handle = call.GetOutputCtrlParamTuple("AcqHandle");

        _logger.LogInformation("HALCON framegrabber opened for {CameraName}", cameraName);
        return handle;
    }

    public void Close(HTuple acqHandle)
    {
        try
        {
            var procedure = new HDevProcedure(_procOpts.CloseProcName);
            var call = new HDevProcedureCall(procedure);

            call.SetInputCtrlParamTuple("AcqHandle", acqHandle);
            call.Execute();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HALCON framegrabber close failed");
        }
    }
}
