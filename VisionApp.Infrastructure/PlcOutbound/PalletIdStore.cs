using System.Collections.Concurrent;

namespace VisionApp.Infrastructure.PlcOutbound;

public sealed class PalletIdStore : IPalletIdStore
{
    private readonly ConcurrentDictionary<string, int> _store = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string stationKey, int palletId) => _store[stationKey] = palletId;

    public int Get(string stationKey) => _store.TryGetValue(stationKey, out var id) ? id : 0;
}
