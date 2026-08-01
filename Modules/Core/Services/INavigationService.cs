using System;

namespace UnlockMatePro.Services
{
    public interface INavigationService
    {
        string CurrentViewName { get; }
        event Action<string>? ViewChanged;
        void NavigateTo(string viewName);
    }
}

