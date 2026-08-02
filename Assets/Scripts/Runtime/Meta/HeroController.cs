using Core.Spells;
using Meta.UI;
using UnityEngine;
using Utility.Services.UI;


namespace Meta
{
    /// <summary>
    /// ОКНО ВЫБОРА ГЕРОЯ и ЕДИНСТВЕННОЕ МЕСТО, ГДЕ ВЫБОР МОЖНО ЗАПРЕТИТЬ.
    ///
    /// ═══ ЧТО ЗДЕСЬ ПОЯВИЛОСЬ И ПОЧЕМУ ═══
    ///
    /// Раньше окно отдавало любую карточку: тумблер переключился — герой выбран.
    /// Для Мага и Чаровницы это означало гарантированное исключение посреди боя
    /// (docs/06 §9: все восемь их заклинаний висят на классе-заготовке <c>Spell</c>,
    /// у которого <c>GetSpell()</c> бросает <c>NotImplementedException</c>).
    ///
    /// Теперь каждый тап по карточке проходит через <see cref="MetaController.AvailabilityOf"/>,
    /// и недоступный герой:
    ///  1. НЕ становится выбранным — ни по тумблеру, ни по кнопке «сохранить»;
    ///  2. ОБЪЯСНЯЕТ, почему, — локализованной строкой на полосе (<see cref="UINotice"/>),
    ///     а не молчанием: молчаливый отказ читается как поломка, а не как правило;
    ///  3. ОСТАЁТСЯ ВИДЕН на экране. Спрятать карточку было бы дешевле, но «герой
    ///     пропал» неотличимо от «сейв слетел», а обещание «Мгла откроется за босса»
    ///     работает, только если Мглу видно.
    ///
    /// ⚠️ ЧЕГО ЗДЕСЬ НЕ ХВАТАЕТ И ПОЧЕМУ. Правильный вид запертой карточки — замок
    /// и погашенный тумблер ПРЯМО НА НЕЙ, до всякого тапа. Это правка <c>UIHero</c>
    /// (одно поле и одна строка в <c>Init</c>), которая в этой работе была вне зоны:
    /// <c>Meta/UI</c> параллельно переделывает другой автор. Полоса — честный, но
    /// временный заменитель: она объясняет ПОСЛЕ тапа, а не ДО.
    ///
    /// ═══ ПРО ТУМБЛЕРЫ ═══
    ///
    /// Карточки сидят в <c>ToggleGroup</c> с <c>allowSwitchOff = 0</c>: выключить
    /// тумблер напрямую группа не даст, она сама гасит прежний, когда включают новый.
    /// Поэтому «вернуть выделение» делается ВКЛЮЧЕНИЕМ правильной карточки
    /// (<see cref="RestoreSelection"/>), а не выключением запертой.
    /// </summary>
    public class HeroController : IAction
    {
        /// <summary>
        /// Книга выбранного героя. Осталась ради вызывающих; истина — <c>MetaController</c>,
        /// у которого она же и лежит в <c>Saves.Profile.HeroId</c>.
        /// </summary>
        public Book SeveBook => _meta.CurrentBook;

        private IUIService  _uiService;
        private UIHeroesWindow _uiHeroesWindow;
        private Book _currentSelectedBook;

        private readonly MetaController _meta;
        private EquipmentController _equipmentController;

        /// <summary>
        /// Полоса «почему нельзя». Одна на контроллер, а не на показ окна: она держит
        /// созданный объект и обязана разбирать его в <see cref="ClearAction"/>.
        /// </summary>
        private readonly UINotice _notice = new UINotice();

        public HeroController(IUIService uiService, MetaController meta)
        {
            _uiService = uiService;
            _meta = meta;
            _equipmentController = new(uiService, meta);
        }

        /// <summary>Мета — для окна героев: какие книги открыты, какая выбрана (docs/10 §13.4).</summary>
        public MetaController Meta => _meta;

        /// <summary>
        /// Колода героя, чью карточку сейчас смотрят. Точка входа окна экипировки.
        /// </summary>
        public HeroLoadout LoadoutOf(Book book) => _meta.LoadoutOf(book);

        public void OpenWindow()
        {
            _uiHeroesWindow = _uiService.Show<UIHeroesWindow>();
            if (_uiHeroesWindow == null)
            {
                Debug.LogError("[Мета] В сцене нет UIHeroesWindow — выбирать героя негде. " +
                               "Проверка: Tools → Окна → Проверить реестр окон.");
                return;
            }

            _notice.Clear();
            _currentSelectedBook = ResolveInitialSelection();

            foreach (UIHero uiHero in _uiHeroesWindow.UIHeroList)
            {
                if (uiHero == null)
                    continue;

                Bind(uiHero, uiHero.HeroBook == _currentSelectedBook);

                if (uiHero.HeroBook != null && !_meta.IsHeroAvailable(uiHero.HeroBook))
                {
                    Debug.Log($"[Мета] Герой «{uiHero.HeroBook.HeroId}» недоступен: " +
                              $"{_meta.AvailabilityOf(uiHero.HeroBook)}. Карточка остаётся видимой, " +
                              "но выбрать его нельзя.");
                }
            }

            _uiHeroesWindow.OnClickHomeButton += HideWindow;

            _uiHeroesWindow.OnClickSaveButton += SaveSelectedHero;
        }

