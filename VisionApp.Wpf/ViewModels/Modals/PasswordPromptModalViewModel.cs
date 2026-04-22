using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using VisionApp.Wpf.Stores;

namespace VisionApp.Wpf.ViewModels.Modals;

public sealed partial class PasswordPromptModalViewModel : ObservableObject
{
	private readonly ModalStore _modalStore;
	private readonly TaskCompletionSource<bool> _tcs;
	private readonly string _expectedPassword;

	public string Title { get; }

	public Task<bool> ResultTask => _tcs.Task;

	[ObservableProperty] private string _password = string.Empty;

	[ObservableProperty] private string _errorMessage = string.Empty;

	public PasswordPromptModalViewModel(string title, string expectedPassword, ModalStore modalStore)
	{
		Title = title;
		_expectedPassword = expectedPassword ?? string.Empty;
		_modalStore = modalStore;
		_tcs = new TaskCompletionSource<bool>();
	}

	partial void OnPasswordChanged(string value)
	{
		if (!string.IsNullOrEmpty(ErrorMessage))
			ErrorMessage = string.Empty;
	}

	[RelayCommand]
	private void Submit()
	{
		if (!string.Equals(Password, _expectedPassword, StringComparison.Ordinal))
		{
			ErrorMessage = "Wrong password entered.";
			return;
		}

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
