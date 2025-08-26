using System;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Core.Steps
{
    public class UILoseWindow : UIWindow
    {
        public event Action OnClickContinueButton;

        [SerializeField] private Button _continueButton;
        public override void Show()
        {
            _continueButton.onClick.AddListener(() => OnClickContinueButton?.Invoke());
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            _continueButton.onClick.RemoveAllListeners();
            OnClickContinueButton = null;
            base.Hide(onHide);
        }
    }
}
