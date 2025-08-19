using Core.Spells;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Core.Steps.UI
{
    public class UIBattleWindow : UIWindow
    {
        public event Action OnClickHomeButton;

        [SerializeField] private Button _homeButton;
        [SerializeField] private TextMeshProUGUI _nameHeroText;
        [SerializeField] private Image _heroIconImage;
        [SerializeField] private Image _classIconImage;
        [SerializeField] private Image _healthFiil;

        public void Init(Book currentBook)
        {
            _nameHeroText.text = currentBook.NameHero;
            _heroIconImage.sprite = currentBook.HeroIcon;
            _classIconImage.sprite = currentBook.IconClass;
        }

        public override void Show()
        {
            _homeButton.onClick.AddListener(() => OnClickHomeButton?.Invoke()); 
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            OnClickHomeButton = null;
            _homeButton.onClick.RemoveAllListeners();
            base.Hide(onHide);
        }

        public void SetHealth(int currentHP, int maxHP)
        {
            _healthFiil.fillAmount = (float) currentHP / maxHP;
        }
    }
}
