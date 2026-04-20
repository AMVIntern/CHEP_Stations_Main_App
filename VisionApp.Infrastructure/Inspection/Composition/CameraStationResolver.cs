using Microsoft.Extensions.Options;

namespace VisionApp.Infrastructure.Inspection.Composition;

public sealed class CameraStationResolver : ICameraStationResolver
{
	private readonly IReadOnlyDictionary<string, string> _map;

	public CameraStationResolver(IOptions<CameraStationOptions> options)
	{
		_map = options.Value.Map;
	}

	public string? TryGetStationKey(string cameraId)
	{
		if (string.IsNullOrWhiteSpace(cameraId))
			return null;

		return _map.TryGetValue(cameraId, out var stationKey) ? stationKey : null;
	}
}
