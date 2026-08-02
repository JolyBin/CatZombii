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
    /// ЛАВКА ЗАКЛИНАНИЙ (docs/10 §13.3). Два стока валюты и ровно два:
    /// рецепты и слоты колоды. Дополнительных колб здесь НЕТ — покупка снижения
    /// сложности отвергнута в §13.3.
    ///
    /// Экран отвечает за одну неочевидную вещь: покупка рецепта с новой стихией —
    /// это НЕ чистое усиление. Она делает пазл труднее (§13.1), поэтому подтверждение
    /// показывается ДО списания и говорит прямо, сколько стихий станет вместо скольких.
    /// Подтверждение — панель внутри окна, а не отдельное <c>UIWindow</c>: реестр
    /// <c>UIService</c> собирается один раз в <c>Awake</c>, и каждое лишнее окно в нём —
    /// ещё один способ тихо получить <c>null</c> из <c>Show&lt;T&gt;()</c>.
    ///
    /// ТОЧКА ПОДКЛЮЧЕНИЯ ДАННЫХ — <see cref="Init"/>.
    /// </summary>
    public class UIShopWindow : UIWindow
    {
        /// <summary>
        /// Покупка ПОДТВЕРЖДЕНА игроком. Отдаёт <see cref="ShopOfferView.Id"/>.
        /// Только это событие означает «списывай клубки»: нажатие на «купить»
        /// у предложения с новой стихией сначала открывает подтверждение.
        /// </summary>
        public event Action<string> OnPurchaseConfirmed;

        public event Action OnBackClick;

        [SerializeField] private Button _backButton;
        [SerializeField] private TextMeshProUGUI _yarnText;
        [SerializeField] private RectTransform _offersContainer;
        [SerializeField] private UIShopOffer _offerPrefab;

        [Header("Подтверждение покупки со сменой числа стихий (§13.3)")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TextMeshProUGUI _confirmOfferText;
        [SerializeField] private TextMeshProUGUI _confirmWarningText;
        [SerializeField] private Button _confirmYesButton;
        [SerializeField] private Button _confirmNoButton;

        private Pool<UIShopOffer> _offerPool;
        private readonly List<UIShopOffer> _shownOffers = new();
        private readonly Dictionary<string, ShopOfferView> _offersById = new();

        private string _pendingOfferId;

        /// <summary>
        /// ЕДИНСТВЕННАЯ точка подключения слоя данных. Зовётся ПОСЛЕ <see cref="Show"/>.
        /// Повторный вызов перерисовывает витрину целиком — так она обновляется
        /// после покупки, не пересоздавая окно.
        /// </summary>
        public void Init(ShopScreenModel model)
        {
            ReleaseOffers();
            CloseConfirm();

            if (model == null)
                return;

            _yarnText.text = model.Yarn.ToString();
            _offerPool ??= CreatePool();

            foreach (ShopOfferView offer in model.Offers)
            {
                _offersById[offer.Id] = offer;

                UIShopOffer view = _offerPool.GetFreePooledObject();
                view.gameObject.SetActive(true);
                view.Init(offer);
                view.OnBuyClick += HandleBuyClick;
                _shownOffers.Add(view);
            }
        }

        public override void Show()
        {
            base.Show();
            _backButton.onClick.AddListener(RaiseBack);
            _confirmYesButton.onClick.AddListener(HandleConfirmYes);
            _confirmNoButton.onClick.AddListener(CloseConfirm);
        }

        public override void Hide(Action onHide = null)
        {
            _backButton.onClick.RemoveAllListeners();
            _confirmYesButton.onClick.RemoveAllListeners();
            _confirmNoButton.onClick.RemoveAllListeners();

            OnBackClick = null;
            OnPurchaseConfirmed = null;

            ReleaseOffers();
            CloseConfirm();
            base.Hide(onHide);
        }

        /// <summary>
        /// Кнопка «купить» у строки витрины. Рецепт с новой стихией не покупается
        /// сразу — сначала честное предупреждение (§13.3). Всё остальное уходит
        /// наружу мгновенно: лишний вопрос на каждую покупку — это налог на игрока.
        /// </summary>
        private void HandleBuyClick(string offerId)
        {
            if (!_offersById.TryGetValue(offerId, out ShopOfferView offer))
                return;

            if (!offer.BringsNewElement)
            {
                OnPurchaseConfirmed?.Invoke(offerId);
                return;
            }

            _pendingOfferId = offerId;
            _confirmOfferText.text = offer.Name;
            _confirmWarningText.text = Localization.Get(LocKeys.ShopConfirmNewElement,
                                                       offer.ElementsAfter, offer.ElementsBefore);
            _confirmPanel.SetActive(true);
        }

        private void HandleConfirmYes()
        {
            string offerId = _pendingOfferId;
            CloseConfirm();

            if (!string.IsNullOrEmpty(offerId))
                OnPurchaseConfirmed?.Invoke(offerId);
        }

        private void CloseConfirm()
        {
            _pendingOfferId = null;
            _confirmPanel.SetActive(false);
        }

        /// <summary>
        /// Пул объекты не уничтожает, поэтому подписка прошлого показа пережила бы
        /// закрытие окна вместе со ссылкой на прошлый контроллер (docs/04).
        /// </summary>
        private void ReleaseOffers()
        {
            foreach (UIShopOffer offer in _shownOffers)
            {
                offer.OnBuyClick -= HandleBuyClick;
                offer.ClearAction();
            }

            _shownOffers.Clear();
            _offersById.Clear();
            _offerPool?.ReturnObjectsToPool();
        }

        private Pool<UIShopOffer> CreatePool()
        {
            Func<UIShopOffer> initializer = () => Instantiate(_offerPrefab, _offersContainer);
            Func<UIShopOffer, bool> isFree = offer => !offer.gameObject.activeSelf;
            Action<UIShopOffer> release = offer => offer.gameObject.SetActive(false);
            return new Pool<UIShopOffer>(initializer, isFree, release, 10);
        }

        private void RaiseBack() => OnBackClick?.Invoke();
    }
}
