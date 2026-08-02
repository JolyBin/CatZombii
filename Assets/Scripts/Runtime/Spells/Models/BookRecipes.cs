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
        /// (docs/10 §14.4: «открыто с начала — оба рецепта длины 1 своего героя»;
        /// §15.4: «узел 1 — Барсик, 4 слота, два рецепта длины 1 бесплатно»).
        ///
        /// ЭТО ВСЕ РЕЦЕПТЫ САМОЙ КОРОТКОЙ ДЛИНЫ, КАКАЯ ЕСТЬ В КНИГЕ, а не буквально
        /// «длины 1». Разница появилась не из вкуса, а из контента: длина 1 есть только
        /// у Воина. У Некроманта, Мага и Чародейки самый короткий рецепт — длины 2,
        /// и буквальное правило оставило бы их без стартовой колоды вовсе, то есть
        /// с пустым пулом стихий и неиграбельным героем.
        ///
        /// Для книги, собранной по §14.4 (два рецепта длины 1), правило даёт ровно то,
        /// что написано в документе. Для любой другой — «самое дешёвое, чем этот герой
        /// умеет играть», что и есть смысл стартовой выдачи.
        ///
        /// Выведено из данных, а не перечислено списком: список сломался бы от любой
        /// правки книги. Побочно закрывается вырожденный случай — стартовых рецептов
        /// не может не быть, поэтому колода непуста по построению.
        /// </summary>
        public List<string> StarterRecipes()
        {
            List<string> starters = new List<string>();
            int shortest = ShortestLength;
            if (shortest <= 0)
                return starters;

            foreach (string id in _ids)
                if (RecipeId.Length(id) == shortest)
                    starters.Add(id);

            return starters;
        }

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

        /// <summary>Бесплатный ли это рецепт — выдаётся с героем и в лавке не показывается.</summary>
        public bool IsStarter(string recipeId)
            => Exists(recipeId) && RecipeId.Length(recipeId) == ShortestLength;

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
