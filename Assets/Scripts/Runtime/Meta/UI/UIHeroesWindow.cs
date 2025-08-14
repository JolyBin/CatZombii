using System;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Meta.UI
{
    public class UIHeroesWindow : UIWindow
    {
        public event Action OnClickSaveButton;
        public event Action OnClickHomeButton;
        public UIHero[] UIHeroList => _uiHeroes;

        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _homeButton;
        [SerializeField] private UIHero[] _uiHeroes;


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
            OnClickSaveButton = null;
            OnClickHomeButton = null;
            foreach (var uiHero in UIHeroList)
            {
                uiHero.ClearAction();
            }
            base.Hide(onHide);
        }
    }
}
