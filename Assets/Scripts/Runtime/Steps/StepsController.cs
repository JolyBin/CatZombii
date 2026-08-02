using Core.Battle;
using Core.Flask;
using Core.Spells;
using Core.Steps.UI;
using Meta;
using System;
using System.Threading;
using Utility.Services.UI;

namespace Core.Steps
{
    public class StepsController
    {
        /// <summary>
        /// Сколько HP возвращает продолжение после поражения. 50% — не подобранное число,
        /// а условие из docs/10 §10: «кот встаёт с 50% HP». Полный запас превратил бы
        /// просмотр рекламы в полноценный второй бой; символические 10% — в кнопку,
        /// которую жмут один раз и больше не жмут.
        /// </summary>
        private const int HERO_REVIVE_PERCENT = 50;

        private readonly FlaskController _flaskController;
        private readonly IUIService _uiService;
        private readonly TableController _tableController;

        private UIBattleWindow _window;
        private UIWinWindow _winWindow;
        private UILoseWindow _loseWindow;
        private HomeController _homeController;
        private BattleController _battleController;
        private Book _currentBook;

        /// <summary>
        /// Часы мира: единственный источник времени партии (docs/10 §0.2).
        /// Собираются здесь, потому что здесь же лежат все три системы, которые
        /// такт затрагивает, — колбы (источник действий), котёл и бой.
        /// </summary>
        private WorldClock _worldClock;

        /// <summary>
        /// Один и тот же обработчик на оба действия игрока — перелив и варку.
        /// Хранится полем ради парной отписки в <see cref="Exit"/>.
        /// </summary>
        private Action _tickAction;

        /// <summary>
        /// Токен жизни партии: отменяется в Exit(), гасит все отложенные эффекты боя.
        /// </summary>
        private CancellationTokenSource _partyCts;

        /// <summary>
        /// КНИГА В БОЮ (docs/10 §17.1) — кнопка и оверлей с ЭКИПИРОВАННЫМИ рецептами.
        ///
        /// Живёт здесь, а не в боевом окне, по двум причинам. Первая: оверлей —
        /// созданные кодом объекты на чужом окне, и разбирать их обязан тот, кто их
        /// завёл, то есть партия (<see cref="Exit"/>), а не сцена. Вторая: показывать
        /// он должен КОЛОДУ, а колода приезжает сюда конструктором и дальше боевого окна
        /// не идёт — окну от книги нужны только HP, имя и иконка героя.
        ///
        /// Открытие НЕ ТИКАЕТ часами мира и не может: тактов здесь никто не раздаёт.
        /// </summary>
        private readonly UIBookOverlay _bookOverlay = new UIBookOverlay();

        /// <summary>Колода партии — источник и для котла, и для колб, и для книги в бою.</summary>
        private readonly SpellDeck _deck;


        /// <summary>
        /// ПАРТИЯ СОБИРАЕТСЯ ИЗ КОЛОДЫ, А НЕ ИЗ КНИГИ — закон docs/10 §13.1.
        ///
        /// Что именно поменялось против прежней версии: источником рецептов и стихий
        /// стал <see cref="SpellDeck"/> (экипированный набор), а книга осталась только
        /// там, где речь о САМОМ ГЕРОЕ, а не о его заклинаниях, — HP, иконка, имя, класс.
        /// Больше ничего менять не пришлось: <c>Table</c> и так принимал
        /// <c>Combination[]</c>, а <c>FlaskController</c> — <c>Element[]</c>.
        ///
        /// Следствие, ради которого всё затевалось: взял в колоду рецепт с новой стихией —
        /// она посыпалась в колбы, и собрать четыре одинаковых стало труднее. Каждое новое
        /// заклинание делает пазл труднее, поэтому прогрессия балансирует себя сама.
        /// </summary>
        public StepsController(IUIService uIService, SpellDeck deck, HomeController homeController, BattleConfig currentlevel)
        {
            _deck = deck;
            _currentBook = deck.Book;
            _flaskController = new FlaskController(uIService, deck.UniqElements);
            _tableController = new TableController(deck.Combinations, _flaskController, uIService);
            _uiService = uIService;
            _homeController = homeController;
            _battleController = new BattleController(uIService, currentlevel, deck.Book, _tableController);
        }

