using System;
using Meta.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.Localization;

namespace Meta.UI
{
    /// <summary>
    /// ОДИН СЛОТ КОЛОДЫ. Слотов 4 на старте, до 8 покупками (docs/10 §14.4).
    ///
    /// Закрытые слоты показываются вместе с открытыми намеренно: это витрина стока
    /// валюты (docs/10 §13.3), и игрок обязан видеть, что колода растёт. Состояния
    /// разведены силуэтом, а не только цветом: пустой слот — пунктирная рамка без
    /// содержимого, закрытый — та же рамка с перечёркивающей полосой (docs/12 §4.4).
    /// </summary>
    public class UIDeckSlot : MonoBehaviour, IAction
    {
        /// <summary>Клик по слоту. Отдаёт <see cref="DeckSlotView.Index"/>, 0-based.</summary>
        public event Action<int> OnClick;

        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _lockBarImage;
        [SerializeField] private TextMeshProUGUI _labelText;

        private int _index;

        public void Init(DeckSlotView slot)
        {
            _index = slot.Index;

            bool filled = slot.State == DeckSlotState.Filled && slot.Recipe != null;

            _iconImage.gameObject.SetActive(filled);
            _lockBarImage.gameObject.SetActive(slot.State == DeckSlotState.Locked);

            if (filled && slot.Recipe.Icon != null)
                _iconImage.sprite = slot.Recipe.Icon;

            _labelText.text = slot.State switch
            {
                DeckSlotState.Filled => slot.Recipe.Name,
                DeckSlotState.Locked => Localization.Get(LocKeys.DeckSlotLocked, slot.UnlockPrice),
                _ => Localization.Get(LocKeys.DeckSlotEmpty),
            };

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(RaiseClick);
        }

        public void ClearAction()
        {
            _button.onClick.RemoveAllListeners();
            OnClick = null;
        }

        private void RaiseClick() => OnClick?.Invoke(_index);
    }
}
