using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VisionApp.Wpf.Models;
using VisionApp.Wpf.Services;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels
{
    public sealed partial class NavigationBarViewModel : ObservableObject, IDisposable
    {
        private readonly NavigationStateService _navState;
        private readonly ShellViewModel _shell;
        private readonly ModalStore _modalStore;
        private readonly IOptionsMonitor<UiSecuritySettings> _uiSecurityMonitor;
        private readonly string _logsRoot;

        public bool IsCollapsed
        {
            get { return _navState.IsCollapsed; }
        }

        public double NavBarWidth
        {
            get { return _navState.NavBarWidth; }
        }

        public NavigationBarViewModel(
            NavigationStateService navState,
            ShellViewModel shell,
            ModalStore modalStore,
            IOptionsMonitor<UiSecuritySettings> uiSecurityMonitor)
        {
            _navState = navState ?? throw new ArgumentNullException(nameof(navState));
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _modalStore = modalStore ?? throw new ArgumentNullException(nameof(modalStore));
            _uiSecurityMonitor = uiSecurityMonitor ?? throw new ArgumentNullException(nameof(uiSecurityMonitor));

            _logsRoot = Directory.Exists(@"C:\AMV\ImageLogs1") ? @"C:\AMV\ImageLogs1" : @"D:\";

            _navState.PropertyChanged += NavState_PropertyChanged;

            // Ensure initial UI is correct
            OnPropertyChanged(nameof(IsCollapsed));
            OnPropertyChanged(nameof(NavBarWidth));
        }

        private void NavState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NavigationStateService.IsCollapsed) ||
                string.IsNullOrWhiteSpace(e.PropertyName))
            {
                OnPropertyChanged(nameof(IsCollapsed));
                OnPropertyChanged(nameof(NavBarWidth));
            }
        }

        [RelayCommand]
        private void ToggleNavigationBar()
        {
            _navState.IsCollapsed = !_navState.IsCollapsed;
            Debug.WriteLine($"[NavBar] Toggle clicked. IsCollapsed={_navState.IsCollapsed}, Width={_navState.NavBarWidth}");
        }

        [RelayCommand]
        private void Home()
        {
            Debug.WriteLine("[NavBar] Home clicked");
            _shell.GoHome();
        }

        [RelayCommand]
        private async Task Settings()
        {
            Debug.WriteLine("[NavBar] Settings clicked");
            if (!await TryUnlockProtectedActionAsync("Settings").ConfigureAwait(true))
                return;
            _shell.GoSettings();
        }

        [RelayCommand]
        private void OpenImageLogs()
        {
            Debug.WriteLine("[NavBar] Image Logs clicked");

            if (!Directory.Exists(_logsRoot))
            {
                _modalStore.ShowMessage(
                    "Image Logs",
                    $"Folder not found:\n{_logsRoot}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = _logsRoot,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[NavBar] Failed to open folder: " + ex);

                _modalStore.ShowMessage(
                    "Image Logs",
                    $"Could not open folder:\n{_logsRoot}\n\n{ex.Message}");
            }
        }


        [RelayCommand]
        private async Task ExitApp()
        {
            try
            {
                Debug.WriteLine("EXIT CLICKED (command executed)");

                if (!await TryUnlockProtectedActionAsync("Exit").ConfigureAwait(true))
                    return;

                bool confirm = await _modalStore.ShowConfirmationAsync(
                    "Exit Application",
                    "Are you sure you want to exit?");

                Debug.WriteLine($"CONFIRM RESULT = {confirm}");

                if (confirm)
                    Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(" EXIT ERROR: " + ex);
                MessageBox.Show(ex.ToString(), "Exit error");
            }
        }



        public void Dispose()
        {
            _navState.PropertyChanged -= NavState_PropertyChanged;
        }

        /// <summary>
        /// If <see cref="UiSecuritySettings.Password"/> is configured, prompts for it; otherwise allows access.
        /// </summary>
        private async Task<bool> TryUnlockProtectedActionAsync(string actionTitle)
        {
            var pwd = _uiSecurityMonitor.CurrentValue.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pwd))
                return true;

            return await _modalStore.ShowPasswordPromptAsync(actionTitle, pwd).ConfigureAwait(true);
        }
    }
}
