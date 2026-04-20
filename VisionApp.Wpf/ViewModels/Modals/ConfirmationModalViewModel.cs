using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels.Modals
{
    public sealed partial class ConfirmationModalViewModel : ObservableObject
    {
        private readonly ModalStore _modalStore;
        private readonly TaskCompletionSource<bool> _tcs;

        public string Title { get; }
        public string Message { get; }

        public Task<bool> ResultTask
        {
            get { return _tcs.Task; }
        }

        public ConfirmationModalViewModel(string title, string message, ModalStore modalStore)
        {
            Title = title;
            Message = message;
            _modalStore = modalStore;
            _tcs = new TaskCompletionSource<bool>();
        }

        [RelayCommand]
        private void Ok()
        {
            _tcs.TrySetResult(true);
            _modalStore.CloseModal();
        }

        [RelayCommand]
        private void Cancel()
        {
            _tcs.TrySetResult(false);
            _modalStore.CloseModal();
        }
    }
}
