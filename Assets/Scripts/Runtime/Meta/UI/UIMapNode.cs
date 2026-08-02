using System;
using Meta.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.Localization;

namespace Meta.UI
{
    /// <summary>
    /// ОДИН УЗЕЛ КАРТЫ. Живёт в префабе, создаётся пулом из <see cref="UIMapWindow"/>.
    ///
    /// Три состояния и признак босса разведены НЕ ЦВЕТОМ (docs/12 §4.4, правило 2):
    ///
    /// | Состояние | Силуэт | Цвет (дублирует, не несёт) |
    /// |---|---|---|
    /// | Открыт | круг | тёплый золотой |
    /// | Пройден | круг + галка | зелёный |
    /// | Заперт  | круг + перечёркивающая полоса | обесцвеченный серый |
    /// | Босс    | + ромб позади круга | тот же, что у состояния |
    ///
    /// Красного здесь нет ни в одном состоянии: он зарезервирован под угрозу
    /// (docs/11 §1.4). «Заперто» — это не угроза, это отсутствие возможности.
    /// </summary>
    public class UIMapNode : MonoBehaviour, IAction
    {
        /// <summary>Клик по узлу. Отдаёт <see cref="MapNodeView.Number"/>, 1-based.</summary>
        public event Action<int> OnClick;

        [SerializeField] private Button _button;
        [SerializeField] private Image _shapeImage;
        [SerializeField] private Image _bossMarkImage;
        [SerializeField] private Image _lockBarImage;
        [SerializeField] private Image _clearMarkImage;
        [SerializeField] private TextMeshProUGUI _numberText;

        [Header("Цвета — ДУБЛИРУЮЩИЙ канал, силуэт задаётся отдельными объектами")]
        [SerializeField] private Color _openColor = new Color(0.98f, 0.76f, 0.28f);
        [SerializeField] private Color _clearedColor = new Color(0.42f, 0.74f, 0.42f);
        [SerializeField] private Color _lockedColor = new Color(0.38f, 0.36f, 0.42f);

        private int _number;

        public void Init(MapNodeView node)
        {
            _number = node.Number;

            _shapeImage.color = node.State switch
            {
                MapNodeState.Cleared => _clearedColor,
                MapNodeState.Locked => _lockedColor,
                _ => _openColor,
            };

            _bossMarkImage.gameObject.SetActive(node.IsBoss);
            _lockBarImage.gameObject.SetActive(node.State == MapNodeState.Locked);
            _clearMarkImage.gameObject.SetActive(node.State == MapNodeState.Cleared);

            _numberText.text = Localization.Get(node.IsBoss ? LocKeys.MapNodeBoss : LocKeys.MapNode, node.Number);

            // Запертый узел не кликается. Именно поэтому у него ЕЩЁ и силуэт другой:
            // «не нажимается» без видимой причины читается как поломка, а не как правило.
            _button.interactable = node.State != MapNodeState.Locked;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(RaiseClick);
        }

        public void ClearAction()
        {
            _button.onClick.RemoveAllListeners();
            OnClick = null;
        }

        private void RaiseClick() => OnClick?.Invoke(_number);
    }
}