        public void Init()
        {
            _partyCts = new CancellationTokenSource();
            _window = _uiService.Show<UIBattleWindow>();
            _window.Init(_currentBook);
            _bookOverlay.Attach(_window, _deck);
            _bookOverlay.OnOpenChanged += HandleBookOpenChanged;
            _flaskController.Init();
            _window.OnClickHomeButton += Exit;
            _flaskController.SubscribeToMove();
            _battleController.OnAllEnemyDie += ShowWinWindow;
            _battleController.OnHeroDie += ShowLoseWindow;
            _battleController.Init(_partyCts.Token);
            _tableController.Init();

            // ЕДИНСТВЕННЫЙ ВХОД ТАКТА. Оба действия игрока идут в один и тот же метод,
            // а порядок обработки внутри такта живёт целиком в WorldClock.Tick —
            // ни котёл, ни бой не подписаны на MoveCommand напрямую и не могут
            // «переставить» друг друга порядком подписки.
            _worldClock = new WorldClock(_tableController, _battleController);
            _tickAction = _worldClock.Tick;
            _flaskController.MoveCommand += _tickAction;
            _tableController.OnBrewCommand += _tickAction;
            _worldClock.Start();
        }

        private void Exit()
        {
            if (_partyCts != null && !_partyCts.IsCancellationRequested)
                _partyCts.Cancel();

            if (_tickAction != null)
            {
                _flaskController.MoveCommand -= _tickAction;
                _tableController.OnBrewCommand -= _tickAction;
                _tickAction = null;
            }
            _worldClock?.Stop();
            _worldClock = null;

            _battleController.OnAllEnemyDie -= ShowWinWindow;
            _battleController.OnHeroDie -= ShowLoseWindow;

            _window.OnClickHomeButton -= Exit;
            _window.Hide();

            // Оверлей книги — созданные кодом объекты на боевом окне. Окно переживает
            // партию (оно сценовое), поэтому без этой строки кнопка книги осталась бы
            // висеть и в следующей партии, показывая колоду предыдущей.
            _bookOverlay.OnOpenChanged -= HandleBookOpenChanged;
            _bookOverlay.ClearAction();

            _flaskController.Exit();
            _tableController.Exit();
            _battleController.Exit();

            // связанный токен боя уже освобождён внутри BattleController.Exit()
            _partyCts?.Dispose();
            _partyCts = null;

            // НА КАРТУ, а не в домашнее окно: бой начался с узла карты, и следующее
            // решение игрока — «какой узел теперь» (docs/10 §13.4, узлы перепроходимы).
            // Карта пересобирается на входе, поэтому пройденный узел и начисленные
            // клубки видны сразу.
            _homeController.ReturnFromBattle();
        }

        /// <summary>
        /// КНИГА ОТКРЫТА — ПАЗЛ ЗАМОЛКАЕТ. Открытие книги по-прежнему НЕ СТОИТ ТАКТА
        /// (часы мира здесь никто не трогает), но и подарить такт оно не должно:
        /// затемнение оверлея живёт на канвасе боевого окна, а колбы и котёл — на своих
        /// вложенных канвасах, и перекрытие между ними это сценовое свойство. Тап сквозь
        /// открытый справочник означал бы ход, которого игрок не делал.
        ///
        /// Обратная сторона зовётся только на РЕШЕНИЕ ИГРОКА закрыть книгу: конец партии
        /// закрывает оверлей молча, иначе разблокировка отменила бы заглушку, которую
        /// только что поставила <see cref="FinishParty"/>.
        /// </summary>
        private void HandleBookOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                _flaskController.LockInput();
                _tableController.LockInput();
                return;
            }

