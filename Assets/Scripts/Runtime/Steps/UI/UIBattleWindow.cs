using Core.Spells;
using System;
using System.Linq;
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
        [SerializeField] private Image _heroImage;
        [SerializeField] private Image _classIconImage;
        [SerializeField] private Image _healthFiil;
        [SerializeField] private TextMeshProUGUI _healthValueText;
        [SerializeField] private UIUnitPosition[] _enemyPositions;
        [SerializeField] private UIUnitPosition[] _friendlyPositions;
        [SerializeField] private TextMeshProUGUI _waveText;

        private int _currentEnemyPositionIndex;
        private int _currentFriendlyPositionIndex;

        public void Init(Book currentBook)
        {
            _nameHeroText.text = currentBook.NameHero;
            _heroIconImage.sprite = currentBook.HeroIcon;
            _classIconImage.sprite = currentBook.IconClass;
        }

        public override void Show()
        {
            foreach (var unitPosition in _enemyPositions)
            {
                unitPosition.SetFree();
            }
            foreach (var unitPosition in _friendlyPositions)
            {
                unitPosition.SetFree();
            }
            _homeButton.onClick.AddListener(() => OnClickHomeButton?.Invoke()); 
            base.Show();
        }

        public void SetHero(Book heroBook)
        {
            
            _heroIconImage.sprite = heroBook.HeroIcon;
            _classIconImage.sprite = heroBook.IconClass;
            _heroImage.sprite = heroBook.HeroIcon;
            _nameHeroText.text = heroBook.NameHero;
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
            _healthValueText.text = currentHP.ToString();
        }

        public void SetWave(int currentWave, int maxWave) => _waveText.text = $"Wave: {currentWave}/{maxWave}";

        public UIUnitPosition SetEnemyPosition() => _enemyPositions.First(x => x.IsFree);
        public UIUnitPosition SetFriendPosition() => _friendlyPositions.First(x => x.IsFree);
    }
}