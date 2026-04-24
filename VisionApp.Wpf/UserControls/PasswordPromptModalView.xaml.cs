using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisionApp.Wpf.ViewModels.Modals;

namespace VisionApp.Wpf.UserControls;

public partial class PasswordPromptModalView : UserControl
{
	public PasswordPromptModalView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		DataContextChanged += OnDataContextChanged;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		PwdBox.Focus();
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		PwdBox.Password = string.Empty;
		if (e.NewValue is PasswordPromptModalViewModel vm)
			vm.Password = string.Empty;
	}

	private void PwdBox_OnPasswordChanged(object sender, RoutedEventArgs e)
	{
		if (DataContext is PasswordPromptModalViewModel vm)
			vm.Password = ((PasswordBox)sender).Password;
	}

	private void PwdBox_OnKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
			return;
		if (DataContext is PasswordPromptModalViewModel vm)
			vm.SubmitCommand.Execute(null);
		e.Handled = true;
	}
}
