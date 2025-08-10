using System;

namespace Utility.Services.UI
{
    public enum WindowState
    {  
        Close,
        Open
    }
    
    public interface IWindow
    {
        WindowState State { get; }
        void Show();
        void Hide(Action onHide = null);
    }
}