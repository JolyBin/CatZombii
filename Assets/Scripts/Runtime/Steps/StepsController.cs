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
            _flaskController.Exit();
            _tableController.Exit();
            _battleController.Exit();

            // связанный токен боя уже освобождён внутри BattleController.Exit()
            _partyCts?.Dispose();
            _partyCts = null;

            _homeController.OpenWindow();
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
        }

        private void ShowWinWindow()
        {
            FinishParty();
            _winWindow = _uiService.Show<UIWinWindow>();
            _winWindow.OnClickContinueButton += () =>
            {
                _winWindow.Hide();
                // Узел засчитан. Возвращённый флаг — «пройден впервые»: из него считается
                // размер награды (docs/10 §15.3, перепрохождение платит 40%). Само
                // начисление появится вместе с экраном победы и цифрами геймдизайнера.
                _homeController.RegisterNodeCleared();
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
            // Resume, а не Start: волна сохраняется, значит и возраст мира сохраняется.
            _worldClock?.Resume();
            return true;
        }
    }
}
