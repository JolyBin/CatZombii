using System;using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Meta.UI
{
    public class HomeWindow : UIWindow
    {
        public event Action OnClickPlayButton;
        public event Action OnClickCharactersButton;

        [SerializeField] private Button _playButton;
        [SerializeField] private Button _charactersButton;


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
            base.Hide(onHide);
        }
    }
}
