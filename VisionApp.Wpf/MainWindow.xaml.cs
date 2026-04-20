using System.Windows;
using VisionApp.Wpf.ViewModels;

namespace VisionApp.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
