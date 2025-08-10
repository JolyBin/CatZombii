using System;

namespace Utility.Services.UI
{ 
    public abstract class UISimpleWindow : UIWindow
    {
        public event EventHandler CloseEvent;

        public override void Show()
        {
            gameObject.SetActive(true);
            State = WindowState.Open;
        }

        public override void Hide(Action onHide = null)
        {
            gameObject.SetActive(false);
            OnHided();
            onHide?.Invoke();
            
            State = WindowState.Close;
        }

        public virtual void Close()
        {
            CloseEvent?.Invoke(this, EventArgs.Empty);
            CloseEvent = null;
        }
    }
}