using HalconDotNet;
using VisionApp.Core.Domain;

namespace VisionApp.Wpf.Models;

/// <summary>
/// Snapshot of a tile at click time. Owns the HImage.
/// </summary>
public sealed class FrameTileSnapshot : IDisposable
{
	public Guid CycleId { get; }
	public TriggerKey Key { get; }

	public HImage Image { get; }
	public bool? Pass { get; }
	public double? Score { get; }
	public string? Message { get; }
	public InspectionVisuals? Visuals { get; }

	public FrameTileSnapshot(
		Guid cycleId,
		TriggerKey key,
		HImage image,
		bool? pass,
		double? score,
		string? message,
		InspectionVisuals? visuals)
	{
		CycleId = cycleId;
		Key = key;
		Image = image;
		Pass = pass;
		Score = score;
		Message = message;
		Visuals = visuals;
	}

	public void Dispose()
	{
		try
		{
			if (Image is not null && Image.IsInitialized())
				Image.Dispose();
		}
		catch { /* ignore */ }
	}
}
