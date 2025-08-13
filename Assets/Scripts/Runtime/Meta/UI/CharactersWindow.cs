using System;using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Meta.UI
{
    public class CharactersWindow : UIWindow
    {
        public event Action OnClickSaveButton;
        public event Action OnClickHomeButton;

        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _homeButton;


        public override void Show()
        {
            _saveButton.onClick.AddListener(() => OnClickSaveButton?.Invoke());
            _homeButton.onClick.AddListener(() => OnClickHomeButton?.Invoke());
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            _saveButton.onClick.RemoveAllListeners();
            _homeButton.onClick.RemoveAllListeners();
            base.Hide(onHide);
        }
    }
}
