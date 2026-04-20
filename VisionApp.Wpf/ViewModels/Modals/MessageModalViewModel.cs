using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels.Modals
{
    public sealed partial class MessageModalViewModel : ObservableObject
    {
        private readonly ModalStore _modalStore;

        public string Title { get; }
        public string Message { get; }

        public MessageModalViewModel(string title, string message, ModalStore modalStore)
        {
            Title = title;
            Message = message;
            _modalStore = modalStore;
        }

        [RelayCommand]
        private void Ok()
        {
            _modalStore.CloseModal();
        }
    }
}
