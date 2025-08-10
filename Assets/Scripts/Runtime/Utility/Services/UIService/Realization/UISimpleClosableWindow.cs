using System;
using UnityEngine;
using UnityEngine.UI;

namespace Utility.Services.UI
{ 
    public abstract class UISimpleClosableWindow : UISimpleWindow, IWindow
    {
        [SerializeField] private Button[] closeButtons;

        public Action CloseClickCommand;

        public override void Show()
        {
            base.Show();
            foreach (var button in closeButtons)
            {
                button.onClick.AddListener(CloseClickHandler);
            }
        }
        
        public override void Hide(Action onHide = null)
        {
            CloseClickCommand = null;
            base.Hide(onHide);
            foreach (var button in closeButtons)
            {
                button.onClick.RemoveListener(CloseClickHandler);
            }
        }
        
        private void CloseClickHandler()
        {
            CloseClickCommand?.Invoke();
        }
    }
}