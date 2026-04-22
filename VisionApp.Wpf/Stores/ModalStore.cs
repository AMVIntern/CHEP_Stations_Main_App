using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using VisionApp.Wpf.ViewModels.Modals;

namespace VisionApp.Wpf.Stores
{
    public sealed class ModalStore : ObservableObject
    {
        private ObservableObject? _modalViewModel;

        public ObservableObject? ModalViewModel
        {
            get { return _modalViewModel; }
            private set { SetProperty(ref _modalViewModel, value); }
        }

        public bool IsModalOpen
        {
            get { return ModalViewModel != null; }
        }

        public void ShowModal(ObservableObject vm)
        {
            ModalViewModel = vm;
            OnPropertyChanged(nameof(IsModalOpen));
        }

        public void CloseModal()
        {
            ModalViewModel = null;
            OnPropertyChanged(nameof(IsModalOpen));
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var vm = new ConfirmationModalViewModel(title, message, this);
            ShowModal(vm);
            return vm.ResultTask;
        }

        public void ShowMessage(string title, string message)
        {
            ShowModal(new MessageModalViewModel(title, message, this));
        }

        /// <summary>
        /// Shows a password prompt. Returns true when the entered password matches <paramref name="expectedPassword"/>.
        /// </summary>
        public Task<bool> ShowPasswordPromptAsync(string title, string expectedPassword)
        {
            var vm = new PasswordPromptModalViewModel(title, expectedPassword, this);
            ShowModal(vm);
            return vm.ResultTask;
        }
    }
}
