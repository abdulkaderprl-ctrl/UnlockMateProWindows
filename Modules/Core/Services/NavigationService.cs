using System;

namespace UnlockMatePro.Services
{
    public class NavigationService : INavigationService
    {
        public string CurrentViewName { get; private set; } = "Dashboard";
        public event Action<string>? ViewChanged;

        public void NavigateTo(string viewName)
        {
            if (CurrentViewName != viewName)
            {
                CurrentViewName = viewName;
                ViewChanged?.Invoke(viewName);
            }
        }
    }
}

