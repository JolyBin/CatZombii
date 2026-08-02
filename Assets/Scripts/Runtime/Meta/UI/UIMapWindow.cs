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
    /// ЭКРАН КАРТЫ УЗЛОВ (docs/10 §13.4): 15 узлов, боссы на 5, 10 и 15,
    /// узлы ПЕРЕПРОХОДИМЫ.
    ///
    /// Число узлов приходит длиной <see cref="MapScreenModel.Nodes"/>, а не константой:
    /// уровней в сборке сегодня семь, и врать про пятнадцать нельзя — недостающие
    /// приходят состоянием <see cref="MapNodeState.Locked"/> и видны как запертые.
    /// Как только уровней станет больше, экран не тронется.
    ///
    /// ТОЧКА ПОДКЛЮЧЕНИЯ ДАННЫХ — <see cref="Init"/>. Всё остальное окно про данные
    /// не знает: ни про <c>BattleConfig</c>, ни про профиль, ни про то, откуда
    /// берётся признак «пройден».
    /// </summary>
    public class UIMapWindow : UIWindow
    {
        /// <summary>Выбран узел. Число — <see cref="MapNodeView.Number"/>, 1-based.</summary>
        public event Action<int> OnNodeClick;

        public event Action OnBackClick;
        public event Action OnDeckClick;
        public event Action OnShopClick;

        [SerializeField] private Button _backButton;
        [SerializeField] private Button _deckButton;
        [SerializeField] private Button _shopButton;
        [SerializeField] private TextMeshProUGUI _yarnText;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private RectTransform _nodesContainer;
        [SerializeField] private UIMapNode _nodePrefab;

        private Pool<UIMapNode> _nodePool;
        private readonly List<UIMapNode> _shownNodes = new();

        /// <summary>
        /// ЕДИНСТВЕННАЯ точка подключения слоя данных. Зовётся ПОСЛЕ <see cref="Show"/>.
        /// Повторный вызов перерисовывает экран целиком — так обновляется карта
        /// после победы, не пересоздавая окно.
        /// </summary>
        public void Init(MapScreenModel model)
        {
            ReleaseNodes();

            if (model == null)
                return;

            _yarnText.text = model.Yarn.ToString();

            int cleared = 0;
            foreach (MapNodeView node in model.Nodes)
            {
                if (node.State == MapNodeState.Cleared)
                    cleared++;
            }

            _progressText.text = Localization.Get(LocKeys.MapProgress, cleared, model.Nodes.Length);

            _nodePool ??= CreatePool();

            foreach (MapNodeView node in model.Nodes)
            {
                UIMapNode view = _nodePool.GetFreePooledObject();
                view.gameObject.SetActive(true);
                view.Init(node);
                view.OnClick += RaiseNodeClick;
                _shownNodes.Add(view);
            }
        }

        public override void Show()
        {
            base.Show();
            _backButton.onClick.AddListener(RaiseBack);
            _deckButton.onClick.AddListener(RaiseDeck);
            _shopButton.onClick.AddListener(RaiseShop);
        }

        public override void Hide(Action onHide = null)
        {
            _backButton.onClick.RemoveAllListeners();
            _deckButton.onClick.RemoveAllListeners();
            _shopButton.onClick.RemoveAllListeners();

            OnBackClick = null;
            OnDeckClick = null;
            OnShopClick = null;
            OnNodeClick = null;

            ReleaseNodes();
            base.Hide(onHide);
        }

        /// <summary>
        /// Отписка от КАЖДОГО узла отдельно. Пул объекты не уничтожает — он их
        /// переиспользует, и подписка прошлого показа пережила бы закрытие окна,
        /// а с ней и ссылка на прошлый контроллер (docs/04, «Освобождение ресурсов»).
        /// </summary>
        private void ReleaseNodes()
        {
            foreach (UIMapNode node in _shownNodes)
            {
                node.OnClick -= RaiseNodeClick;
                node.ClearAction();
            }

            _shownNodes.Clear();
            _nodePool?.ReturnObjectsToPool();
        }

        private Pool<UIMapNode> CreatePool()
        {
            Func<UIMapNode> initializer = () => Instantiate(_nodePrefab, _nodesContainer);
            Func<UIMapNode, bool> isFree = node => !node.gameObject.activeSelf;
            Action<UIMapNode> release = node => node.gameObject.SetActive(false);
            return new Pool<UIMapNode>(initializer, isFree, release, 15);
        }

        private void RaiseNodeClick(int number) => OnNodeClick?.Invoke(number);

        private void RaiseBack() => OnBackClick?.Invoke();

        private void RaiseDeck() => OnDeckClick?.Invoke();

        private void RaiseShop() => OnShopClick?.Invoke();
    }
}
