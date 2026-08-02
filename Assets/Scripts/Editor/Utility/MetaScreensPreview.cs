using Meta;
using UnityEditor;
using UnityEngine;
using Utility.Services.UI;

namespace CatZombii.EditorTools
{
    /// <summary>
    /// ПОКАЗАТЬ ТРИ ЭКРАНА МЕТЫ НА ЗАГЛУШЕЧНЫХ ДАННЫХ, не трогая загрузку игры.
    ///
    /// Зачем отдельная кнопка, а не вызов из <c>GameManager</c>. Каркас меты и слой
    /// данных делаются параллельно, и вклиниваться в <c>Boot</c> значит либо драться
    /// за один файл, либо оставить в загрузке мёртвый код, который потом никто не уберёт.
    /// Кнопка не входит в сборку (папка <c>Editor</c>), ничего не пишет в сцену и живёт
    /// ровно до тех пор, пока экраны не подключит настоящий профиль — вместе с
    /// <see cref="MetaScreensStub"/>.
    ///
    /// Работает ТОЛЬКО в Play Mode: <c>UIService</c> собирает реестр окон в <c>Awake</c>,
    /// и вне игры показывать нечего.
    /// </summary>
    public static class MetaScreensPreview
    {
        private const string MENU_ROOT = "Tools/Мета/";

        private static MetaScreensController _controller;

        [MenuItem(MENU_ROOT + "Показать карту (заглушка)")]
        public static void ShowMap() => Build()?.ShowMap();

        [MenuItem(MENU_ROOT + "Показать колоду (заглушка)")]
        public static void ShowDeck() => Build()?.ShowDeck();

        [MenuItem(MENU_ROOT + "Показать лавку (заглушка)")]
        public static void ShowShop() => Build()?.ShowShop();

        [MenuItem(MENU_ROOT + "Закрыть экраны меты")]
        public static void Close()
        {
            _controller?.Exit();
            _controller = null;
        }

        private static MetaScreensController Build()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[Мета] Экраны показываются только в Play Mode: реестр окон " +
                               "UIService собирается в Awake, вне игры его просто нет.");
                return null;
            }

            if (UIService.Instance == null)
            {
                Debug.LogError("[Мета] В сцене нет UIService. Открыта та сцена? " +
                               "В сборке участвует только Assets/Scenes/SampleScene.unity.");
                return null;
            }

            if (_controller != null)
                return _controller;

            IUIService uiService = UIService.Instance;
            _controller = new MetaScreensController(uiService)
            {
                MapModelSource = MetaScreensStub.BuildMap,
                DeckModelSource = MetaScreensStub.BuildDeck,
                ShopModelSource = MetaScreensStub.BuildShop,
            };

            _controller.OnNodeChosen += number =>
                Debug.Log($"[Мета] Выбран узел {number}. Настоящий обработчик стартует бой этого уровня.");
            _controller.OnDeckSlotChosen += index =>
                Debug.Log($"[Мета] Тап по слоту {index}. Настоящий обработчик снимает рецепт со слота.");
            _controller.OnRecipeChosen += id =>
                Debug.Log($"[Мета] Тап по рецепту «{id}». Настоящий обработчик надевает или снимает его.");
            _controller.OnPurchaseConfirmed += id =>
                Debug.Log($"[Мета] Покупка «{id}» подтверждена. Настоящий обработчик списывает клубки.");
            _controller.OnExit += Close;

            return _controller;
        }
    }
}
