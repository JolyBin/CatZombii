using System;
using System.Collections.Generic;
using Meta.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Collections;
using Utility.Services.Localization;
using Utility.Services.UI;

namespace Meta.UI
{
    /// <summary>
    /// ЭКРАН КОЛОДЫ (docs/10 §13.1 и §13.2).
    ///
    /// Главное на этом экране — НЕ слоты и не карточки, а строка «стихий в колбах: N».
    /// §13.1 объявляет законом, что каждое новое заклинание делает пазл труднее:
    /// стихии сыплются из ЭКИПИРОВАННОЙ колоды, значит «взял ещё рецепт» = «собрать
    /// четыре одинаковых стало сложнее». Закон работает в коде сам собой
    /// (<c>Book.OnValidate</c> → <c>FlaskController</c>), но игрок его не увидит,
    /// если не написать. Поэтому счётчик стихий стоит выше слотов и выше коллекции.
    ///
    /// Ряд кружков рядом со счётчиком фиксированный — семь, по числу стихий
    /// (docs/10 §13.5: «восьмого элемента не будет»). Лишние гасятся.
    ///
    /// ТОЧКА ПОДКЛЮЧЕНИЯ ДАННЫХ — <see cref="Init"/>.
    /// </summary>
    public class UIDeckWindow : UIMetaWindow
    {
        /// <summary>Тап по слоту колоды. Отдаёт индекс слота, 0-based.</summary>
        public event Action<int> OnSlotClick;

        /// <summary>Тап по карточке рецепта в коллекции. Отдаёт <see cref="RecipeView.Id"/>.</summary>
        public event Action<string> OnRecipeClick;

        public event Action OnBackClick;
        public event Action OnShopClick;

        [SerializeField] private Button _backButton;
        [SerializeField] private Button _shopButton;
        [SerializeField] private TextMeshProUGUI _yarnText;

        [Header("Закон §13.1 — счётчик стихий")]
        [SerializeField] private TextMeshProUGUI _elementsCountText;

        [Tooltip("Ровно семь: стихий семь и восьмой не будет (docs/10 §13.5). Лишние гасятся.")]
        [SerializeField] private Image[] _elementIcons;

        [Header("Слоты")]
        [SerializeField] private TextMeshProUGUI _slotsText;
        [SerializeField] private RectTransform _slotsContainer;
        [SerializeField] private UIDeckSlot _slotPrefab;

        [Header("Коллекция")]
        [SerializeField] private RectTransform _collectionContainer;
        [SerializeField] private UIRecipeCard _recipeCardPrefab;

        private Pool<UIDeckSlot> _slotPool;
        private Pool<UIRecipeCard> _cardPool;
        private readonly List<UIDeckSlot> _shownSlots = new();
        private readonly List<UIRecipeCard> _shownCards = new();

        /// <summary>
        /// ЕДИНСТВЕННАЯ точка подключения слоя данных. Зовётся ПОСЛЕ <see cref="Show"/>.
        /// Повторный вызов перерисовывает экран целиком — так обновляется колода
        /// после «надеть/снять», не пересоздавая окно.
        /// </summary>
        public void Init(DeckScreenModel model)
        {
            ReleaseItems();
            SetRefusal(model?.Refusal);

            if (model == null)
                return;

            _yarnText.text = model.Yarn.ToString();
            _elementsCountText.text = Localization.Get(LocKeys.DeckElementsCount, model.Elements.Length);
            _slotsText.text = Localization.Get(LocKeys.DeckSlots, model.SlotsUnlocked, model.SlotsMax);

            for (int i = 0; i < _elementIcons.Length; i++)
            {
                bool used = i < model.Elements.Length;
                _elementIcons[i].gameObject.SetActive(used);

                if (!used)
                    continue;

                _elementIcons[i].color = model.Elements[i].Color;

                // Спрайта у стихии может не быть — тогда остаётся запасной силуэт
                // из префаба. Обнулять нельзя: пустой Image — это белый квадрат.
                if (model.Elements[i].Icon != null)
                    _elementIcons[i].sprite = model.Elements[i].Icon;
            }

            _slotPool ??= CreateSlotPool();
            _cardPool ??= CreateCardPool();

            foreach (DeckSlotView slot in model.Slots)
            {
                UIDeckSlot view = _slotPool.GetFreePooledObject();
                view.gameObject.SetActive(true);
                view.Init(slot);
                view.OnClick += RaiseSlotClick;
                _shownSlots.Add(view);
            }

            foreach (RecipeView recipe in model.Collection)
            {
                UIRecipeCard card = _cardPool.GetFreePooledObject();
                card.gameObject.SetActive(true);
                card.Init(recipe);
                card.OnClick += RaiseRecipeClick;
                _shownCards.Add(card);
            }
        }

        public override void Show()
        {
            base.Show();
            _backButton.onClick.AddListener(RaiseBack);
            _shopButton.onClick.AddListener(RaiseShop);
        }

        public override void Hide(Action onHide = null)
        {
            _backButton.onClick.RemoveAllListeners();
            _shopButton.onClick.RemoveAllListeners();

            OnBackClick = null;
            OnShopClick = null;
            OnSlotClick = null;
            OnRecipeClick = null;

            ReleaseItems();
            base.Hide(onHide);
        }

        /// <summary>
        /// Пул объекты не уничтожает, поэтому подписка прошлого показа пережила бы
        /// закрытие окна вместе со ссылкой на прошлый контроллер (docs/04).
        /// </summary>
        private void ReleaseItems()
        {
            foreach (UIDeckSlot slot in _shownSlots)
            {
                slot.OnClick -= RaiseSlotClick;
                slot.ClearAction();
            }

            foreach (UIRecipeCard card in _shownCards)
            {
                card.OnClick -= RaiseRecipeClick;
                card.ClearAction();
            }

            _shownSlots.Clear();
            _shownCards.Clear();
            _slotPool?.ReturnObjectsToPool();
            _cardPool?.ReturnObjectsToPool();
        }

        private Pool<UIDeckSlot> CreateSlotPool()
        {
            Func<UIDeckSlot> initializer = () => Instantiate(_slotPrefab, _slotsContainer);
            Func<UIDeckSlot, bool> isFree = slot => !slot.gameObject.activeSelf;
            Action<UIDeckSlot> release = slot => slot.gameObject.SetActive(false);
            return new Pool<UIDeckSlot>(initializer, isFree, release, 8);
        }

        private Pool<UIRecipeCard> CreateCardPool()
        {
            Func<UIRecipeCard> initializer = () => Instantiate(_recipeCardPrefab, _collectionContainer);
            Func<UIRecipeCard, bool> isFree = card => !card.gameObject.activeSelf;
            Action<UIRecipeCard> release = card => card.gameObject.SetActive(false);
            return new Pool<UIRecipeCard>(initializer, isFree, release, 9);
        }

        private void RaiseSlotClick(int index) => OnSlotClick?.Invoke(index);

        private void RaiseRecipeClick(string id) => OnRecipeClick?.Invoke(id);

        private void RaiseBack() => OnBackClick?.Invoke();

        private void RaiseShop() => OnShopClick?.Invoke();
    }
}
