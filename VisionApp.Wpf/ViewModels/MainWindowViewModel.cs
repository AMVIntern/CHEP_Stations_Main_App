using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels
{
    public sealed class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly ShellViewModel _shellViewModel;
        private readonly ModalStore _modalStore;

        public NavigationBarViewModel NavigationBarViewModel { get; }
        public StatusFooterViewModel StatusFooterViewModel { get; }
        public ObservableObject CurrentViewModel
        {
            get { return _shellViewModel.CurrentViewModel; }
            set { _shellViewModel.CurrentViewModel = value; }
        }

        // Correct names from your ModalStore
        public bool IsModalOpen
        {
            get { return _modalStore.IsModalOpen; }
        }

        public ObservableObject? ModalViewModel
        {
            get { return _modalStore.ModalViewModel; }
        }

        public MainWindowViewModel(
            ShellViewModel shellViewModel,
            NavigationBarViewModel navigationBarViewModel,
            ModalStore modalStore,
            StatusFooterViewModel statusFooterViewModel)
        {
            _shellViewModel = shellViewModel;
            NavigationBarViewModel = navigationBarViewModel;
            _modalStore = modalStore;

            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            _modalStore.PropertyChanged += ModalStore_PropertyChanged;

            OnPropertyChanged(nameof(CurrentViewModel));
            OnPropertyChanged(nameof(IsModalOpen));
            OnPropertyChanged(nameof(ModalViewModel));
            StatusFooterViewModel = statusFooterViewModel;
        }

        private void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShellViewModel.CurrentViewModel))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
            }
        }

        private void ModalStore_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // ModalStore raises ModalViewModel changes and we manually raise IsModalOpen too,
            // but we still handle both cases safely
            if (e.PropertyName == nameof(ModalStore.ModalViewModel) ||
                e.PropertyName == nameof(ModalStore.IsModalOpen) ||
                string.IsNullOrWhiteSpace(e.PropertyName))
            {
                OnPropertyChanged(nameof(IsModalOpen));
                OnPropertyChanged(nameof(ModalViewModel));
            }
        }

        public void Dispose()
        {
            _shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            _modalStore.PropertyChanged -= ModalStore_PropertyChanged;
            StatusFooterViewModel.Dispose();
        }
    }
}
