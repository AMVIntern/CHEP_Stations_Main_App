using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels.Modals;

/// <summary>
/// Modal password gate. <see cref="SubmitCommand"/> completes the prompt when the password matches.
/// </summary>
public sealed class PasswordPromptModalViewModel : ObservableObject
{
    private readonly ModalStore _modalStore;
    private readonly TaskCompletionSource<bool> _tcs;
    private readonly string _expectedPassword;

    public string Title { get; }

    public Task<bool> ResultTask => _tcs.Task;

    public IRelayCommand SubmitCommand { get; }
    public IRelayCommand CancelCommand { get; }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set
        {
            if (!SetProperty(ref _password, value))
                return;
            if (!string.IsNullOrEmpty(ErrorMessage))
                ErrorMessage = string.Empty;
        }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public PasswordPromptModalViewModel(string title, string expectedPassword, ModalStore modalStore)
    {
        Title = title;
        _expectedPassword = expectedPassword ?? string.Empty;
        _modalStore = modalStore;
        _tcs = new TaskCompletionSource<bool>();

        SubmitCommand = new RelayCommand(SubmitExecute);
        CancelCommand = new RelayCommand(CancelExecute);
    }

    private void SubmitExecute()
    {
        var entered = Password.Trim();
        var expected = _expectedPassword.Trim();

        if (!string.Equals(entered, expected, StringComparison.Ordinal))
        {
            ErrorMessage = "Wrong password entered.";
            return;
        }

        _tcs.TrySetResult(true);
        _modalStore.CloseModal();
    }

    private void CancelExecute()
    {
        _tcs.TrySetResult(false);
        _modalStore.CloseModal();
    }
}
