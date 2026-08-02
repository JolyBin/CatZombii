using System;
using Meta.Models;
using Meta.UI;
using Utility.Services.UI;

namespace Meta
{
    /// <summary>
    /// ШОВ МЕЖДУ ТРЕМЯ ЭКРАНАМИ МЕТЫ И СЛОЕМ ДАННЫХ. Один класс, одна ответственность:
    /// показать нужное окно, скормить ему модель и переслать наружу то, что нажал игрок.
    ///
    /// Здесь НЕТ ни профиля, ни цен, ни правил разблокировки — и это главное свойство
    /// класса, а не упущение. Экран не должен знать, откуда взялось «узел 6 заперт»:
    /// сегодня это <c>Saves.Profile.LevelIndex</c>, завтра карта с ветвлением.
    ///
    /// ПОДКЛЮЧЕНИЕ — три делегата и пять событий:
    ///
    /// <code>
    /// var screens = new MetaScreensController(uiService)
    /// {
    ///     MapModelSource  = () => BuildMapModel(),
    ///     DeckModelSource = () => BuildDeckModel(),
    ///     ShopModelSource = () => BuildShopModel(),
    /// };
    /// screens.OnNodeChosen        += number => StartLevel(number - 1);
    /// screens.OnRecipeChosen      += id     => { ToggleEquip(id); screens.Refresh(); };
    /// screens.OnDeckSlotChosen    += index  => { ClearSlot(index); screens.Refresh(); };
    /// screens.OnPurchaseConfirmed += id     => { Buy(id);          screens.Refresh(); };
    /// screens.OnExit              += ()     => screens.Exit();
    /// screens.ShowMap();
    /// </code>
    ///
    /// Делегаты-источники, а не «передай модель в ShowMap», ровно из-за
    /// <see cref="Refresh"/>: после покупки экран обязан перерисоваться сам, а иначе
    /// каждый вызывающий писал бы это руками и однажды забыл бы.
    /// </summary>
    public sealed class MetaScreensController : IAction
    {
        /// <summary>Какой из трёх экранов открыт. Нужен только <see cref="Refresh"/>.</summary>
        private enum Screen
        {
            None = 0,
            Map = 1,
            Deck = 2,
            Shop = 3,
        }

        /// <summary>Собрать модель карты. Обязателен, если карту показывают.</summary>
        public Func<MapScreenModel> MapModelSource;

        /// <summary>Собрать модель колоды. Обязателен, если колоду показывают.</summary>
        public Func<DeckScreenModel> DeckModelSource;

        /// <summary>Собрать модель лавки. Обязателен, если лавку показывают.</summary>
        public Func<ShopScreenModel> ShopModelSource;

        /// <summary>Игрок выбрал узел. Число 1-based — <see cref="MapNodeView.Number"/>.</summary>
        public event Action<int> OnNodeChosen;

        /// <summary>Тап по слоту колоды, 0-based. Обычно означает «снять рецепт со слота».</summary>
        public event Action<int> OnDeckSlotChosen;

        /// <summary>Тап по карточке рецепта. Обычно означает «надеть/снять».</summary>
        public event Action<string> OnRecipeChosen;

        /// <summary>
        /// Покупка ПОДТВЕРЖДЕНА. Предупреждение про новую стихию (§13.3) уже показано
        /// и принято — здесь можно списывать клубки.
        /// </summary>
        public event Action<string> OnPurchaseConfirmed;

        /// <summary>Игрок вышел из меты (кнопка «назад» на карте).</summary>
        public event Action OnExit;

        private readonly IUIService _uiService;

        private UIMapWindow _mapWindow;
        private UIDeckWindow _deckWindow;
        private UIShopWindow _shopWindow;

        private Screen _current = Screen.None;

        public MetaScreensController(IUIService uiService) => _uiService = uiService;

        public void ShowMap()
        {
            HideCurrent();

            _mapWindow = _uiService.Show<UIMapWindow>();
            if (_mapWindow == null)
                return;

            _mapWindow.OnNodeClick += RaiseNodeChosen;
            _mapWindow.OnDeckClick += ShowDeck;
            _mapWindow.OnShopClick += ShowShop;
            _mapWindow.OnBackClick += RaiseExit;

            _mapWindow.Init(MapModelSource?.Invoke());
            _current = Screen.Map;
        }

        public void ShowDeck()
        {
            HideCurrent();

            _deckWindow = _uiService.Show<UIDeckWindow>();
            if (_deckWindow == null)
                return;

            _deckWindow.OnSlotClick += RaiseSlotChosen;
            _deckWindow.OnRecipeClick += RaiseRecipeChosen;
            _deckWindow.OnShopClick += ShowShop;
            _deckWindow.OnBackClick += ShowMap;

            _deckWindow.Init(DeckModelSource?.Invoke());
            _current = Screen.Deck;
        }

        public void ShowShop()
        {
            HideCurrent();

            _shopWindow = _uiService.Show<UIShopWindow>();
            if (_shopWindow == null)
                return;

            _shopWindow.OnPurchaseConfirmed += RaisePurchaseConfirmed;
            _shopWindow.OnBackClick += ShowDeck;

            _shopWindow.Init(ShopModelSource?.Invoke());
            _current = Screen.Shop;
        }

        /// <summary>
        /// Перерисовать ОТКРЫТЫЙ экран новой моделью, не пересоздавая окно.
        /// Зовётся слоем данных после любой мутации: купил, надел, снял.
        /// </summary>
        public void Refresh()
        {
            switch (_current)
            {
                case Screen.Map:
                    _mapWindow?.Init(MapModelSource?.Invoke());
                    break;
                case Screen.Deck:
                    _deckWindow?.Init(DeckModelSource?.Invoke());
                    break;
                case Screen.Shop:
                    _shopWindow?.Init(ShopModelSource?.Invoke());
                    break;
            }
        }

        /// <summary>
        /// Закрыть мету целиком и отпустить всё. Оба конца подписок гасятся здесь:
        /// сами окна зануляют СВОИ события в <c>Hide</c>, а <see cref="ClearAction"/>
        /// зануляет наши (docs/04, «Освобождение ресурсов»).
        /// </summary>
        public void Exit()
        {
            HideCurrent();
            ClearAction();
        }

        public void ClearAction()
        {
            OnNodeChosen = null;
            OnDeckSlotChosen = null;
            OnRecipeChosen = null;
            OnPurchaseConfirmed = null;
            OnExit = null;

            MapModelSource = null;
            DeckModelSource = null;
            ShopModelSource = null;
        }

        /// <summary>
        /// Окна гасятся через <c>Hide()</c>, а он уже зануляет их собственные события
        /// и возвращает пул. Отдельная отписка здесь была бы вторым списком, который
        /// разъедется с первым при первой же новой кнопке.
        /// </summary>
        private void HideCurrent()
        {
            switch (_current)
            {
                case Screen.Map:
                    _mapWindow?.Hide();
                    break;
                case Screen.Deck:
                    _deckWindow?.Hide();
                    break;
                case Screen.Shop:
                    _shopWindow?.Hide();
                    break;
            }

            _current = Screen.None;
        }

        private void RaiseNodeChosen(int number) => OnNodeChosen?.Invoke(number);

        private void RaiseSlotChosen(int index) => OnDeckSlotChosen?.Invoke(index);

        private void RaiseRecipeChosen(string id) => OnRecipeChosen?.Invoke(id);

        private void RaisePurchaseConfirmed(string id) => OnPurchaseConfirmed?.Invoke(id);

        private void RaiseExit() => OnExit?.Invoke();
    }
}
