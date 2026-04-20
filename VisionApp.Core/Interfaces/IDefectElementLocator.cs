namespace VisionApp.Core.Interfaces;

public interface IDefectElementLocator
{
	bool TryLocate(
		string stationKey,
		string cameraId,
		int triggerIndex,
		int imageWidth,
		double xCenter,
		out string boardElement,
		out string bearerElement);
}