        /// <summary>
        /// ТОЧКА СОХРАНЕНИЯ №2 — игрок нажал «сохранить» в окне героев.
        ///
        /// Почему здесь, а не на переключении тумблера: тумблер — это ещё не выбор,
        /// игрок листает героев и сравнивает. Кнопка «сохранить» уже названа сохранением,
        /// обманывать её значение нельзя; а писать в IndexedDB на каждое переключение
        /// тумблера значит платить за то, чего игрок не просил.
        ///
        /// Повторное нажатие ничего не стоит: если герой не менялся, записи не будет.
        /// ВТОРАЯ ПРОВЕРКА ДОСТУПНОСТИ здесь не паранойя, а страховка от единственного
        /// случая, когда первая не сработала: карточки выбранного героя нет в окне
        /// (см. <see cref="RestoreSelection"/>). Окно тогда не закрывается — иначе игрок
        /// ушёл бы с экрана в уверенности, что выбор принят.
        /// </summary>
        private void SaveSelectedHero()
        {
            if (!_meta.TrySelectHero(_currentSelectedBook))
            {
                Refuse(_currentSelectedBook);
                return;
            }

            HideWindow();
        }

        /// <summary>
        /// Тумблер карточки переключился. Значение <c>false</c> нас не касается вовсе:
        /// группа гасит прежнюю карточку сама, и реагировать на это значило бы дважды
        /// обрабатывать один и тот же выбор.
        /// </summary>
        private void SelectedHero(bool value, UIHero uiHero)
        {
            if (!value || uiHero == null)
                return;

            if (_meta.IsHeroAvailable(uiHero.HeroBook))
            {
                _currentSelectedBook = uiHero.HeroBook;
                _notice.Clear();
                return;
            }

            Refuse(uiHero.HeroBook);
            RestoreSelection();
        }

        /// <summary>Сказать игроку, почему этот герой не берётся. Строка — уже локализованная.</summary>
        private void Refuse(Book book)
        {
            string reason = _meta.DescribeUnavailable(book);
            _notice.Show(_uiHeroesWindow, reason);
            Debug.Log($"[Мета] Отказ в выборе героя «{(book == null ? "?" : book.HeroId)}»: {reason}");
        }

        /// <summary>
        /// Вернуть выделение на того героя, который выбран на самом деле. Именно
        /// ВКЛЮЧЕНИЕМ правильной карточки: <c>ToggleGroup</c> стоит с
        /// <c>allowSwitchOff = 0</c> и выключить запертую напрямую не позволит.
        ///
        /// Перед включением карточка перепривязывается (<see cref="Bind"/>), потому что
        /// <c>UIHero.Init</c> ДОБАВЛЯЕТ слушателей, не снимая прежних, — иначе каждое
        /// возвращение удваивало бы обработчики.
        /// </summary>
        private void RestoreSelection()
        {
            if (_uiHeroesWindow == null)
                return;

            foreach (UIHero uiHero in _uiHeroesWindow.UIHeroList)
            {
                if (uiHero == null || uiHero.HeroBook != _currentSelectedBook)
                    continue;

                Bind(uiHero, true);
                return;
            }

            Debug.LogWarning($"[Мета] Карточки выбранного героя «{(_currentSelectedBook == null ? "?" : _currentSelectedBook.HeroId)}» " +
                             "нет в окне — вернуть выделение некуда. Выбор всё равно не изменится: " +
                             "его защищает проверка в «сохранить».");
        }

        /// <summary>
        /// Привязать карточку заново: сначала снять всё, что на ней висит, и только потом
        /// поставить своё. Порядок обязателен — <c>UIHero.Init</c> подписывается
        /// на <c>Toggle</c> и на кнопку без снятия прежних подписок.
        /// </summary>
        private void Bind(UIHero uiHero, bool isSelected)
        {
            uiHero.ClearAction();
            uiHero.Init(isSelected);
            uiHero.OnHeroSelected += value => SelectedHero(value, uiHero);
            uiHero.OnInfoButtonClick += OpenEquipment;
        }

        /// <summary>
        /// Кем показать окно выбранным. Обычно это текущий герой меты; список карточек
        /// живёт в сцене, поэтому «текущего героя в окне нет» — возможный случай
        /// (книга замера из <c>TactMeterSetupMenu</c>), и на него нужен ответ.
        /// </summary>
        private Book ResolveInitialSelection()
        {
            Book current = _meta.CurrentBook;

            foreach (UIHero uiHero in _uiHeroesWindow.UIHeroList)
                if (uiHero != null && uiHero.HeroBook == current)
                    return current;

            foreach (UIHero uiHero in _uiHeroesWindow.UIHeroList)
                if (uiHero != null && _meta.IsHeroAvailable(uiHero.HeroBook))
                    return uiHero.HeroBook;

            return current;
        }

        /// <summary>Книгу героя показывает окно экипировки — в том числе запертого: смотреть можно всем.</summary>
        private void OpenEquipment(Book book) => _equipmentController.OpenWindow(book);

        private void HideWindow()
        {
            _notice.Clear();
            _uiHeroesWindow?.Hide();

        }

        /// <summary>
        /// Разбор по правилу проекта. Окно свои события зануляет само в <c>Hide()</c>,
        /// а вот полоса — созданный кодом объект на чужом окне, и без этого вызова
        /// она пережила бы владельца (docs/04, «Освобождение ресурсов»).
        /// Зовётся из <c>HomeController.Exit()</c>, то есть при разборе главного экрана.
        /// </summary>
        public void ClearAction()
        {
            if (_uiHeroesWindow != null)
            {
                _uiHeroesWindow.OnClickHomeButton -= HideWindow;
                _uiHeroesWindow.OnClickSaveButton -= SaveSelectedHero;

                foreach (UIHero uiHero in _uiHeroesWindow.UIHeroList)
                    uiHero?.ClearAction();
            }

            _notice.ClearAction();
            _uiHeroesWindow = null;
        }

    }
}
