using System;
using System.Collections.Generic;
using Core.Battle;
using Core.Spells;
using UnityEngine;
using Utility.Services.Saves;

namespace Meta
{
    /// <summary>
    /// МЕТА ЦЕЛИКОМ, ОДНИМ ОБЪЕКТОМ: карта, кошелёк, покупки, колоды, открытые герои.
    /// Всё, что экранам меты нужно знать и менять, они спрашивают отсюда — и только отсюда.
    ///
    /// ═══ ЗАЧЕМ ФАСАД, ЕСЛИ ЕСТЬ <c>Saves.Profile</c> ═══
    ///
    /// Профиль — это данные, а не законы. Между «в сейве лежит строка <c>3&gt;3&gt;4</c>»
    /// и «эту карточку можно нажать» стоят правила docs/10 §13: слоты, префиксы,
    /// бесплатные рецепты длины 1, открытые герои, перепроходимость узлов. Если каждое
    /// окно будет читать профиль напрямую, правила разъедутся по экранам, и первое же
    /// расхождение выглядит как «в лавке купил, а в колоде не появилось».
    ///
    /// ═══ ЧЕГО ЗДЕСЬ НЕТ И ПОЧЕМУ ═══
    ///
    /// ЦЕНЫ. <see cref="TryBuyRecipe"/> и <see cref="TryBuySlot"/> принимают цену
    /// параметром. Цена — баланс, то есть работа геймдизайнера: docs/10 §14.4 даёт
    /// диапазоны (80–120 за рецепт длины 2, 200–300 за длину 3) и точную лестницу слотов
    /// (150/250/400/600), но в ассетах этого нет, а выдумывать баланс в коде — ровно то,
    /// что этот проект запрещает. Когда лестница станет ассетом, она подставится
    /// в вызов, а законы здесь не изменятся.
    ///
    /// НАГРАДЫ ЗА УЗЛЫ. По той же причине: §15.3 расписывает 60/100/150 за узел
    /// и 40% за перепрохождение, но это цифры, а не правила. Здесь есть только факт
    /// «узел пройден впервые» (<see cref="MapProgress.TryRegisterClear"/>) и способ
    /// начислить (<see cref="AddCoins"/>).
    ///
    /// ═══ ПРО ОСВОБОЖДЕНИЕ ═══
    ///
    /// Мета живёт всю сессию, поэтому подписок наружу у неё нет — есть только свои
    /// события, которые зануляются в <see cref="Exit"/> вместе с событиями карты.
    /// </summary>
    public sealed class MetaController
    {
        /// <summary>Кошелёк изменился. Аргумент — сколько стало.</summary>
        public event Action<int> OnCoinsChanged;

        /// <summary>Слотов колоды стало больше. Аргумент — сколько стало.</summary>
        public event Action<int> OnDeckSlotsChanged;

        /// <summary>Открылся герой. Аргумент — его книга.</summary>
        public event Action<Book> OnHeroUnlocked;

        /// <summary>Сменился выбранный герой.</summary>
        public event Action<Book> OnHeroSelected;

        /// <summary>Что-то изменилось в колоде или покупках текущего героя.</summary>
        public event Action OnLoadoutChanged;

        private readonly Book _fallbackBook;
        private readonly Dictionary<Book, HeroLoadout> _loadouts = new Dictionary<Book, HeroLoadout>();

        private Book _currentBook;

        public MapProgress Map { get; }

        public MetaController(Book startBook, BattleConfig[] battleConfigs)
        {
            _fallbackBook = startBook;
            Map = new MapProgress(battleConfigs);
            Map.OnChapterCleared += UnlockHeroesOfChapter;

            _currentBook = ResolveSavedBook(startBook);
            EnsureCurrentHeroUnlocked();
        }

        // =====================================================================================
        // Герой
        // =====================================================================================

        /// <summary>Кем играем. Никогда не <c>null</c>, пока в проекте есть хоть одна книга.</summary>
        public Book CurrentBook => _currentBook;

        /// <summary>Колода текущего героя — то, из чего собирается партия (docs/10 §13.1).</summary>
        public HeroLoadout CurrentLoadout => LoadoutOf(_currentBook);

        /// <summary>
        /// Колода конкретного героя. Кэшируется на сессию: <see cref="HeroLoadout"/>
        /// держит рабочую копию списка экипированного, и второй экземпляр на ту же книгу
        /// означал бы два расходящихся мнения об одной колоде.
        /// </summary>
        public HeroLoadout LoadoutOf(Book book)
        {
            if (book == null)
                return null;

            if (!_loadouts.TryGetValue(book, out HeroLoadout loadout))
            {
                loadout = new HeroLoadout(book);
                loadout.OnChanged += RaiseLoadoutChanged;
                _loadouts.Add(book, loadout);
            }
            return loadout;
        }

