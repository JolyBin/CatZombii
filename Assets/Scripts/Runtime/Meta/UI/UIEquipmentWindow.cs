using Core.Spells;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Collections;
using Utility.Services.UI;

namespace Meta.UI
{
    public class UIEquipmentWindow : UIWindow
    {
        public event Action OnBackButtonClick;

        [SerializeField] private Button _backButton;
        [SerializeField] private Image _heroIconImage, _heroMainColorImage;
        [SerializeField] private TextMeshProUGUI _heroNameText;
        [SerializeField] private TextMeshProUGUI _classNameText;
        [SerializeField] private RectTransform _combinationCointanier;
        [SerializeField] private UICombination _uiCombinationPrefab;

        private Pool<UICombination> _combinationPool;

        private void Start()
        {
            Func<UICombination> initializer = new(() => Instantiate(_uiCombinationPrefab, _combinationCointanier));
            Func<UICombination, bool> predicate = new((uiCombination) => !uiCombination.gameObject.activeInHierarchy);
            Action<UICombination> returnToPoolAction = new((uiCombination) =>
            {
                uiCombination.gameObject.SetActive(false);
            });

            _combinationPool = new(initializer, predicate, returnToPoolAction, 6);
        }

        public override void Show()
        {
            base.Show();
            _backButton.onClick.AddListener(() => OnBackButtonClick?.Invoke());
        }

        public override void Hide(Action onHide = null)
        {
            base.Hide(onHide);
            OnBackButtonClick = null;
            _backButton.onClick.RemoveAllListeners();
            _combinationPool?.ReturnObjectsToPool();
        }

        public void SetBook(Book book)
        {
            _heroMainColorImage.color = book.UniqElements[0].Color;
            _heroIconImage.sprite = book.HeroIcon;
            _heroNameText.text = book.NameHero;
            _classNameText.text = book.ClassHero;
            foreach (Combination combination in book.Combinations)
            {
                UICombination uICombination = _combinationPool.GetFreePooledObject();
                uICombination.gameObject.SetActive(true);
                uICombination.Show(combination);
            }
        }    

    }
}
