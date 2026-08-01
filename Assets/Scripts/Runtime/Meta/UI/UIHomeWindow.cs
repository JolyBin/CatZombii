using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.Localization;
using Utility.Services.UI;

namespace Meta.UI
{
    public class UIHomeWindow : UIWindow
    {
        public event Action OnClickPlayButton;
        public event Action OnClickCharactersButton;

        [SerializeField] private Button _playButton;
        [SerializeField] private Button _charactersButton;
        [SerializeField] private TextMeshProUGUI _currentLevelText;


        public override void Show()
        {
            _playButton.onClick.AddListener(() => OnClickPlayButton?.Invoke());
            _charactersButton.onClick.AddListener(() => OnClickCharactersButton?.Invoke());
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            _playButton.onClick.RemoveAllListeners();
            _charactersButton.onClick.RemoveAllListeners();
            OnClickPlayButton = null;
            OnClickCharactersButton = null;
            base.Hide(onHide);
        }

        public void SetLevelValue(int value)
            => _currentLevelText.text = Localization.Get(LocKeys.HomeLevel, value);
    }
}