        /// <summary>
        /// Открыт ли герой. docs/10 §13.4: героев трое, стартовый и два за боссов узлов
        /// 5 и 10; <b>валютой герои не покупаются</b>. Книги со <c>UnlockChapter == 0</c>
        /// открыты всегда — сегодня это все четыре, пока геймдизайнер не расставил главы.
        /// </summary>
        public bool IsHeroUnlocked(Book book)
        {
            if (book == null)
                return false;
            if (book.UnlockChapter <= 0)
                return true;

            return Saves.Profile.IsHeroUnlocked(book.HeroId);
        }

        /// <summary>Все книги проекта — список для окна героев.</summary>
        public Book[] AllHeroes => BookCatalog.All;

        /// <summary>
        /// ВЫБОР ГЕРОЯ. Закрытого героя выбрать нельзя — это и есть разделение из §13.4:
        /// карта даёт идентичность, валюта даёт глубину.
        /// </summary>
        public bool TrySelectHero(Book book)
        {
            if (book == null || !IsHeroUnlocked(book))
                return false;
            if (_currentBook == book)
                return true;

            _currentBook = book;
            Saves.Profile.HeroId = book.HeroId;
            Saves.RequestSave($"выбран герой «{book.HeroId}»");
            OnHeroSelected?.Invoke(book);
            return true;
        }

        /// <summary>
        /// Открыть героя вручную. Нужна отладке и тому, кто однажды свяжет анлок
        /// с чем-то, кроме главы (например с наградой за событие).
        /// </summary>
        public bool TryUnlockHero(Book book)
        {
            if (book == null || IsHeroUnlocked(book))
                return false;

            if (!Saves.Profile.UnlockHero(book.HeroId))
                return false;

            Saves.RequestSave($"открыт герой «{book.HeroId}»");
            OnHeroUnlocked?.Invoke(book);
            return true;
        }

        // =====================================================================================
        // Кошелёк и покупки (§13.3)
        // =====================================================================================

        /// <summary>Клубки (docs/10 §15).</summary>
        public int Coins => Saves.Profile.Coins;

        public bool CanAfford(int price) => price >= 0 && Coins >= price;

        /// <summary>
        /// НАЧИСЛЕНИЕ. Единственный вход для награды за узел, ×2 за рекламу и суточного
        /// подарка. Кошелёк при поражении не уменьшается никогда (§10), поэтому
        /// отрицательных начислений здесь нет вовсе — списывает только покупка.
        /// </summary>
        public void AddCoins(int amount, string reason)
        {
            if (amount <= 0)
                return;

            Saves.Profile.Coins += amount;
            Saves.RequestSave(reason);
            OnCoinsChanged?.Invoke(Saves.Profile.Coins);
        }

        /// <summary>
        /// ПОКУПКА РЕЦЕПТА. Платится только ВЛАДЕНИЕ — экипировка бесплатна и обратима
        /// в любой момент (docs/10 §15.5, обязательное смягчение: иначе игрок, потративший
        /// клубки не туда, застревает без выхода).
        ///
        /// Цена приходит снаружи — см. «чего здесь нет» в комментарии к классу.
        /// </summary>
        public bool TryBuyRecipe(Book book, string recipeId, int price)
        {
            HeroLoadout loadout = LoadoutOf(book);
            if (loadout == null || !loadout.Recipes.Exists(recipeId) || loadout.IsOwned(recipeId))
                return false;
            if (!CanAfford(price))
                return false;

            if (!loadout.AddOwned(recipeId))
                return false;

            Saves.Profile.Coins -= price;
            Saves.RequestSave($"куплен рецепт «{recipeId}» героя «{book.HeroId}»");
            OnCoinsChanged?.Invoke(Saves.Profile.Coins);
            return true;
        }

        /// <summary>Слотов колоды сейчас. Общие для всех героев (docs/10 §15.1).</summary>
        public int DeckSlots => Saves.Profile.DeckSlots;

        public bool CanBuySlot => Saves.Profile.DeckSlots < PlayerProfile.MAX_DECK_SLOTS;

        /// <summary>
        /// Какой по счёту слот покупается следующим, считая с единицы: 1 — пятый слот,
        /// 4 — восьмой. Ровно этим числом индексируется лестница цен из §14.4, когда
        /// она станет ассетом. Ноль — покупать больше нечего.
        /// </summary>
        public int NextSlotStep
            => CanBuySlot ? Saves.Profile.DeckSlots - PlayerProfile.BASE_DECK_SLOTS + 1 : 0;