            _flaskController.UnlockInput();
            _tableController.UnlockInput();
        }

        /// <summary>
        /// Партия кончилась любым исходом: мир останавливается и перестаёт принимать
        /// действия. Это и есть вторая половина бага №12 — пазл больше не живёт
        /// под окном итога. Пошаговость делает лечение полным: остановить приём
        /// действий = остановить мир, отдельного «замораживателя» не требуется.
        /// </summary>
        private void FinishParty()
        {
            _worldClock?.Stop();
            _flaskController.LockInput();
            _tableController.LockInput();

            // Книга замолкает вместе с ними: открытый справочник поверх окна итога
            // читается как «партия ещё идёт», а под окном итога он к тому же
            // перехватывал бы тапы по кнопке «продолжить».
            _bookOverlay.LockInput();

            // КНОПКА «ДОМОЙ» БОЕВОГО ОКНА ТОЖЕ ЗАМОЛКАЕТ. Она уводит через Exit(),
            // МИНУЯ HomeController.RegisterNodeCleared — то есть выигранный узел не
            // засчитался бы и награда не начислилась бы. Боевое окно остаётся под окном
            // итога, и полагаться на то, что чужой канвас его перекрывает, нельзя:
            // порядок канвасов — сценовое свойство, а потеря прогресса — не то, что
            // можно оставить на «вроде не нажимается».
            //
            // Выход из законченной партии остаётся ровно один — кнопка на окне итога.
            if (_window != null)
                _window.OnClickHomeButton -= Exit;
        }

        private void ShowWinWindow()
        {
            FinishParty();
            _winWindow = _uiService.Show<UIWinWindow>();

            // Узел засчитывается и оплачивается В МОМЕНТ ПОБЕДЫ, а не по кнопке
            // «продолжить». Две причины, и обе важнее прежнего порядка:
            //
            // 1. Окно обязано показать НАЧИСЛЕННОЕ число, а не пообещать его. Считать
            //    награду вторым вызовом ради показа — это второй счётчик, которого
            //    RegisterNodeCleared прямо запрещает.
            // 2. Игрок, закрывший вкладку на экране победы, бой всё равно выиграл.
            //    Раньше он терял и узел, и клубки.
            //
            // Карта по-прежнему собирается позже (Exit → ReturnFromBattle), то есть
            // уже из обновлённого профиля.
            _winWindow.SetReward(_homeController.RegisterNodeCleared());

            _winWindow.OnClickContinueButton += () =>
            {
                _winWindow.Hide();
                Exit();
            };
        }

        private void ShowLoseWindow()
        {
            FinishParty();
            _loseWindow = _uiService.Show<UILoseWindow>();
            _loseWindow.OnClickContinueButton += () =>
            {
                _loseWindow.Hide();
                Exit();
            };
        }

        /// <summary>
        /// ПРОДОЛЖИТЬ ПОСЛЕ ПОРАЖЕНИЯ — обратная сторона <see cref="FinishParty"/>.
        /// Единственная точка входа для rewarded-крючка (docs/09 пункт 8; docs/10 §10:
        /// «кот встаёт с 50% HP, волна сохраняется»).
        ///
        /// Что здесь и почему именно здесь: воскрешение затрагивает ВСЕ ТРИ системы партии,
        /// а не только бой. Бой поднимает героя и возвращает его в цели врагов
        /// (<c>BattleController.ReviveHero</c>), но пазл и котёл в этот момент стоят
        /// с заблокированным вводом, а часы мира остановлены — то есть игрок физически
        /// не смог бы сделать ход. Собрать это может только тот, кто владеет всеми тремя,
        /// то есть этот класс.
        ///
        /// Порядок обязателен: сначала оживает бой, и только потом снимается блокировка.
        /// Наоборот — значит на один кадр отдать игроку ход в бою, который ещё считается
        /// оконченным (<c>_isBattleOver</c>), то есть подарить бесплатный такт.
        ///
        /// Кнопки на окне поражения ещё нет — её ставят вместе с рекламой (докрутка
        /// в И3). Метод публичный и самодостаточный именно поэтому: когда кнопка появится,
        /// к ней подключается один вызов, а не переписывается конец партии.
        /// </summary>
        /// <returns><c>false</c> — воскрешать некого; окно поражения остаётся как было.</returns>
        public bool ContinueAfterDefeat()
        {
            if (_battleController == null || !_battleController.ReviveHero(HERO_REVIVE_PERCENT))
                return false;

            _loseWindow?.Hide();

            _flaskController.UnlockInput();
            _tableController.UnlockInput();
            _bookOverlay.UnlockInput();

            // Партия снова живая — значит и выход из неё снова живой. Парно к тому,
            // что сняла FinishParty; без этого воскресший игрок остался бы в бою
            // без кнопки «домой».
            if (_window != null)
            {
                _window.OnClickHomeButton -= Exit;
                _window.OnClickHomeButton += Exit;
            }

            // Resume, а не Start: волна сохраняется, значит и возраст мира сохраняется.
            _worldClock?.Resume();
            return true;
        }
    }
}
