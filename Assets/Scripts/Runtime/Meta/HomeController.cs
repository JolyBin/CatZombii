using Core.Battle;
using Core.Spells;
using Core.Steps;
using Meta.UI;
using UnityEngine;
using Utility.Services.UI;

namespace Meta
{
    /// <summary>
    /// ГЛАВНЫЙ ЭКРАН И ВСЁ, ЧТО ИЗ НЕГО РАСТЁТ: карта, колода, лавка, герои и запуск боя.
    ///
    /// ═══ ПОТОК ЭКРАНОВ ═══
    ///
    /// <code>
    ///                    ┌──────────────┐
    ///   старт игры ─────▶│ UIHomeWindow │──── «ГЕРОИ» ───▶ UIHeroesWindow (поверх)
    ///                    └──────┬───────┘
    ///                           │ «ИГРАТЬ»
    ///                           ▼
    ///                    ┌──────────────┐◀── «назад» ──┐
    ///                    │  UIMapWindow │              │
    ///                    └──────┬───────┘──────────────┘
    ///                           │ клик по узлу        (колода ⇄ лавка живут
    ///                           ▼                      внутри MetaScreensController)
    ///                    ┌──────────────┐
    ///                    │     БОЙ      │
    ///                    └──────┬───────┘
    ///                           │ победа/поражение/«домой»
    ///                           └──────▶ обратно НА КАРТУ
    /// </code>
    ///
    /// ПОЧЕМУ КАРТА ИЗ ДОМАШНЕГО ОКНА, А НЕ ВМЕСТО НЕГО. docs/10 §13.4 требует карту
    /// из узлов, но не говорит, где живёт вход в неё, — значит выбор наш, и у него
    /// три причины:
    ///
    /// 1. У героев нет другой двери. Выбор героя открывается кнопкой «ГЕРОИ»
    ///    домашнего окна, а героев по §13.4 трое, и они — главная ось идентичности.
    ///    Сделай карту корнем — и кнопку пришлось бы переносить на карту, то есть
    ///    делать сценовую работу, которую поправка П5 (docs/10 §8) прямо называет
    ///    главным риском плана.
    /// 2. Кнопке «назад» на карте нужен адрес. Она уже нарисована и уже кликается;
    ///    без домашнего окна её оставалось бы только убрать (тоже правка сцены)
    ///    или оставить мёртвой.
    /// 3. Ноль правок сцены. Оба окна уже стоят и уже активны в <c>Awake</c>, значит
    ///    оба уже в реестре <c>UIService</c>. Меняется только код.
    ///
    /// ПОСЛЕ БОЯ ВОЗВРАТ НА КАРТУ, А НЕ ДОМОЙ (<see cref="ReturnFromBattle"/>): бой
    /// начался с карты, узлы перепроходимы (§13.4), и следующее решение игрока —
    /// «какой узел теперь». Домашнее окно на этом пути было бы лишним тапом.
    /// </summary>
    public class HomeController
    {
        private IUIService _uiService;

        private UIHomeWindow _homeWindow;
        private StepsController _stepsController;
        private HeroController _heroController;

        /// <summary>
        /// Мета целиком: карта, кошелёк, покупки, колоды. Создаётся здесь, потому что
        /// здесь же начинается и кончается сессия главного экрана.
        /// </summary>
        private readonly MetaController _meta;

        /// <summary>
        /// Три экрана меты и их связь с данными. Живут ПАРОЙ и ровно столько, сколько
        /// показаны: <see cref="MetaScreensController.Exit"/> зануляет свои делегаты,
        /// поэтому переиспользовать закрытый экземпляр нельзя — на каждый вход на карту
        /// собирается новый. Это дешёвые объекты, зато освобождение получается
        /// не «когда-нибудь», а в той же строке, где закрытие.
        /// </summary>
        private MetaScreensController _screens;

        private MetaScreensBinding _binding;

        /// <summary>
        /// Узел, с которого игрок сейчас пойдёт в бой. Это НЕ прогресс — прогресс живёт
        /// в <c>Saves.Profile.ClearedNodes</c> и читается через <see cref="MetaController.Map"/>.
        ///
        /// Раньше на этом месте был <c>_configIndex</c>, и он был сразу и выбором,
        /// и прогрессом, и тупиком: рос на победе, упирался в последний уровень
        /// и никогда не сбрасывался (docs/10 §13.4). Теперь узлы перепроходимы,
        /// и «какой узел выбран» — это состояние ЭКРАНА, живущее до следующего клика.
        /// </summary>
        private int _selectedNode;

        public HomeController(IUIService uiService, Book startBook, BattleConfig[] battleConfigs)
        {
            _uiService = uiService;

            // Уровни отдаются мете и живут дальше только там: карта (MapProgress) — их
            // единственный владелец, и второй список здесь означал бы второе мнение
            // о том, сколько в игре узлов.
            _meta = new MetaController(startBook, battleConfigs);
            _heroController = new HeroController(_uiService, _meta);
            _selectedNode = _meta.Map.NextNode;
            OpenWindow();
        }

