using System;
using Meta.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.Localization;

namespace Meta.UI
{
    /// <summary>
    /// СТРОКА ЛАВКИ — одно предложение: рецепт или слот колоды (docs/10 §13.3).
    ///
    /// Дополнительных колб здесь нет и не будет: покупка снижения сложности отвергнута
    /// в §13.3 вместе с «мебелью с боевыми эффектами» из §9. Если такое предложение
    /// когда-нибудь появится в модели — это ошибка слоя данных, а не недоделка экрана.
    ///
    /// Ромб «новая стихия» — силуэт, а не цветная точка: он несёт единственный смысл,
    /// ради которого §13.3 вообще требует предупреждения, и обязан читаться без цвета
    /// (docs/12 §4.4, правило 2).
    /// </summary>
    public class UIShopOffer : MonoBehaviour, IAction
    {
        /// <summary>Нажата кнопка покупки. Отдаёт <see cref="ShopOfferView.Id"/>.</summary>
        public event Action<string> OnBuyClick;

        [SerializeField] private Button _buyButton;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _newElementMarkImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _noteText;
        [SerializeField] private TextMeshProUGUI _priceText;

        private string _id;

        public void Init(ShopOfferView offer)
        {
            _id = offer.Id;

            _nameText.text = offer.Kind == ShopOfferKind.DeckSlot && string.IsNullOrEmpty(offer.Name)
                ? Localization.Get(LocKeys.ShopSlotOffer, offer.SlotNumber)
                : offer.Name;

            if (offer.Icon != null)
                _iconImage.sprite = offer.Icon;

            _newElementMarkImage.gameObject.SetActive(offer.BringsNewElement);

            // Подпись под именем отвечает на единственный вопрос, который §13.1 делает
            // важным: станет ли пазл труднее. «Новых стихий не добавит» — такая же
            // информация, как и предупреждение, просто с другим знаком.
            _noteText.text = offer.Owned
                ? Localization.Get(LocKeys.ShopOwned)
                : !offer.Affordable
                    ? Localization.Get(LocKeys.ShopNotEnough)
                    : offer.BringsNewElement
                        ? Localization.Get(LocKeys.ShopConfirmNewElement, offer.ElementsAfter, offer.ElementsBefore)
                        : offer.Kind == ShopOfferKind.Recipe
                            ? Localization.Get(LocKeys.ShopConfirmSameElements)
                            : string.Empty;

            _priceText.text = offer.Price.ToString();
            _buyButton.interactable = !offer.Owned && offer.Affordable;

            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(RaiseBuy);
        }

        public void ClearAction()
        {
            _buyButton.onClick.RemoveAllListeners();
            OnBuyClick = null;
        }

        private void RaiseBuy() => OnBuyClick?.Invoke(_id);
    }
}
