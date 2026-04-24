using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels
{
    public sealed class ShellViewModel : ObservableObject
    {
        private readonly ModalStore _modalStore;

        private ObservableObject _currentViewModel;

        public ObservableObject CurrentViewModel
        {
            get { return _currentViewModel; }
            set { SetProperty(ref _currentViewModel, value); }
        }

        public ObservableObject? ModalViewModel
        {
            get { return _modalStore.ModalViewModel; }
        }

        public bool IsModalOpen
        {
            get { return _modalStore.IsModalOpen; }
        }

        private readonly MainViewModel _homeViewModel;
        private readonly Func<SettingsViewModel> _settingsFactory;

        public ShellViewModel(MainViewModel homeViewModel, ModalStore modalStore, Func<SettingsViewModel> settingsFactory)
        {
            _homeViewModel = homeViewModel;
            _currentViewModel = homeViewModel;
            _settingsFactory = settingsFactory;

            _modalStore = modalStore;
            _modalStore.PropertyChanged += ModalStore_PropertyChanged;
        }

        private void ModalStore_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModalStore.ModalViewModel) ||
                e.PropertyName == nameof(ModalStore.IsModalOpen))
            {
                OnPropertyChanged(nameof(ModalViewModel));
                OnPropertyChanged(nameof(IsModalOpen));
            }
        }

        public void GoHome()
        {
            CurrentViewModel = _homeViewModel;
        }

        public void GoSettings()
        {
            CurrentViewModel = _settingsFactory();
        }
    }
}