        /// <summary>Мета для экранов карты, колоды и лавки. Единственный вход к прогрессу.</summary>
        public MetaController Meta => _meta;

        /// <summary>Узел, выбранный на карте. Пока экрана карты нет — первый непройденный.</summary>
        public int SelectedNode => _selectedNode;

        /// <summary>
        /// ПОКАЗАТЬ ДОМАШНЕЕ ОКНО. Зовётся при старте игры и по кнопке «назад» с карты.
        ///
        /// Подписка тут безопасно повторяемая: <c>UIHomeWindow.Hide()</c> зануляет свои
        /// события, а показать окно, не спрятав предыдущее, этот класс не умеет.
        /// </summary>
        public void OpenWindow()
        {
            _homeWindow = _uiService.Show<UIHomeWindow>();
            if (_homeWindow == null)
            {
                Debug.LogError("[Мета] В сцене нет UIHomeWindow — показывать главный экран нечем. " +
                               "Проверка: Tools → Окна → Проверить реестр окон.");
                return;
            }

            _homeWindow.SetLevelValue(_selectedNode + 1);
            _homeWindow.OnClickPlayButton += OpenMap;
            _homeWindow.OnClickCharactersButton += OpenHeroesWindow;
        }

        /// <summary>
        /// ОТКРЫТЬ КАРТУ — вход во всю мету (docs/10 §13). Отсюда же игрок попадает
        /// в колоду и лавку: переходы между тремя экранами живут внутри
        /// <see cref="MetaScreensController"/> и наружу не выходят.
        ///
        /// Связка «данные ↔ экраны» собирается заново на каждый вход. Так она всегда
        /// читает АКТУАЛЬНОГО героя: между двумя заходами на карту игрок мог сменить
        /// его в окне героев, а колода принадлежит герою.
        /// </summary>
        public void OpenMap()
        {
            CloseMeta();
            _homeWindow?.Hide();

            _binding = CreateScreensBinding();
            _screens = new MetaScreensController(_uiService);

            _binding.AttachTo(_screens);
            _screens.OnExit += ExitMapToHome;
            _screens.ShowMap();
        }

        /// <summary>
        /// ВЫБОР УЗЛА НА КАРТЕ. Точка входа для экрана карты (docs/10 §13.4): узлы
        /// перепроходимы, поэтому выбрать можно любой открытый, а не только следующий.
        /// </summary>
        /// <returns><c>false</c> — узел закрыт или его нет; экран обязан оставить выбор как был.</returns>
        public bool TrySelectNode(int node)
        {
            if (!_meta.Map.IsUnlocked(node))
                return false;

            _selectedNode = node;
            _homeWindow?.SetLevelValue(_selectedNode + 1);
            return true;
        }

        /// <summary>
        /// Выбрать узел и сразу пойти в бой — то, что делает клик по узлу карты.
        /// </summary>
        /// <returns>
        /// <c>false</c> — бой НЕ начался (узел закрыт или у него нет собранного уровня).
        /// Врать здесь нельзя: по этому ответу экран решает, показывать ли отказ,
        /// и «true, но ничего не произошло» выглядит как зависшая кнопка.
        /// </returns>
        public bool TryStartNode(int node) => TrySelectNode(node) && StartGame();

        /// <summary>
        /// ПОБЕДА НА УЗЛЕ. Зовётся из <c>StepsController.ShowWinWindow</c> по кнопке
        /// «продолжить», то есть ровно один раз на выигранный бой.
        ///
        /// Что изменилось против прежнего <c>AddConfigIndex</c>: прогресс больше не
        /// «следующий уровень», а «узел пройден». Перепрохождение уже пройденного узла
        /// прогресс не двигает и записи на диск не стоит — но событие о победе всё равно
        /// случается, потому что награда за перепрохождение есть (40% по §15.3).
        ///
        /// ⚠️ ЭТО ЕДИНСТВЕННОЕ МЕСТО, ГДЕ НАЧИСЛЯЕТСЯ НАГРАДА ЗА БОЙ. Кнопка «×2 за
        /// рекламу» (docs/10 §10) не должна считать награду заново — ей достаточно
        /// добавить столько же ещё раз: <c>Meta.AddCoins(reward, "×2 за рекламу")</c>.
        /// Второй счётчик награды разъедется с первым при первой же правке §15.3.
        ///
        /// Порядок важен: узел засчитывается и оплачивается ЗДЕСЬ, а карта
        /// перерисовывается позже, в <see cref="ReturnFromBattle"/>, — то есть игрок
        /// возвращается уже на карту с отмеченным узлом и выросшим счётчиком клубков.
        /// </summary>
        /// <returns>Сколько клубков начислено за бой — это же число показывает экран победы.</returns>
        public int RegisterNodeCleared()
        {
            int node = _selectedNode;
            _meta.Map.TryRegisterClear(node, out bool firstClear);

            int reward = MetaEconomy.NodeReward(_meta.Map.ChapterOf(node), _meta.Map.IsBoss(node), firstClear);
            _meta.AddCoins(reward, $"награда за узел {node + 1}");

            // Следующий заход по умолчанию — первый непройденный узел. На карте игрок
            // всё равно выберет сам; это значение нужно домашнему окну и тому случаю,
            // когда бой запустили не с карты.
            _selectedNode = _meta.Map.NextNode;
            return reward;
        }

