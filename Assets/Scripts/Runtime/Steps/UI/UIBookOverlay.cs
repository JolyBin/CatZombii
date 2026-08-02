using System;
using Core.Flask.Models;
using Core.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.Localization;
using Utility.UI;

namespace Core.Steps.UI
{
    /// <summary>
    /// КНИГА В БОЮ (docs/10 §17.1): кнопка на боевом экране открывает оверлей
    /// с ЭКИПИРОВАННЫМИ рецептами.
    ///
    /// ═══ ПОЧЕМУ ЭТО ВООБЩЕ НЕ ПРОТИВОРЕЧИТ ЯДРУ ═══
    ///
    /// Ядро — «знание книги» (§0.1), и справочник рецептов на первый взгляд его отменяет.
    /// Не отменяет: §17.1 разделяет «помнить СПИСОК наизусть» (зубрёжка, бьёт по
    /// вернувшемуся через три дня игроку, то есть прямо по D7) и «знать, КАКОЙ инструмент
    /// нужен этому полю и хватит ли ходов до него» (это и есть ядро). Первое не является
    /// условием второго. Ни одна тактическая игра не прячет от игрока его собственный
    /// арсенал — скрывают будущее, а не свои возможности.
    ///
    /// ═══ ЧТО ОВЕРЛЕЙ ПОКАЗЫВАЕТ И ЧЕГО НЕ ПОКАЗЫВАЕТ ═══
    ///
    ///  · Показывает СОСТАВ (цепочку стихий) и НАЗНАЧЕНИЕ (имя заклинания).
    ///  · Показывает ЭКИПИРОВАННОЕ, а не всю купленную коллекцию, — этим подкрепляется
    ///    §13.2 «колода есть выбор»: источник здесь <see cref="SpellDeck"/>, тот же самый,
    ///    из которого собраны котёл и пул стихий колб.
    ///  · ⛔ НЕ показывает подсказку «доложи X → получишь Y» и вообще ничего о ТЕКУЩЕМ
    ///    содержимом котла: печатать игроку дерево будущего запрещено (§0). Предпросмотр
    ///    текущего выигрыша живёт на кнопке варки и остаётся единственным контекстным
    ///    подсказчиком в игре.
    ///  · ⛔ НЕ показывает числа урона/лечения. Рецепты по §0.1 отличаются НАЗНАЧЕНИЕМ,
    ///    а не величиной; колонка чисел вернула бы ровно ту одномерную шкалу, ради отмены
    ///    которой переписывалось ядро.
    ///
    /// ОТКРЫТИЕ НЕ СТОИТ ТАКТА — и это не поблажка, а следствие пошаговости: мир и так
    /// стоит, пока игрок думает (<c>WorldClock</c> тикает только от перелива и варки).
    /// Поэтому здесь нет ни одного обращения к часам мира, и добавлять его нельзя.
    ///
    /// ═══ ПОЧЕМУ ЭТО НЕ <c>UIWindow</c>, А СЛОЙ, ПОСТРОЕННЫЙ КОДОМ ═══
    ///
    /// Поправка П5 (docs/10 §8) называет сценовую работу главным риском плана: каждое
    /// новое окно — это полдня расстановки и тихая поломка при ошибке, потому что
    /// <c>UIService</c> собирает реестр через <c>FindObjectsByType(FindObjectsInactive.Exclude)</c>,
    /// а <c>Show&lt;T&gt;()</c> при промахе МОЛЧА возвращает <c>null</c>. В проекте уже
    /// принят другой приём — слой, который строится кодом (<see cref="UINotice"/>), и здесь
    /// он же: ни одной правки сцены, ни одной ручной привязки, нечему разъехаться.
    ///
    /// Цена честная и её надо знать: вид оверлея задан числами в этом файле, а не
    /// инспектором. Когда придёт арт, панель заменяется на префаб — канал данных
    /// (<see cref="SpellDeck"/>) при этом не меняется.
    ///
    /// ═══ ГДЕ ЖИВЁТ И СКОЛЬКО ═══
    ///
    /// Ровно ОДНУ ПАРТИЮ. Создаётся в <c>StepsController.Init</c>, разбирается в его
    /// <c>Exit()</c> через <see cref="ClearAction"/>: это созданные кодом объекты
    /// на чужом окне, и без разбора они пережили бы хозяина (docs/04, «Освобождение
    /// ресурсов»). Оверлей второй партии ничего не знает о первой.
    ///
    /// КНОПКА висит на контейнере с безопасной зоной (там же, где котёл и полка),
    /// а ЗАТЕМНЕНИЕ — на самом окне, снаружи безопасной зоны: ужатый полноэкранный фон
    /// оставил бы непрокрашенную полосу ровно там, где вырез (см. <see cref="SafeAreaFitter"/>).
    /// </summary>
    public sealed class UIBookOverlay : IAction
    {
        /// <summary>
        /// ИГРОК ОТКРЫЛ ИЛИ ЗАКРЫЛ КНИГУ. Нужно партии, чтобы на время просмотра
        /// заглушить пазл и котёл.
        ///
        /// Затемнение оверлея и так ловит тапы, но полагаться ТОЛЬКО на него нельзя:
        /// котёл и полка — отдельные вложенные канвасы со своими
        /// <c>GraphicRaycaster</c>, и порядок их перекрытия — сценовое свойство,
        /// которое ломается перестановкой объектов (тот же довод, по которому
        /// <c>StepsController.FinishParty</c> отписывает кнопку «домой», а не надеется
        /// на канвас окна итога). Цена ошибки здесь — потерянный такт, то есть ход,
        /// которого игрок не делал.
        ///
        /// ⚠️ Событие означает РЕШЕНИЕ ИГРОКА. <see cref="LockInput"/> закрывает панель
        /// молча: конец партии — это не «игрок закрыл книгу», и разбудить им пазл,
        /// который только что заглушили окном итога, было бы прямой поломкой.
        /// </summary>
        public event Action<bool> OnOpenChanged;

