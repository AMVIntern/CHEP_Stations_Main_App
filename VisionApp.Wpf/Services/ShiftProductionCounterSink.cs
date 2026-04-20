using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using VisionApp.Core.Domain;
using VisionApp.Core.Interfaces;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.Services;

/// <summary>
/// Result sink that tracks per-shift production metrics (Total, Assured, Standard).
/// Resets counters automatically when the shift changes.
/// Updates a ProductionCounterStore on the UI thread via Dispatcher.
/// Persists counts to disk so they survive app restarts within the same shift.
/// </summary>
public sealed class ShiftProductionCounterSink : IResultSink
{
	private const string CountsDir = @"C:\ProgramData\AMV\VisionApp\0.0.1\Counts";
	private const string CountsFileName = "production_counts.json";
	private readonly string _countsFilePath = Path.Combine(CountsDir, CountsFileName);

	private readonly ProductionCounterStore _store;
	private readonly IShiftResolver _shiftResolver;
	private readonly Dispatcher _dispatcher;
	private readonly ILogger<ShiftProductionCounterSink> _logger;

	private ShiftInfo _lastShift = default; // Will be set in constructor

	/// <summary>
	/// DTO for JSON serialization of production counts.
	/// </summary>
	private sealed class CountsSnapshot
	{
		public int ShiftNumber { get; set; }
		public string ShiftDate { get; set; } = "";
		public int Total { get; set; }
		public int Assured { get; set; }
		public int Standard { get; set; }
	}

	public ShiftProductionCounterSink(
		ProductionCounterStore store,
		IShiftResolver shiftResolver,
		Dispatcher dispatcher,
		ILogger<ShiftProductionCounterSink> logger)
	{
		_store = store;
		_shiftResolver = shiftResolver;
		_dispatcher = dispatcher;
		_logger = logger;

		// Resolve current shift at startup
		var currentShift = _shiftResolver.Resolve(DateTimeOffset.Now);
		_lastShift = currentShift;

		// Try to restore saved counters if the shift still matches
		TryRestoreFromFile(currentShift);

        _store.ShiftNumber = currentShift.ShiftNumber;
    }

	public async Task WriteCycleResultAsync(CycleCompleted completed, CancellationToken ct)
	{
		// Resolve the shift at the time the cycle completed
		var shift = _shiftResolver.Resolve(completed.CompletedAt);

		// Marshal updates to the UI thread
		if (_dispatcher.CheckAccess())
		{
			UpdateMetrics(shift, completed.OverallPass);
		}
		else
		{
			try
			{
				await _dispatcher.InvokeAsync(
					() => UpdateMetrics(shift, completed.OverallPass),
					DispatcherPriority.Background,
					ct);
			}
			catch (OperationCanceledException)
			{
				// App shutting down
			}
		}
	}

	private void UpdateMetrics(ShiftInfo shift, bool overallPass)
	{
		// Detect shift change — compare only ShiftNumber + ShiftDate.
		// CalendarDate is intentionally excluded: Shift 3 spans midnight, so CalendarDate
		// changes at 00:00 even though the logical shift hasn't ended.
		if (shift.ShiftNumber != _lastShift.ShiftNumber || shift.ShiftDate != _lastShift.ShiftDate)
		{
			_lastShift = shift;
			_store.ShiftNumber = shift.ShiftNumber;
			_store.Total = 0;
			_store.Assured = 0;
			_store.Standard = 0;
		}

		// Increment counters
		_store.Total++;
		if (overallPass)
			_store.Assured++;
		else
			_store.Standard++;

		// Persist updated counters to disk
		SaveToFile();
	}

	/// <summary>
	/// Attempts to restore production counts from the saved file if the shift still matches.
	/// </summary>
	private void TryRestoreFromFile(ShiftInfo currentShift)
	{
		try
		{
			if (!File.Exists(_countsFilePath))
			{
				_logger.LogInformation("Production counts file not found; starting at 0.");
				return;
			}

			var json = File.ReadAllText(_countsFilePath);
			var snap = JsonSerializer.Deserialize<CountsSnapshot>(json);
			if (snap is null)
			{
				_logger.LogWarning("Production counts file is empty or invalid.");
				return;
			}

			// Only restore if the shift (number + date) still matches
			if (snap.ShiftNumber == currentShift.ShiftNumber
				&& snap.ShiftDate == currentShift.ShiftDate.ToString("yyyy-MM-dd"))
			{
				_store.ShiftNumber = snap.ShiftNumber;
				_store.Total = snap.Total;
				_store.Assured = snap.Assured;
				_store.Standard = snap.Standard;
				_logger.LogInformation(
					"Restored production counts from disk: Total={Total} Assured={Assured} Standard={Standard}",
					snap.Total, snap.Assured, snap.Standard);
			}
			else
			{
				_logger.LogInformation(
					"Saved production counts are from a different shift; starting fresh. Saved: Shift {SavedShift} on {SavedDate}, Current: Shift {CurrentShift} on {CurrentDate}",
					snap.ShiftNumber, snap.ShiftDate,
					currentShift.ShiftNumber, currentShift.ShiftDate);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not restore production counts from file — starting at 0.");
		}
	}

	/// <summary>
	/// Saves current production counts to disk for persistence across app restarts.
	/// </summary>
	private void SaveToFile()
	{
		try
		{
			Directory.CreateDirectory(CountsDir);

			var snap = new CountsSnapshot
			{
				ShiftNumber = _store.ShiftNumber,
				ShiftDate = _lastShift.ShiftDate.ToString("yyyy-MM-dd"),
				Total = _store.Total,
				Assured = _store.Assured,
				Standard = _store.Standard,
			};

			var options = new JsonSerializerOptions { WriteIndented = true };
			var json = JsonSerializer.Serialize(snap, options);
			File.WriteAllText(_countsFilePath, json);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not save production counts to file.");
		}
	}
}
