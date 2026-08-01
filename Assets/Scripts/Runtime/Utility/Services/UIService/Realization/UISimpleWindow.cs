using System;

namespace Utility.Services.UI
{ 
    public abstract class UISimpleWindow : UIWindow
    {
        public event EventHandler CloseEvent;

        public override void Show()
        {
            gameObject.SetActive(true);
            // base.Show() здесь не зовётся сознательно (окно показывается активностью,
            // а не Canvas.enabled), поэтому строки обновляем явно — иначе ветка
            // UISimpleWindow осталась бы единственной без локализации.
            ApplyLocalization();
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