        // ═══ Геометрия кнопки (канвас-единицы, референс 1080×1920, docs/12 §3.3) ═══

        /// <summary>Сторона кнопки. Закон тапа — 150 единиц минимум (docs/12 §3.2).</summary>
        private const float BUTTON_SIDE = UILayout.MIN_TAP_SIDE;

        /// <summary>
        /// Отступ правого края от края экрана. Кнопка встаёт В КОЛОНКУ «ВАРИТЬ» —
        /// у той правый край на 10 единиц левее поля макета, и разнобой здесь читался бы
        /// как случайность.
        /// </summary>
        private const float BUTTON_RIGHT = UILayout.SAFE_SIDE + 10f;

        /// <summary>
        /// Нижний край кнопки. Верх кнопки варки — 760 единиц от низа; книга садится
        /// прямо над ней и упирается верхом в границу кухни (920). Свободного места
        /// на боевом экране больше нет нигде: HUD, поле, котёл и полка заняты целиком.
        /// </summary>
        private const float BUTTON_BOTTOM = 770f;

        // ═══ Геометрия оверлея ═══

        private const float BOARD_WIDTH = UILayout.REFERENCE_WIDTH_PORTRAIT - 2f * UILayout.SAFE_SIDE;
        private const float BOARD_PADDING = 30f;
        private const float TITLE_HEIGHT = 90f;
        private const float HINT_HEIGHT = 70f;
        private const float ROW_HEIGHT = 120f;
        private const float ROW_GAP = 10f;
        private const float CLOSE_WIDTH = 420f;
        private const float CLOSE_HEIGHT = UILayout.MIN_TAP_SIDE;

        /// <summary>Кружков цепочки на строку. Самый длинный рецепт в ассетах — четыре звена.</summary>
        private const int MAX_CHAIN_ICONS = 4;

        private const float CHAIN_ICON_SIDE = 76f;
        private const float CHAIN_ICON_GAP = 12f;
        private const float ROW_PADDING = 24f;

        private static readonly Color BoardColor = new Color(0.09f, 0.08f, 0.12f, 0.97f);
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color TextColor = new Color(1f, 0.92f, 0.78f);
        private static readonly Color HintColor = new Color(1f, 0.92f, 0.78f, 0.55f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.13f, 0.20f, 0.95f);

        private UIBattleWindow _window;
        private SpellDeck _deck;

        private GameObject _buttonObject;
        private Button _button;
        private GameObject _panelObject;

        /// <summary>Партия кончилась — кнопка обязана замолчать вместе с колбами и котлом.</summary>
        private bool _isLocked;