        /// <summary>
        /// ВЕРНУТЬСЯ ИЗ БОЯ. Зовётся из <c>StepsController.Exit()</c> любым исходом —
        /// победа, поражение, кнопка «домой».
        ///
        /// Ведёт НА КАРТУ, а не в домашнее окно: бой начался с карты, и там же лежит
        /// следующее решение игрока. Карта собирается заново, поэтому пройденный узел
        /// и новая сумма клубков видны сразу — отдельного «обнови карту» не существует
        /// и не может рассинхронизироваться.
        /// </summary>
        public void ReturnFromBattle()
        {
            _stepsController = null;
            OpenMap();
        }

        /// <summary>
        /// СВЯЗАТЬ ЭКРАНЫ МЕТЫ С ДАННЫМИ. Одна точка входа для карты, колоды и лавки:
        /// возвращённый <see cref="MetaScreensBinding"/> уже умеет собирать все три
        /// модели и применять клики по законам docs/10 §13.
        ///
        /// Освобождение — на вызывающем: <c>binding.ClearAction()</c> вместе с
        /// <c>MetaScreensController.Exit()</c>. Внутри этого класса за обоими следит
        /// <see cref="CloseMeta"/>.
        /// </summary>
        public MetaScreensBinding CreateScreensBinding() => new MetaScreensBinding(this);

        /// <summary>
        /// РАЗОБРАТЬ ГЛАВНЫЙ ЭКРАН. Сегодня это конец приложения (зовётся из
        /// <c>GameManager.OnDestroy</c>): сцена одна, и другого способа уйти отсюда нет.
        /// </summary>
        public void Exit()
        {
            CloseMeta();
            _meta.Exit();
        }

        // =====================================================================================
        // Внутреннее
        // =====================================================================================

        /// <summary>
        /// ЗАКРЫТЬ КАРТУ, КОЛОДУ И ЛАВКУ и отпустить обе половины шва. Идемпотентно —
        /// зовётся и перед показом карты, и перед боем, и на выходе из игры.
        ///
        /// Оба конца гасятся вместе и в одном месте намеренно: подписка биндинга
        /// на экраны и подписка экрана на окна — это одна связь, разорванная наполовину
        /// она означает живой обработчик у закрытого окна (docs/04, «Освобождение ресурсов»).
        /// </summary>
        private void CloseMeta()
        {
            if (_screens != null)
                _screens.OnExit -= ExitMapToHome;

            _binding?.ClearAction();
            _screens?.Exit();

            _binding = null;
            _screens = null;
        }

        /// <summary>Кнопка «назад» на карте — единственный выход из меты обратно на главный экран.</summary>
        private void ExitMapToHome()
        {
            CloseMeta();
            OpenWindow();
        }

        /// <summary>
        /// ЗАПУСК БОЯ. Конфиг проверяется ДО того, как что-то закрывается: узел без
        /// собранного уровня обязан оставить игрока на карте, а не выбросить его
        /// на пустой экран.
        /// </summary>
        /// <returns><c>false</c> — играть нечем, ни одно окно не тронуто.</returns>
        private bool StartGame()
        {
            BattleConfig config = _meta.Map.ConfigOf(_selectedNode);
            if (config == null)
            {
                Debug.LogError($"[Мета] Узла {_selectedNode + 1} нет в сборке — играть нечем.");
                return false;
            }

            // Гасим ровно то, откуда пришли. Раньше здесь было только домашнее окно,
            // и после появления карты бой шёл бы поверх неё.
            CloseMeta();
            _homeWindow?.Hide();

            // ЗАКОН §13.1: в бой едет КОЛОДА, а не книга. Отсюда «каждое новое заклинание
            // делает пазл труднее» — пул стихий колб выводится из экипированного набора.
            SpellDeck deck = _meta.CurrentLoadout.BuildDeck();
            _stepsController = new StepsController(_uiService, deck, this, config);
            _stepsController.Init();
            return true;
        }

        private void OpenHeroesWindow()
        {
            _heroController.OpenWindow();
        }
    }
}
