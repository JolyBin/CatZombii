using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.Localization;
using Utility.Services.UI;

namespace Core.Steps
{
    public class UIWinWindow : UIWindow
    {
        public event Action OnClickContinueButton;
        public event Action OnClickAdsButton;

        [SerializeField] private Button _continueButton, _adsButton;

        /// <summary>
        /// Награда за бой. Может быть не назначен — тогда окно молча остаётся без
        /// цифры, но бой всё равно оплачивается: начисление живёт в
        /// <c>HomeController.RegisterNodeCleared</c>, а не здесь.
        /// </summary>
        [SerializeField] private TextMeshProUGUI _rewardText;

        /// <summary>
        /// ПОКАЗАТЬ НАЧИСЛЕННУЮ НАГРАДУ. Окно ничего не считает: число ему отдаёт
        /// тот единственный метод, который награду и начисляет (docs/10 §15.3).
        /// Второй расчёт здесь разъехался бы с первым при первой же правке экономики.
        /// </summary>
        public void SetReward(int reward)
        {
            if (_rewardText != null)
                _rewardText.text = Localization.Get(LocKeys.WinReward, reward);
        }

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