        /// <summary>
        /// ПОСТАВИТЬ КНОПКУ НА БОЕВОЕ ОКНО. Сама панель строится лениво, при первом
        /// открытии: игрок, ни разу не заглянувший в книгу, не платит за неё ничем.
        /// </summary>
        public void Attach(UIBattleWindow window, SpellDeck deck)
        {
            if (window == null)
            {
                Debug.LogWarning("[Бой] Книгу некуда вешать: боевого окна нет. " +
                                 "Проверка: Tools → Окна → Проверить реестр окон.");
                return;
            }

            ClearAction();

            _window = window;
            _deck = deck;
            _isLocked = false;

            BuildButton();
        }

        /// <summary>
        /// Партия кончилась: оверлей закрывается и кнопка гаснет. Парно к
        /// <c>FlaskController.LockInput</c> и <c>TableController.LockInput</c> —
        /// живой элемент управления под окном итога читается как «игра не кончилась».
        /// </summary>
        public void LockInput()
        {
            _isLocked = true;

            // Молча: см. комментарий к OnOpenChanged. Пазл в этот момент уже заглушён
            // концом партии, и «книгу закрыли» его разбудить не должно.
            DestroyPanel();

            if (_button != null)
                _button.interactable = false;
            if (_buttonObject != null)
                _buttonObject.SetActive(false);
        }

        /// <summary>Игрок воскрес (<c>StepsController.ContinueAfterDefeat</c>) — книга снова доступна.</summary>
        public void UnlockInput()
        {
            _isLocked = false;

            if (_buttonObject != null)
                _buttonObject.SetActive(true);
            if (_button != null)
                _button.interactable = true;
        }

        /// <summary>
        /// Разбор по правилу проекта. Зовётся из <c>StepsController.Exit()</c>: оверлей
        /// живёт партию, а не сессию, и его объекты созданы кодом на чужом окне.
        /// </summary>
        public void ClearAction()
        {
            if (_button != null)
                _button.onClick.RemoveAllListeners();

            DestroyPanel();

            if (_buttonObject != null)
                UnityEngine.Object.Destroy(_buttonObject);

            OnOpenChanged = null;
            _buttonObject = null;
            _button = null;
            _window = null;
            _deck = null;
        }

        // =====================================================================================
        // Открыть / закрыть
        // =====================================================================================

        private void Toggle()
        {
            if (_isLocked)
                return;

            if (_panelObject != null)
                Close();
            else
                Open();
        }

        /// <summary>
        /// Панель собирается ЗАНОВО на каждое открытие и уничтожается на закрытие.
        /// Дороже пула ровно на одно действие игрока в минуту, зато между открытиями
        /// не остаётся состояния, которое можно забыть обновить, — а именно так
        /// справочники и начинают врать (устаревшее имя, чужой язык, рецепт из прошлой
        /// партии). Строк тут не больше восьми: колода ограничена слотами (§13.3).
        /// </summary>
        private void Open()
        {
            if (_window == null)
                return;

            DestroyPanel();
            BuildPanel();

            if (_panelObject == null)
                return;

            _panelObject.transform.SetAsLastSibling();
            OnOpenChanged?.Invoke(true);
        }

        /// <summary>Закрыть ПО РЕШЕНИЮ ИГРОКА — кнопкой «закрыть», тапом мимо или той же кнопкой книги.</summary>
        private void Close()
        {
            if (_panelObject == null)
                return;

            DestroyPanel();
            OnOpenChanged?.Invoke(false);
        }

        private void DestroyPanel()
        {
            if (_panelObject != null)
                UnityEngine.Object.Destroy(_panelObject);
            _panelObject = null;
        }

        // =====================================================================================
        // Сборка
        // =====================================================================================

