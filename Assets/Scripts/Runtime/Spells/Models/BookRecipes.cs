using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Spells
{
    /// <summary>
    /// КНИГА, РАЗЛОЖЕННАЯ ПО ИДЕНТИФИКАТОРАМ РЕЦЕПТОВ, и закон колоды поверх неё
    /// (docs/10 §13.2).
    ///
    /// ═══ ЗАКОН ═══
    ///
    /// «Слот занимает каждый рецепт, а глубокий рецепт нельзя взять без его префиксов.»
    /// <c>Огонь &gt; Огонь &gt; Сила</c> требует, чтобы в колоде лежали <c>Огонь</c>
    /// и <c>Огонь &gt; Огонь</c>, — три слота. Выбор становится осязаемым: уйти вглубь
    /// одной сильной веткой или разложиться вширь на много дешёвых ответов.
    ///
    /// ═══ РАЗВИЛКА, КОТОРУЮ ПРИШЛОСЬ ЗАКРЫТЬ: ПРЕФИКС, КОТОРОГО НЕ СУЩЕСТВУЕТ ═══
    ///
    /// В книге Воина ультимейт — <c>Fighting &gt; Fighting &gt; Fire</c>, а рецепта
    /// <c>Fighting &gt; Fighting</c> НЕТ: это промежуточный узел дерева без заклинания.
    /// Буквальное чтение закона запрещало бы ультимейт навсегда — потребованный префикс
    /// невозможно ни купить, ни экипировать, потому что он не существует как рецепт.
    /// И это не единичный случай: сегодня НИ ОДНА из четырёх книг проекта не проходит
    /// буквальную проверку (у Некроманта нет <c>Dark</c> и <c>Dark &gt; Venom &gt; Dark</c>,
    /// у Мага нет <c>Fire</c> и <c>Water</c>, у Чародейки нет <c>Fairy</c>, <c>Grass</c>
    /// и ещё двух узлов).
    ///
    /// ПРИНЯТОЕ ЗДЕСЬ ПРАВИЛО: <b>префикс обязан лежать в колоде только если он вообще
    /// существует как рецепт.</b> Несуществующий префикс пропускается молча.
    ///
    /// Почему именно так, а не «запретить такие рецепты» и не «считать промежуточный
    /// узел бесплатным слотом»:
    ///
    ///  · Промежуточный узел — НЕ ошибка данных, а осознанная форма дерева. Это записано
    ///    в <c>BookTreeReport</c> отдельным абзацем: на таком узле варка штатно не срабатывает,
    ///    и «глубокая ветка, назад дороги нет» — законный дизайн.
    ///  · Смысл закона — в ЦЕНЕ СЛОТА и в ЛЕСТНИЦЕ КЭШ-АУТОВ: игрок платит слотами за то,
    ///    что по дороге к сильному рецепту может остановиться и забрать награду.
    ///    Там, где остановиться НЕЛЬЗЯ (узел без заклинания), платить не за что —
    ///    промежуточный узел уже наказан тактами, которые игрок пройдёт без права
    ///    забрать награду.
    ///  · Закон при этом не размывается: если геймдизайнер ДОБАВИТ заклинание на
    ///    <c>Fighting &gt; Fighting</c>, префикс тут же станет обязательным — сам собой,
    ///    без правки кода. То есть правило читается как «нельзя перепрыгнуть кэш-аут,
    ///    который существует».
    ///
    /// ⚠️ Это дизайнерская развилка, а не техническая. Если геймдизайнер решит иначе
    /// («глубокий рецепт с промежуточным узлом запрещён» или «промежуточный узел занимает
    /// слот, но не даёт заклинания»), меняется РОВНО ОДИН метод — <see cref="RequiredPrefixes"/>.
    ///
    /// ═══ ПОЧЕМУ ПЛОСКИЙ ИНДЕКС, А НЕ <see cref="Table"/> ═══
    ///
    /// docs/10 §13.2 обещает, что правило «бесплатно вытекает из уже написанного trie».
    /// Обещание почти верно: правилу нужен НАБОР существующих цепочек, а trie — это
    /// реализация того же набора, заточенная под другой вопрос. <c>Table.TryGetSpell</c>
    /// спрашивают массивом <see cref="Core.Flask.Models.Element"/>, которого у меты нет
    /// (в сейве лежат числа), и он отвечает только про свою партию — а колоду собирают
    /// по ВСЕЙ книге, включая некупленное. Плоский словарь отвечает на оба вопроса
    /// одинаково дёшево и заодно отдаёт сам <see cref="Combination"/>, который окну
    /// колоды нужен, чтобы нарисовать карточку.
    /// </summary>
    public sealed class BookRecipes
    {
        private static readonly Dictionary<Book, BookRecipes> _cache = new Dictionary<Book, BookRecipes>();

        private readonly Dictionary<string, Combination> _byId;
        private readonly List<string> _ids;

        /// <summary>Стартовая колода в порядке выдачи: префикс всегда раньше своего наследника.</summary>
        private readonly List<string> _starters;

        private readonly HashSet<string> _starterSet;

        public Book Book { get; }

        /// <summary>Идентификаторы всех рецептов книги в порядке ассета.</summary>
        public IReadOnlyList<string> Ids => _ids;

        /// <summary>Все стихии книги — словарь для <c>RecipeId.Describe</c>.</summary>
        public Core.Flask.Models.Element[] Vocabulary { get; }

        private BookRecipes(Book book)
        {
            Book = book;
            _byId = new Dictionary<string, Combination>(StringComparer.Ordinal);
            _ids = new List<string>();

            Combination[] combinations = book == null ? Array.Empty<Combination>() : book.Combinations;
            foreach (Combination combination in combinations)
            {
                string id = RecipeId.Of(combination);
                if (string.IsNullOrEmpty(id))
                    continue;

                // Дубль цепочки — поломка данных, о которой ругается BookTreeReport.
                // Здесь повторяем поведение Table: выигрывает последний, иначе отчёт
                // показывал бы одну книгу, а мета работала бы с другой.
                _byId[id] = combination;
                if (!_ids.Contains(id))
                    _ids.Add(id);
            }

            Vocabulary = SpellDeck.CollectUniqElements(combinations);

            _starters = ResolveStarters(book);
            _starterSet = new HashSet<string>(_starters, StringComparer.Ordinal);
        }

        /// <summary>
        /// Разбор книги кэшируется: окно колоды спрашивает её на каждый кадр отрисовки,
        /// а книга за сессию не меняется.
        /// </summary>
        public static BookRecipes For(Book book)
        {
            if (book == null)
                return new BookRecipes(null);

            if (!_cache.TryGetValue(book, out BookRecipes recipes))
            {
                recipes = new BookRecipes(book);
                _cache.Add(book, recipes);
            }
            return recipes;
        }

        public bool Exists(string recipeId)
            => !string.IsNullOrEmpty(recipeId) && _byId.ContainsKey(recipeId);

        public bool TryGet(string recipeId, out Combination combination)
        {
            combination = null;
            return !string.IsNullOrEmpty(recipeId) && _byId.TryGetValue(recipeId, out combination);
        }

        public Combination Get(string recipeId)
            => TryGet(recipeId, out Combination combination) ? combination : null;

        /// <summary>
        /// ЗАКОН §13.2 В ОДНОЙ СТРОКЕ: какие рецепты обязаны лежать в колоде вместе
        /// с этим. Собственные префиксы, <b>отфильтрованные по существованию</b>
        /// (обоснование — в комментарии к классу), от короткого к длинному.
        /// </summary>
        public List<string> RequiredPrefixes(string recipeId)
        {
            List<string> required = new List<string>();
            foreach (string prefix in RecipeId.ProperPrefixes(recipeId))
                if (_byId.ContainsKey(prefix))
                    required.Add(prefix);
            return required;
        }

        /// <summary>
        /// Обратный вопрос — его задаёт кнопка «убрать из колоды»: кто из
        /// <paramref name="among"/> держится на этом рецепте как на префиксе.
        /// Без него игрок вынул бы <c>Огонь</c> из-под <c>Огонь &gt; Огонь</c>
        /// и оставил колоду в состоянии, которое сам собрать не мог бы.
        /// </summary>
        public List<string> DependentsOf(string recipeId, IEnumerable<string> among)
        {
            List<string> dependents = new List<string>();
            if (string.IsNullOrEmpty(recipeId) || among == null)
                return dependents;

            foreach (string other in among)
            {
                if (string.IsNullOrEmpty(other) || other == recipeId)
                    continue;
                if (RecipeId.IsPrefixOf(recipeId, other) && !dependents.Contains(other))
                    dependents.Add(other);
            }
            return dependents;
        }

        /// <summary>
        /// СТАРТОВЫЕ РЕЦЕПТЫ — те, что игрок получает вместе с героем и не покупает
        /// (docs/10 §14.4, §15.4, §17.2). Порядок значащий: префикс всегда раньше
        /// своего наследника, потому что <c>HeroLoadout.EnsureStarterDeck</c> надевает
        /// их подряд и упирается в число слотов.
        ///
        /// ═══ ДВА ИСТОЧНИКА, И ПЕРВЫЙ ГЛАВНЕЕ ═══
        ///
        ///  1. <b>Явный список в самой книге</b> (<see cref="Book.StarterRecipes"/>).
        ///     Заполнен — он и есть ответ.
        ///  2. Пусто — <b>прежнее правило</b>: все рецепты самой короткой длины, какая
        ///     есть в книге (<see cref="ShortestLength"/>).
        ///
        /// ПОЧЕМУ ПОНАДОБИЛСЯ ЯВНЫЙ СПИСОК, если правило «выводится из данных» звучит
        /// надёжнее. Потому что оно выводит НЕ ТО. Стартовая колода — это дизайнерское
        /// решение «с чем игрок садится в первый бой», и оно не обязано совпадать
        /// с формальным признаком «самый короткий рецепт»:
        ///
        ///  · docs/10 §17.2 требует выдать Воину ТРЕТИЙ рецепт — <c>Огонь &gt; Огонь</c>,
        ///    длины 2, — потому что с двумя рецептами длины 1 решение «забрать сейчас
        ///    или достроить» не существует как таковое: достраивать не во что. Правило
        ///    по длине этот рецепт выдать не может в принципе.
        ///  · «Добавить всем один рецепт длины 2» тоже не работает: у Некроманта,
        ///    Мага и Чародейки самый короткий рецепт УЖЕ длины 2, и такая добавка либо
        ///    ничего не изменит, либо раздаст лишнее.
        ///
        /// Отсюда форма: решение записывается там, где его принимают, — в книге, ассетом,
        /// без правки кода. Правило по длине остаётся ФОЛБЭКОМ, а не вторым мнением:
        /// оно отвечает только за книги, которым стартовую колоду ещё не назначили
        /// (сегодня это Некромант, Маг, Чародейка и книги замера такта), и заодно
        /// закрывает вырожденный случай — стартовых рецептов не может не быть,
        /// поэтому колода непуста по построению.
        ///
        /// ═══ ПРЕФИКСЫ ДОБАВЛЯЮТСЯ САМИ ═══
        ///
        /// Закон §13.2 требует, чтобы вместе с <c>Огонь &gt; Огонь</c> в колоде лежал
        /// <c>Огонь</c>. Стартовый список замыкается по существующим префиксам, иначе
        /// выданный рецепт не прошёл бы <c>HeroLoadout.CanEquip</c> и был бы выброшен
        /// из колоды при первой же починке сейва — то есть подарок молча исчез бы.
        /// Несуществующий префикс (промежуточный узел дерева) пропускается — так же,
        /// как в <see cref="RequiredPrefixes"/>.
        /// </summary>
        public List<string> StarterRecipes() => new List<string>(_starters);

        /// <summary>Длина самого короткого рецепта книги. 0 — рецептов нет вовсе.</summary>
        public int ShortestLength
        {
            get
            {
                int shortest = 0;
                foreach (string id in _ids)
                {
                    int length = RecipeId.Length(id);
                    if (shortest == 0 || length < shortest)
                        shortest = length;
                }
                return shortest;
            }
        }

        /// <summary>
        /// Бесплатный ли это рецепт — выдаётся с героем и в лавке не показывается.
        ///
        /// Спрашивает ТОТ ЖЕ набор, что отдаёт <see cref="StarterRecipes"/>, а не считает
        /// признак заново: две формулировки одного правила разъехались бы молча, и разъезд
        /// выглядел бы как «рецепт и выдан бесплатно, и продаётся в лавке» (или наоборот —
        /// «куплен за клубки, а мета считает его подарком и стирает покупку»).
        /// </summary>
        public bool IsStarter(string recipeId)
            => !string.IsNullOrEmpty(recipeId) && _starterSet.Contains(recipeId);

        /// <summary>
        /// Разбор стартовой колоды книги — один раз на книгу, вместе с самим разбором.
        /// Порядок разобран в <see cref="StarterRecipes"/>; здесь только исполнение.
        /// </summary>
        private List<string> ResolveStarters(Book book)
        {
            List<string> declared = new List<string>();
            int dropped = 0;

            foreach (Combination combination in book == null ? Array.Empty<Combination>() : book.StarterRecipes)
            {
                string id = RecipeId.Of(combination);

                // Ассет мог быть удалён, вынут из книги или указан дважды. Ни один
                // из случаев не повод падать: стартовая колода — это подарок, а не закон.
                if (string.IsNullOrEmpty(id) || !_byId.ContainsKey(id))
                {
                    dropped++;
                    continue;
                }
                if (!declared.Contains(id))
                    declared.Add(id);
            }

            if (dropped > 0)
                Debug.LogWarning($"[Книга] В стартовой колоде «{(book == null ? "?" : book.name)}» " +
                                 $"пропущено записей: {dropped}. Комбинации нет в списке рецептов этой книги " +
                                 "(или ссылка пуста) — выдать её игроку нельзя.");

            List<string> source = declared.Count > 0 ? declared : ShortestRecipes();

            // Замыкание по префиксам (закон §13.2). Префикс кладётся ПЕРЕД наследником:
            // EnsureStarterDeck надевает список подряд и на нехватке слотов обрывается,
            // а оборваться он обязан на длинном рецепте, а не на его основании.
            List<string> closed = new List<string>(source.Count);
            foreach (string id in source)
            {
                foreach (string prefix in RequiredPrefixes(id))
                    if (!closed.Contains(prefix))
                        closed.Add(prefix);

                if (!closed.Contains(id))
                    closed.Add(id);
            }

            return closed;
        }

        /// <summary>
        /// ПРАВИЛО-ФОЛБЭК: все рецепты самой короткой длины книги. Работает, только пока
        /// стартовая колода и «самое дешёвое, чем герой умеет играть» — это одно и то же.
        /// </summary>
        private List<string> ShortestRecipes()
        {
            List<string> shortest = new List<string>();
            int length = ShortestLength;
            if (length <= 0)
                return shortest;

            foreach (string id in _ids)
                if (RecipeId.Length(id) == length)
                    shortest.Add(id);

            return shortest;
        }

        /// <summary>Сколько слотов колоды займёт этот рецепт вместе с обязательными префиксами.</summary>
        public int SlotCost(string recipeId, ICollection<string> alreadyEquipped)
        {
            if (!Exists(recipeId))
                return 0;

            int cost = alreadyEquipped != null && alreadyEquipped.Contains(recipeId) ? 0 : 1;
            foreach (string prefix in RequiredPrefixes(recipeId))
                if (alreadyEquipped == null || !alreadyEquipped.Contains(prefix))
                    cost++;
            return cost;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Кэш ассетов переживает выход из Play Mode при выключенном Domain Reload,
        /// а вместе с ним — разбор книги, которую в это время правили в инспекторе.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForEditor() => _cache.Clear();
#endif
    }
}
