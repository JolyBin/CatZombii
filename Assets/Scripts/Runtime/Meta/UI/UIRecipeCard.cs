using System;
using Meta.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.Localization;

namespace Meta.UI
{
    /// <summary>
    /// КАРТОЧКА РЕЦЕПТА в списке «что есть в книге» (экран колоды).
    ///
    /// Показывает цепочку стихий, а не только имя: рецепт `Огонь &gt; Огонь &gt; Сила`
    /// стоит трёх слотов (docs/10 §13.2), и цена в слотах обязана быть видна там же,
    /// где принимается решение. Кружки цепочки берут спрайт из <see cref="ElementView.Icon"/>,
    /// а цвет — вторым каналом (docs/12 §4.4, правило 2).
    /// </summary>
    public class UIRecipeCard : MonoBehaviour, IAction
    {
        /// <summary>Клик по карточке. Отдаёт <see cref="RecipeView.Id"/>.</summary>
        public event Action<string> OnClick;

        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _stateText;
        [SerializeField] private Image _equippedMarkImage;

        [Tooltip("Кружки цепочки стихий. Их столько, какова максимальная длина рецепта (docs/10 §14 — три).")]
        [SerializeField] private Image[] _chainIcons;

        private string _id;

        public void Init(RecipeView recipe)
        {
            _id = recipe.Id;
            _nameText.text = recipe.Name;

            if (recipe.Icon != null)
                _iconImage.sprite = recipe.Icon;

            for (int i = 0; i < _chainIcons.Length; i++)
            {
                bool used = recipe.Chain != null && i < recipe.Chain.Length;
                _chainIcons[i].gameObject.SetActive(used);

                if (!used)
                    continue;

                ElementView element = recipe.Chain[i];
                _chainIcons[i].color = element.Color;

                // Спрайта у стихии может не быть — тогда остаётся тот, что лежит
                // в префабе. Обнулять его нельзя: пустой Image рисует белый квадрат.
                if (element.Icon != null)
                    _chainIcons[i].sprite = element.Icon;
            }

            _equippedMarkImage.gameObject.SetActive(recipe.Equipped);
            _stateText.text = Localization.Get(recipe.Equipped ? LocKeys.DeckEquipped : LocKeys.DeckEquip);

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(RaiseClick);
        }

        public void ClearAction()
        {
            _button.onClick.RemoveAllListeners();
            OnClick = null;
        }

        private void RaiseClick() => OnClick?.Invoke(_id);
    }
}