        private void BuildButton()
        {
            TMP_FontAsset font = UIFonts.BorrowFrom(_window);
            RectTransform parent = ResolveSafeParent();

            _buttonObject = new GameObject("BookButton", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = _buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(BUTTON_SIDE, BUTTON_SIDE);
            rect.anchoredPosition = new Vector2(-BUTTON_RIGHT, BUTTON_BOTTOM);
            rect.SetAsLastSibling();

            Image background = _buttonObject.GetComponent<Image>();
            background.color = ButtonColor;

            _button = _buttonObject.GetComponent<Button>();
            _button.targetGraphic = background;
            _button.onClick.AddListener(Toggle);

            CreateLabel(rect, "Label", Localization.Get(LocKeys.BattleBookOpen), 38f, TextColor,
                        TextAlignmentOptions.Center, new Vector2(8f, 8f), new Vector2(-8f, -8f), font);
        }

        private void BuildPanel()
        {
            TMP_FontAsset font = UIFonts.BorrowFrom(_window);

            Combination[] recipes = _deck == null ? System.Array.Empty<Combination>() : _deck.Combinations;
            int rowCount = Mathf.Max(recipes.Length, 1);

            // ЗАТЕМНЕНИЕ — на самом окне, а не внутри безопасной зоны: ужатый
            // полноэкранный фон оставил бы непрокрашенную полосу там, где вырез.
            _panelObject = new GameObject("BookOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform panelRect = _panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(_window.transform, false);
            Stretch(panelRect);

            Image backdrop = _panelObject.GetComponent<Image>();
            backdrop.color = BackdropColor;

            // Тап мимо панели закрывает. Затемнение ОБЯЗАНО ловить клики: под ним лежат
            // колбы и кнопка варки, а случайный ход из-под открытого справочника — это
            // потерянный такт, за который игрок винит игру, а не себя.
            Button backdropButton = _panelObject.GetComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Close);

            float boardHeight = BOARD_PADDING + TITLE_HEIGHT + HINT_HEIGHT
                              + rowCount * ROW_HEIGHT + (rowCount - 1) * ROW_GAP
                              + BOARD_PADDING + CLOSE_HEIGHT + BOARD_PADDING;

            GameObject boardObject = new GameObject("Board", typeof(RectTransform), typeof(Image));
            RectTransform board = boardObject.GetComponent<RectTransform>();
            board.SetParent(panelRect, false);
            board.anchorMin = new Vector2(0.5f, 0.5f);
            board.anchorMax = new Vector2(0.5f, 0.5f);
            board.pivot = new Vector2(0.5f, 0.5f);
            board.sizeDelta = new Vector2(BOARD_WIDTH, boardHeight);
            board.anchoredPosition = Vector2.zero;
            boardObject.GetComponent<Image>().color = BoardColor;

            float cursor = -BOARD_PADDING;

            CreateLabel(board, "Title", Localization.Get(LocKeys.BattleBookTitle), 54f, TextColor,
                        TextAlignmentOptions.Center,
                        new Vector2(BOARD_PADDING, 0f), new Vector2(-BOARD_PADDING, 0f), font,
                        top: cursor, height: TITLE_HEIGHT);
            cursor -= TITLE_HEIGHT;

            CreateLabel(board, "Hint", Localization.Get(LocKeys.BattleBookHint), 32f, HintColor,
                        TextAlignmentOptions.Center,
                        new Vector2(BOARD_PADDING, 0f), new Vector2(-BOARD_PADDING, 0f), font,
                        top: cursor, height: HINT_HEIGHT);
            cursor -= HINT_HEIGHT;

            if (recipes.Length == 0)
            {
                // Пустая колода в бой не выпускается (HeroLoadout.BuildDeck падает
                // на всю книгу), но справочник обязан пережить и битый сейв.
                CreateLabel(board, "Empty", Localization.Get(LocKeys.BattleBookEmpty), 36f, HintColor,
                            TextAlignmentOptions.Center,
                            new Vector2(BOARD_PADDING, 0f), new Vector2(-BOARD_PADDING, 0f), font,
                            top: cursor, height: ROW_HEIGHT);
                cursor -= ROW_HEIGHT;
            }
            else
            {
                for (int i = 0; i < recipes.Length; i++)
                {
                    BuildRow(board, recipes[i], cursor, font);
                    cursor -= ROW_HEIGHT + (i == recipes.Length - 1 ? 0f : ROW_GAP);
                }
            }

            cursor -= BOARD_PADDING;
            BuildCloseButton(board, cursor, font);
        }

        private void BuildRow(RectTransform board, Combination combination, float top, TMP_FontAsset font)
        {
            GameObject rowObject = new GameObject("Recipe", typeof(RectTransform), typeof(Image));
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.SetParent(board, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(BOARD_PADDING, 0f);
            row.offsetMax = new Vector2(-BOARD_PADDING, 0f);
            row.sizeDelta = new Vector2(row.sizeDelta.x, ROW_HEIGHT);
            row.anchoredPosition = new Vector2(0f, top);

            Image rowBackground = rowObject.GetComponent<Image>();
            rowBackground.color = RowColor;
            rowBackground.raycastTarget = false;

            Element[] chain = combination == null || combination.Elements == null
                ? System.Array.Empty<Element>()
                : combination.Elements;

            int shown = Mathf.Min(chain.Length, MAX_CHAIN_ICONS);
            for (int i = 0; i < shown; i++)
                BuildChainIcon(row, chain[i], i);

            float chainWidth = ROW_PADDING + MAX_CHAIN_ICONS * (CHAIN_ICON_SIDE + CHAIN_ICON_GAP);

            // Имя заклинания — УЖЕ локализованное значение (BaseSpellConfig.Name читает
            // свой ключ сам). Пустое имя — законный промежуточный узел дерева: рецепта
            // без заклинания в колоде быть не должно, но справочник об этом не спорит.
            string name = combination != null && combination.Spell != null ? combination.Spell.Name : string.Empty;
            CreateLabel(row, "Name", name, 40f, TextColor, TextAlignmentOptions.MidlineLeft,
                        new Vector2(chainWidth, 8f), new Vector2(-ROW_PADDING, -8f), font);
        }

        /// <summary>
        /// Звено цепочки: СИЛУЭТ и цвет, а не один цвет (docs/12 §4.4 — цветовой код
        /// в одиночку запрещён). Спрайта у стихии может не быть — тогда остаётся
        /// закрашенный квадрат, и это честнее, чем ничего.
        /// </summary>
        private void BuildChainIcon(RectTransform row, Element element, int index)
        {
            GameObject iconObject = new GameObject("Element " + index, typeof(RectTransform), typeof(Image));
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.SetParent(row, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(CHAIN_ICON_SIDE, CHAIN_ICON_SIDE);
            rect.anchoredPosition = new Vector2(ROW_PADDING + index * (CHAIN_ICON_SIDE + CHAIN_ICON_GAP), 0f);

            Image image = iconObject.GetComponent<Image>();
            image.raycastTarget = false;

            if (element == null)
            {
                image.color = HintColor;
                return;
            }

            if (element.Texture != null)
                image.sprite = element.Texture;
            image.color = element.Color;
        }

        private void BuildCloseButton(RectTransform board, float top, TMP_FontAsset font)
        {
            GameObject closeObject = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = closeObject.GetComponent<RectTransform>();
            rect.SetParent(board, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(CLOSE_WIDTH, CLOSE_HEIGHT);
            rect.anchoredPosition = new Vector2(0f, top);

            Image background = closeObject.GetComponent<Image>();
            background.color = ButtonColor;

            Button button = closeObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(Close);

            CreateLabel(rect, "Label", Localization.Get(LocKeys.BattleBookClose), 40f, TextColor,
                        TextAlignmentOptions.Center, new Vector2(8f, 8f), new Vector2(-8f, -8f), font);
        }

        // =====================================================================================
        // Мелочи
        // =====================================================================================

        /// <summary>
        /// Куда вешать кнопку: контейнер, ужатый по безопасной зоне устройства
        /// (<see cref="SafeAreaFitter"/>), — тот же, в котором лежат котёл и полка.
        /// Ищется компонентом, а не по имени: имя объекта переживёт не всякий рефакторинг
        /// сцены, а компонент — это и есть определение «безопасной зоны».
        /// </summary>
        private RectTransform ResolveSafeParent()
        {
            SafeAreaFitter fitter = _window.GetComponentInChildren<SafeAreaFitter>(true);
            if (fitter != null && fitter.transform is RectTransform safeRect)
                return safeRect;

            return (RectTransform)_window.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Текст, растянутый по родителю. Шрифт — заимствованный: дефолтный TMP собран
        /// без кириллицы (<see cref="UIFonts"/>).
        /// </summary>
        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text,
                                                   float fontSize, Color color, TextAlignmentOptions alignment,
                                                   Vector2 offsetMin, Vector2 offsetMax, TMP_FontAsset font,
                                                   float top = float.NaN, float height = 0f)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            if (float.IsNaN(top))
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(offsetMin.x, 0f);
                rect.offsetMax = new Vector2(offsetMax.x, 0f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                rect.anchoredPosition = new Vector2(0f, top);
            }

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;

            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }
    }
}