        /// <summary>
        /// ПОКУПКА СЛОТА КОЛОДЫ (docs/10 §13.3). Дополнительные КОЛБЫ, в отличие
        /// от слотов, не продаются никогда — это отвергнуто в §13.3 как покупка снижения
        /// сложности.
        /// </summary>
        public bool TryBuySlot(int price)
        {
            if (!CanBuySlot || !CanAfford(price))
                return false;

            Saves.Profile.Coins -= price;
            Saves.Profile.DeckSlots++;
            Saves.RequestSave($"куплен слот колоды №{Saves.Profile.DeckSlots}");

            OnCoinsChanged?.Invoke(Saves.Profile.Coins);
            OnDeckSlotsChanged?.Invoke(Saves.Profile.DeckSlots);
            OnLoadoutChanged?.Invoke();
            return true;
        }

        // =====================================================================================
        // Внутреннее
        // =====================================================================================

        /// <summary>
        /// Босс главы пройден — открываются герои, назначенные на эту главу (§15.4:
        /// узел 5 → второй герой, узел 10 → третий). Расписание живёт в самих книгах
        /// (<c>Book.UnlockChapter</c>), а не списком здесь: список пришлось бы держать
        /// синхронным с папкой книг вручную.
        /// </summary>
        private void UnlockHeroesOfChapter(int chapter)
        {
            foreach (Book book in BookCatalog.All)
            {
                if (book == null || book.UnlockChapter != chapter || IsHeroUnlocked(book))
                    continue;

                if (Saves.Profile.UnlockHero(book.HeroId))
                {
                    Saves.RequestSave($"открыт герой «{book.HeroId}» за босса главы {chapter}");
                    OnHeroUnlocked?.Invoke(book);
                }
            }
        }

        /// <summary>
        /// Герой из сейва мог стать закрытым: геймдизайнер поставил ему главу уже после
        /// того, как игрок им играл. Отбирать сыгранного героя нельзя — это выглядит как
        /// потерянный прогресс, то есть ровно тот дефект, который сейвы и чинят.
        /// </summary>
        private void EnsureCurrentHeroUnlocked()
        {
            if (_currentBook == null || IsHeroUnlocked(_currentBook))
                return;

            if (Saves.Profile.UnlockHero(_currentBook.HeroId))
                Debug.Log($"[Мета] Герой «{_currentBook.HeroId}» был выбран до того, как ему назначили главу — " +
                          "оставляем открытым.");
        }

        /// <summary>
        /// Достать из сейва героя, которым играли в прошлый раз.
        ///
        /// Любой сбой здесь — НЕ повод остаться без книги: без книги не собирается ни одна
        /// комбинация, то есть игра запустится, но играть в неё будет нельзя. Поэтому все
        /// три плохих случая (в сейве пусто, ассет переименовали, ассет удалили) ведут
        /// в одно место — герой по умолчанию из <c>GameManager._startBook</c>.
        /// </summary>
        private Book ResolveSavedBook(Book fallbackBook)
        {
            string savedHeroId = Saves.Profile.HeroId;
            if (string.IsNullOrEmpty(savedHeroId))
                return fallbackBook;

            Book savedBook = BookCatalog.Find(savedHeroId);
            if (savedBook != null)
                return savedBook;

            Debug.LogWarning($"[Saves] Героя «{savedHeroId}» из сейва нет среди книг " +
                             $"(Resources/{BookCatalog.RESOURCES_PATH}). Берём героя по умолчанию.");
            return fallbackBook;
        }

        private void RaiseLoadoutChanged() => OnLoadoutChanged?.Invoke();

        /// <summary>
        /// Освобождение по правилу проекта: снимаем подписку на карту и зануляем свои
        /// события. Зовётся при разборе главного экрана; сегодня это конец приложения.
        /// </summary>
        public void Exit()
        {
            Map.OnChapterCleared -= UnlockHeroesOfChapter;

            foreach (KeyValuePair<Book, HeroLoadout> pair in _loadouts)
                pair.Value.OnChanged -= RaiseLoadoutChanged;
            _loadouts.Clear();

            Map.Exit();

            OnCoinsChanged = null;
            OnDeckSlotsChanged = null;
            OnHeroUnlocked = null;
            OnHeroSelected = null;
            OnLoadoutChanged = null;
        }

        /// <summary>Герой по умолчанию — для окон, которым нужен фолбэк.</summary>
        public Book FallbackBook => _fallbackBook;
    }
}
