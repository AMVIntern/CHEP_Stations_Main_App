using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using VisionApp.Core.Domain;
using VisionApp.Wpf.Stores;
using ProductionCounterStore = VisionApp.Wpf.Stores.ProductionCounterStore;

namespace VisionApp.Wpf.ViewModels
{
    public enum FooterConnectionState
    {
        Ready,
        Reconnecting,
        Disconnected
    }

    public sealed class StatusFooterViewModel : ObservableObject, IDisposable
    {
        private readonly CameraConnectionStore _cameraConnectionStore;
        private readonly ProductionCounterStore _productionStore;

        private FooterConnectionState _overallState;
        private string _statusText = string.Empty;

        public FooterConnectionState OverallState
        {
            get { return _overallState; }
            private set { SetProperty(ref _overallState, value); }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        public int ShiftNumber => _productionStore.ShiftNumber;
        public int Total => _productionStore.Total;
        public int Assured => _productionStore.Assured;
        public int Standard => _productionStore.Standard;

        public StatusFooterViewModel(CameraConnectionStore cameraConnectionStore, ProductionCounterStore productionStore)
        {
            _cameraConnectionStore = cameraConnectionStore;
            _productionStore = productionStore;

            // Listen for cameras being added/removed
            _cameraConnectionStore.Cameras.CollectionChanged += Cameras_CollectionChanged;

            // Listen for state changes on existing cameras
            foreach (var cam in _cameraConnectionStore.Cameras)
            {
                cam.PropertyChanged += CameraItem_PropertyChanged;
            }

            // Listen for production counter changes
            _productionStore.PropertyChanged += ProductionStore_PropertyChanged;

            Recalculate();
        }

        private void Cameras_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (CameraConnectionItem item in e.OldItems)
                {
                    item.PropertyChanged -= CameraItem_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (CameraConnectionItem item in e.NewItems)
                {
                    item.PropertyChanged += CameraItem_PropertyChanged;
                }
            }

            Recalculate();
        }

        private void CameraItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // State drives everything
            if (e.PropertyName == nameof(CameraConnectionItem.State) ||
                e.PropertyName == nameof(CameraConnectionItem.IsConnected) ||
                string.IsNullOrWhiteSpace(e.PropertyName))
            {
                Recalculate();
            }
        }

        private void Recalculate()
        {
            var cams = _cameraConnectionStore.Cameras;

            if (cams.Count == 0)
            {
                // No cameras discovered yet -> treat as reconnecting/initialising
                OverallState = FooterConnectionState.Reconnecting;
                StatusText = "Reconnecting… (waiting for cameras)";
                return;
            }

            int disconnectedCount = cams.Count(c => IsDisconnected(c.State));
            bool anyReconnectingOrUnknown = cams.Any(c => IsReconnectingOrUnknown(c.State));
            bool allConnected = cams.All(c => c.State == CameraConnectionState.Connected);

            if (disconnectedCount > 0)
            {
                OverallState = FooterConnectionState.Disconnected;
                StatusText = disconnectedCount == 1
                    ? "Disconnected (1 camera offline)"
                    : $"Disconnected ({disconnectedCount} cameras offline)";
                return;
            }

            if (allConnected)
            {
                OverallState = FooterConnectionState.Ready;
                StatusText = "Ready (all cameras connected)";
                return;
            }

            // Not disconnected, not all-connected -> connecting/unknown/etc.
            if (anyReconnectingOrUnknown)
            {
                OverallState = FooterConnectionState.Reconnecting;
                StatusText = "Reconnecting… (some cameras reconnecting / unknown)";
                return;
            }

            // Fallback (should rarely happen)
            OverallState = FooterConnectionState.Reconnecting;
            StatusText = "Reconnecting…";
        }

        private static bool IsDisconnected(CameraConnectionState state)
        {
            return state == CameraConnectionState.Disconnected;
        }

        private static bool IsReconnectingOrUnknown(CameraConnectionState state)
        {
            // Treat anything that is not Connected as "not ready" unless it is explicitly Disconnected
            // This catches Unknown / Connecting / Reconnecting etc. without needing every enum member listed.
            if (state == CameraConnectionState.Connected) return false;
            if (state == CameraConnectionState.Disconnected) return false;
            return true;
        }

        private void ProductionStore_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Forward production store changes to the view
            OnPropertyChanged(nameof(ShiftNumber));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(Assured));
            OnPropertyChanged(nameof(Standard));
        }

        public void Dispose()
        {
            _cameraConnectionStore.Cameras.CollectionChanged -= Cameras_CollectionChanged;
            _productionStore.PropertyChanged -= ProductionStore_PropertyChanged;

            foreach (var cam in _cameraConnectionStore.Cameras)
            {
                cam.PropertyChanged -= CameraItem_PropertyChanged;
            }
        }
    }
}
