namespace VisionApp.Infrastructure.PlcOutbound;

public interface IPalletIdStore
{
    /// <summary>
    /// Stores a PalletID integer for a given station key.
    /// </summary>
    void Set(string stationKey, int palletId);

    /// <summary>
    /// Retrieves the stored PalletID for a station key.
    /// Returns 0 if not found.
    /// </summary>
    int Get(string stationKey);
}
