using CommunityToolkit.Mvvm.ComponentModel;

namespace VisionApp.Wpf.Services
{
    public sealed class NavigationStateService : ObservableObject
    {
        private bool _isCollapsed = true;

        public bool IsCollapsed
        {
            get { return _isCollapsed; }
            set
            {
                if (SetProperty(ref _isCollapsed, value))
                {
                    OnPropertyChanged(nameof(NavBarWidth));
                }
            }
        }

        public double NavBarWidth
        {
            get { return IsCollapsed ? 88 : 260; } // 360 fits your big buttons
        }
    }
}
