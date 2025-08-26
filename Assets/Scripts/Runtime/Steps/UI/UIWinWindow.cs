using System;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Core.Steps
{
    public class UIWinWindow : UIWindow
    {
        public event Action OnClickContinueButton;
        public event Action OnClickAdsButton;

        [SerializeField] private Button _continueButton, _adsButton;
        public override void Show()
        {
            _continueButton.onClick.AddListener(() => OnClickContinueButton?.Invoke());
            _adsButton.onClick.AddListener(() => OnClickAdsButton?.Invoke());
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            _continueButton.onClick.RemoveAllListeners();
            _adsButton.onClick.RemoveAllListeners();
            OnClickContinueButton = null;
            OnClickAdsButton = null;
            base.Hide(onHide);
        }
    }
}
