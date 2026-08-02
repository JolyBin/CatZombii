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
    /// | Открыт | щит | тёплый янтарный |
    /// | Пройден | щит + метка | зелёный |
    /// | Заперт  | щит с врисованным ЗАМКОМ | полностью обесцвеченный серый |
    /// | Босс    | + ромб позади щита | тот же, что у состояния |
    ///
    /// ПОЧЕМУ СПРАЙТ, А НЕ ТИНТ. Раньше состояние несла окраска одного и того же
    /// кружка, то есть жило целиком в канале цвета — ровно то, что docs/12 §4.4
    /// запрещает. Теперь у каждого состояния свой спрайт кита, и силуэт разный
    /// сам по себе: замок на запертом узле — это форма, а не оттенок. Поэтому
    /// <see cref="Image.color"/> тут ВСЕГДА белый: тинт поверх готового спрайта
    /// только замутил бы его.
    ///
    /// Перечёркивающей полосы больше нет: у <c>LockedLevel</c> замок уже врисован,
    /// и полоса поверх него перекрывала и замок, и номер узла — то есть отнимала
    /// читаемость, а не добавляла.
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
        [SerializeField] private Image _clearMarkImage;

        [Header("Силуэт состояния — ОСНОВНОЙ канал. Цвет спрайта только дублирует его")]
        [SerializeField] private Sprite _openSprite;
        [SerializeField] private Sprite _clearedSprite;
        [SerializeField] private Sprite _lockedSprite;

        [SerializeField] private TextMeshProUGUI _numberText;

        private int _number;

        public void Init(MapNodeView node)
        {
            _number = node.Number;

            Sprite shape = node.State switch
            {
                MapNodeState.Cleared => _clearedSprite,
                MapNodeState.Locked => _lockedSprite,
                _ => _openSprite,
            };

            // Спрайт может быть не назначен только при битом префабе. Обнулять Image
            // нельзя: пустой Image рисует белый квадрат — узел стал бы «сломанным»,
            // а не «без картинки».
            if (shape != null)
                _shapeImage.sprite = shape;

            _shapeImage.color = Color.white;

            _bossMarkImage.gameObject.SetActive(node.IsBoss);
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
