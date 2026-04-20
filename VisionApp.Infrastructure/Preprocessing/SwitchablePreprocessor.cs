using Microsoft.Extensions.Options;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;

namespace VisionApp.Infrastructure.Preprocessing;

public sealed class SwitchablePreprocessor : IFramePreprocessor
{
    private readonly IOptionsMonitor<PreprocessOptions> _opts;
    private readonly GammaPreprocessor _gamma;
    private readonly PassThroughPreprocessor _passthrough;

    public SwitchablePreprocessor(
        IOptionsMonitor<PreprocessOptions> opts,
        GammaPreprocessor gamma,
        PassThroughPreprocessor passthrough)
    {
        _opts = opts;
        _gamma = gamma;
        _passthrough = passthrough;
    }

    public Task<FrameArrived> PreprocessAsync(RawFrameArrived raw, CancellationToken ct)
    {
        if (!_opts.CurrentValue.Enabled)
            return _passthrough.PreprocessAsync(raw, ct);

        return _gamma.PreprocessAsync(raw, ct);
    }
}
