using System;
using System.Collections.Generic;
using Core.Flask.Models;
using Core.Spells;
using UnityEngine;
using Utility.Services.Localization;
using Utility.Services.Saves;

namespace Meta
{
    /// <summary>Почему рецепт нельзя взять в колоду. Порядок причин — от дешёвых к дорогим.</summary>
    public enum EquipResult
    {
        Ok,
        AlreadyEquipped,
        UnknownRecipe,
        NotOwned,
        NoFreeSlot,
        MissingPrefix,
    }

    /// <summary>Почему рецепт нельзя убрать из колоды.</summary>
    public enum UnequipResult
    {
        Ok,
        NotEquipped,
        RequiredByOther,
        LastRecipe,
    }

    /// <summary>
    /// Приговор с объяснением. Возвращается ДО действия, чтобы окно колоды могло
    /// нарисовать кнопку серой и сказать почему, а не ловить отказ после клика.
    /// </summary>
    public readonly struct EquipVerdict
    {
        public readonly EquipResult Result;

        /// <summary>Рецепты, которых не хватает в колоде (закон §13.2). Пусто, кроме <see cref="EquipResult.MissingPrefix"/>.</summary>
        public readonly IReadOnlyList<string> MissingPrefixes;

        public bool IsOk => Result == EquipResult.Ok;

        public EquipVerdict(EquipResult result, IReadOnlyList<string> missingPrefixes = null)
        {
            Result = result;
            MissingPrefixes = missingPrefixes ?? Array.Empty<string>();
        }
    }

    public readonly struct UnequipVerdict
    {
        public readonly UnequipResult Result;

        /// <summary>Кто из колоды держится на этом рецепте как на префиксе.</summary>
        public readonly IReadOnlyList<string> Dependents;

        public bool IsOk => Result == UnequipResult.Ok;

        public UnequipVerdict(UnequipResult result, IReadOnlyList<string> dependents = null)
        {
            Result = result;
            Dependents = dependents ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// КОЛОДА ОДНОГО ГЕРОЯ И ВСЕ ЕЁ ЗАКОНЫ. Единственное место, где профиль (строки)
    /// встречается с книгой (ассеты), и единственное, что имеет право писать в
    /// <c>HeroProgress</c>.
    ///
    /// ═══ ТРИ ЗАКОНА, КОТОРЫЕ ЗДЕСЬ ЖИВУТ ═══
    ///
    /// §13.1 ПУЛ СТИХИЙ — ИЗ ЭКИПИРОВАННОГО. <see cref="BuildDeck"/> отдаёт партии
    ///       не книгу, а колоду. Отсюда «каждое новое заклинание делает пазл труднее»:
    ///       взял рецепт с новой стихией — она посыпалась в колбы. Прогрессия
    ///       балансирует себя сама, потому что единственное, что поднимает потолок
    ///       игрока, — единственное, что усложняет ему пазл.
    ///
    /// §13.2 СЛОТ ЗА КАЖДЫЙ РЕЦЕПТ, ПРЕФИКСЫ ОБЯЗАТЕЛЬНЫ. Сам закон и разбор развилки
    ///       «префикс, которого не существует как рецепт» — в <see cref="BookRecipes"/>.
    ///       Здесь только применение: <see cref="CanEquip"/> и <see cref="CanUnequip"/>.
    ///
    /// §14.4/§15.4/§17.2 СТАРТОВАЯ КОЛОДА выдаётся экипированной при первом появлении
    ///       героя в профиле (<see cref="EnsureStarterDeck"/>). ЧТО именно в неё входит,
    ///       решает не этот класс, а книга: явный список в ассете, а если он пуст —
    ///       правило по длинам. Разбор — <see cref="BookRecipes.StarterRecipes"/>.
    ///
    /// ═══ ЧТО ЗДЕСЬ СОЗНАТЕЛЬНО НЕ ЖИВЁТ ═══
    ///
    /// ЦЕНЫ. Покупка (<see cref="MetaController.TryBuyRecipe"/>) принимает цену
    /// параметром: цена — это баланс, то есть контент геймдизайнера, а не закон меты.
    /// Диапазоны из docs/10 §14.4 (80–120 за длину 2, 200–300 за длину 3) в код
    /// не переносятся, пока не станут ассетом.
    ///
    /// ═══ ПРО ЗАПИСЬ В СЕЙВ ═══
    ///
    /// Каждое изменение колоды пишется сразу (<c>Saves.RequestSave</c>), и это отличается
    /// от правила «сохраняемся там, где игрок заметит потерю» — потому что здесь он её
    /// как раз заметит: перебрать колоду заново дороже, чем перепройти узел.
    /// <c>RequestSave</c> — отложенная запись, а не поход в IndexedDB на каждый клик.
    /// </summary>
    public sealed class HeroLoadout
    {
        /// <summary>
        /// Колода не может быть пустой. Не вкусовщина: пустой пул стихий — это падение
        /// в <c>ElementsGenerator</c> (<c>Random.Range(0, 0)</c> по пустому списку),
        /// то есть «разобрал колоду до конца» превратилось бы в «игра не запускается».
        /// </summary>
        private const int MIN_EQUIPPED = 1;

        /// <summary>Колода изменилась: экипировали, сняли, купили, добавился слот.</summary>
        public event Action OnChanged;

        private readonly HeroProgress _progress;
        private readonly List<string> _equipped;

        public Book Book { get; }

        public BookRecipes Recipes { get; }

        public HeroLoadout(Book book)
        {
            Book = book;
            Recipes = BookRecipes.For(book);

            string heroId = book == null ? string.Empty : book.HeroId;
            bool isFirstTouch = Saves.Profile.FindHero(heroId) == null;
            _progress = Saves.Profile.GetOrCreateHero(heroId);
            _equipped = new List<string>(_progress.EquippedRecipes ?? Array.Empty<string>());

            DropUnknownRecipes();

            if (isFirstTouch)
                EnsureStarterDeck();
        }

        /// <summary>Слотов всего. Общие для всех героев (docs/10 §15.1) — поэтому из профиля.</summary>
        public int Slots => Saves.Profile.DeckSlots;

        public int UsedSlots => _equipped.Count;

        public int FreeSlots => Mathf.Max(0, Slots - UsedSlots);

        public IReadOnlyList<string> Equipped => _equipped;

        /// <summary>Все рецепты книги — то, что рисует окно колоды и лавка.</summary>
        public IReadOnlyList<string> AllRecipes => Recipes.Ids;

        /// <summary>
        /// Владеет ли игрок рецептом. Стартовые — ВСЕГДА да: это правило docs/10 §14.4,
        /// а не запись в сейве (см. <c>HeroProgress.OwnedRecipes</c> и
        /// <see cref="BookRecipes.StarterRecipes"/>).
        ///
        /// ⚠️ Следствие для СТАРЫХ сейвов: рецепт, который геймдизайнер сделал стартовым
        /// уже после релиза, у купившего его игрока перестаёт быть покупкой — запись
        /// вычищается из <c>OwnedRecipes</c> в <see cref="DropUnknownRecipes"/>, клубки
        /// не возвращаются. Терпимо, пока это происходит до первого релиза; после —
        /// потребуется разовая компенсация в <c>ProfileMigration</c>.
        /// </summary>
        public bool IsOwned(string recipeId)
        {
            if (!Recipes.Exists(recipeId))
                return false;
            if (Recipes.IsStarter(recipeId))
                return true;

            foreach (string owned in _progress.OwnedRecipes)
                if (owned == recipeId)
                    return true;

            return false;
        }

        public bool IsEquipped(string recipeId) => _equipped.Contains(recipeId);

        /// <summary>
        /// ЗАПИСАТЬ ПОКУПКУ. Не проверяет ни цену, ни кошелёк — этим занимается
        /// <see cref="MetaController.TryBuyRecipe"/>, у которого есть и то и другое.
        /// Экипировку покупка НЕ ТРОГАЕТ: docs/10 §15.5 требует, чтобы платилось только
        /// владение, а экипировка оставалась бесплатной и обратимой — иначе игрок,
        /// потративший клубки не туда, застревает без выхода.
        /// </summary>
        public bool AddOwned(string recipeId)
        {
            if (!Recipes.Exists(recipeId) || IsOwned(recipeId))
                return false;

            List<string> owned = new List<string>(_progress.OwnedRecipes) { recipeId };
            _progress.OwnedRecipes = owned.ToArray();
            OnChanged?.Invoke();
            return true;
        }

        // =====================================================================================
        // Закон колоды (§13.2)
        // =====================================================================================

        public EquipVerdict CanEquip(string recipeId)
        {
            if (!Recipes.Exists(recipeId))
                return new EquipVerdict(EquipResult.UnknownRecipe);
            if (IsEquipped(recipeId))
                return new EquipVerdict(EquipResult.AlreadyEquipped);
            if (!IsOwned(recipeId))
                return new EquipVerdict(EquipResult.NotOwned);

            // Префиксы, которых не хватает. Некупленный префикс — тоже «не хватает»:
            // в колоду он всё равно не ляжет, и честнее показать это одной причиной,
            // а не отправить игрока кликать по цепочке отказов.
            List<string> missing = new List<string>();
            foreach (string prefix in Recipes.RequiredPrefixes(recipeId))
                if (!IsEquipped(prefix))
                    missing.Add(prefix);

            if (missing.Count > 0)
                return new EquipVerdict(EquipResult.MissingPrefix, missing);

            if (FreeSlots < 1)
                return new EquipVerdict(EquipResult.NoFreeSlot);

            return new EquipVerdict(EquipResult.Ok);
        }

        public bool TryEquip(string recipeId)
        {
            if (!CanEquip(recipeId).IsOk)
                return false;

            _equipped.Add(recipeId);
            Commit($"рецепт «{recipeId}» в колоду");
            return true;
        }

        public UnequipVerdict CanUnequip(string recipeId)
        {
            if (!IsEquipped(recipeId))
                return new UnequipVerdict(UnequipResult.NotEquipped);

            List<string> dependents = Recipes.DependentsOf(recipeId, _equipped);
            if (dependents.Count > 0)
                return new UnequipVerdict(UnequipResult.RequiredByOther, dependents);

            if (_equipped.Count <= MIN_EQUIPPED)
                return new UnequipVerdict(UnequipResult.LastRecipe);

            return new UnequipVerdict(UnequipResult.Ok);
        }

        public bool TryUnequip(string recipeId)
        {
            if (!CanUnequip(recipeId).IsOk)
                return false;

            _equipped.Remove(recipeId);
            Commit($"рецепт «{recipeId}» из колоды");
            return true;
        }

        /// <summary>
        /// Сколько слотов реально стоит взять этот рецепт — вместе с недостающими
        /// префиксами. Ровно то число, которое надо показать на карточке в лавке:
        /// «Огонь &gt; Огонь &gt; Сила» стоит три слота, а не один.
        /// </summary>
        public int SlotCost(string recipeId) => Recipes.SlotCost(recipeId, _equipped);

        // =====================================================================================
        // Закон пула стихий (§13.1) — то, ради чего вся мета
        // =====================================================================================

        /// <summary>Стихий в колоде сейчас. Это E из docs/10 §13.6.</summary>
        public int ElementCount => EquippedElements().Length;

        /// <summary>
        /// Стихии, которых в колоде ещё нет, а после экипировки рецепта (вместе с его
        /// обязательными префиксами) появятся. Пусто — пазл не изменится.
        ///
        /// Это и есть то самое честное предупреждение при покупке из docs/10 §13.3:
        /// «в колбах станет 4 стихии вместо 3». Стихии отдельно не продаются —
        /// стихия приходит вместе с первым рецептом, который её использует.
        /// </summary>
        public Element[] NewElementsOf(string recipeId)
        {
            if (!Recipes.Exists(recipeId))
                return Array.Empty<Element>();

            List<string> candidate = new List<string>(_equipped);
            foreach (string prefix in Recipes.RequiredPrefixes(recipeId))
                if (!candidate.Contains(prefix))
                    candidate.Add(prefix);
            if (!candidate.Contains(recipeId))
                candidate.Add(recipeId);

            Element[] before = EquippedElements();
            Element[] after = SpellDeck.CollectUniqElements(ToCombinations(candidate));

            List<Element> added = new List<Element>();
            foreach (Element element in after)
                if (Array.IndexOf(before, element) < 0)
                    added.Add(element);

            return added.ToArray();
        }

        /// <summary>Сколько стихий будет в колбах, если взять этот рецепт. Пара к <see cref="ElementCount"/>.</summary>
        public int ElementCountAfterEquip(string recipeId)
            => ElementCount + NewElementsOf(recipeId).Length;

        /// <summary>
        /// ТО, С ЧЕМ ИГРОК ИДЁТ В БОЙ. Здесь закон §13.1 и превращается в игру:
        /// <c>StepsController</c> раздаёт <see cref="SpellDeck.Combinations"/> в
        /// <c>Table</c>, а <see cref="SpellDeck.UniqElements"/> — в <c>FlaskController</c>.
        ///
        /// ФОЛБЭК НА ВСЮ КНИГУ — страховка, а не режим. Пустая колода означает пустой пул
        /// стихий, то есть падение генератора шариков; довести до этого может только
        /// битый сейв (законы выше пустую колоду не разрешают). Молча падать в бою хуже,
        /// чем сыграть книгой целиком и написать об этом в лог.
        /// </summary>
        public SpellDeck BuildDeck()
        {
            List<Combination> combinations = ToCombinations(_equipped);
            if (combinations.Count == 0)
            {
                Debug.LogWarning($"[Мета] У героя «{HeroIdOf(Book)}» пустая колода — играем всей книгой. " +
                                 "Так быть не должно: пустой пул стихий роняет генератор шариков.");
                return SpellDeck.WholeBook(Book);
            }
            return new SpellDeck(Book, combinations);
        }

        // =====================================================================================
        // Внутреннее
        // =====================================================================================

        /// <summary>
        /// СТАРТОВАЯ КОЛОДА выдаётся ровно один раз — при первом появлении героя
        /// в профиле. Пустая (но существующая) запись означает «игрок сам разобрал
        /// колоду», и собирать её обратно при каждом входе было бы отменой его решения.
        /// </summary>
        private void EnsureStarterDeck()
        {
            if (_equipped.Count > 0)
                return;

            foreach (string starter in Recipes.StarterRecipes())
            {
                if (_equipped.Count >= Slots)
                    break;
                _equipped.Add(starter);
            }

            if (_equipped.Count > 0)
                Commit($"стартовая колода героя «{HeroIdOf(Book)}»");
        }

        /// <summary>
        /// Сейв — ВНЕШНИЕ данные, а книга могла поменяться между сборками (рецепт
        /// вырезали, цепочку переписали). Неизвестный книге идентификатор — не повод
        /// падать: он просто выпадает из колоды. Заодно чинится порядок префиксов,
        /// если руками правленый сейв его нарушил.
        /// </summary>
        private void DropUnknownRecipes()
        {
            bool changed = false;

            for (int i = _equipped.Count - 1; i >= 0; i--)
            {
                if (Recipes.Exists(_equipped[i]) && IsOwned(_equipped[i]))
                    continue;
                _equipped.RemoveAt(i);
                changed = true;
            }

            if (_equipped.Count > Slots)
            {
                _equipped.RemoveRange(Slots, _equipped.Count - Slots);
                changed = true;
            }

            // Префикс мог выпасть вместе с вырезанным рецептом — тогда наследники
            // остались бы в колоде вопреки §13.2. Снимаем их, пока набор не станет
            // замкнутым: за проход снимается хотя бы один, значит цикл конечен.
            bool stable = false;
            while (!stable)
            {
                stable = true;
                for (int i = _equipped.Count - 1; i >= 0; i--)
                {
                    foreach (string prefix in Recipes.RequiredPrefixes(_equipped[i]))
                    {
                        if (_equipped.Contains(prefix))
                            continue;
                        _equipped.RemoveAt(i);
                        changed = true;
                        stable = false;
                        break;
                    }
                }
            }

            List<string> owned = new List<string>();
            foreach (string recipeId in _progress.OwnedRecipes)
                if (Recipes.Exists(recipeId) && !Recipes.IsStarter(recipeId))
                    owned.Add(recipeId);

            if (owned.Count != _progress.OwnedRecipes.Length)
            {
                _progress.OwnedRecipes = owned.ToArray();
                changed = true;
            }

            if (changed)
            {
                _progress.EquippedRecipes = _equipped.ToArray();
                Debug.LogWarning($"[Мета] Колода героя «{HeroIdOf(Book)}» починена: в сейве были рецепты, " +
                                 "которых книга больше не знает или которые нарушали правило префиксов.");
            }
        }

        private void Commit(string reason)
        {
            _progress.EquippedRecipes = _equipped.ToArray();
            Saves.RequestSave(reason);
            OnChanged?.Invoke();
        }

        private Element[] EquippedElements()
            => SpellDeck.CollectUniqElements(ToCombinations(_equipped));

        private List<Combination> ToCombinations(IReadOnlyList<string> recipeIds)
        {
            List<Combination> combinations = new List<Combination>(recipeIds.Count);
            foreach (string recipeId in recipeIds)
                if (Recipes.TryGet(recipeId, out Combination combination))
                    combinations.Add(combination);
            return combinations;
        }

        private static string HeroIdOf(Book book) => book == null ? "?" : book.HeroId;

        // =====================================================================================
        // Формулировки отказов
        // =====================================================================================

        /// <summary>
        /// ОТКАЗ СЛОВАМИ ИГРОКА. Живёт рядом с законом, а не в окне: правило и его
        /// объяснение обязаны меняться вместе, иначе экран однажды начнёт рассказывать
        /// про запрет, которого уже нет.
        /// </summary>
        public string Describe(EquipVerdict verdict)
        {
            switch (verdict.Result)
            {
                case EquipResult.Ok:
                    return string.Empty;
                case EquipResult.AlreadyEquipped:
                    return Localization.Get(LocKeys.DeckErrorAlreadyEquipped);
                case EquipResult.NotOwned:
                    return Localization.Get(LocKeys.DeckErrorNotOwned);
                case EquipResult.NoFreeSlot:
                    return Localization.Get(LocKeys.DeckErrorNoSlot);
                case EquipResult.MissingPrefix:
                    return Localization.Get(LocKeys.DeckErrorNeedsPrefix, DescribeList(verdict.MissingPrefixes));
                default:
                    return Localization.Get(LocKeys.DeckErrorUnknownRecipe);
            }
        }

        public string Describe(UnequipVerdict verdict)
        {
            switch (verdict.Result)
            {
                case UnequipResult.Ok:
                    return string.Empty;
                case UnequipResult.RequiredByOther:
                    return Localization.Get(LocKeys.DeckErrorRequiredBy, DescribeList(verdict.Dependents));
                case UnequipResult.LastRecipe:
                    return Localization.Get(LocKeys.DeckErrorLastRecipe);
                default:
                    return Localization.Get(LocKeys.DeckErrorNotEquipped);
            }
        }

        /// <summary>Цепочка стихий словами: «Огонь &gt; Огонь». Словарь — стихии книги.</summary>
        public string DescribeRecipe(string recipeId) => RecipeId.Describe(recipeId, Recipes.Vocabulary);

        private string DescribeList(IReadOnlyList<string> recipeIds)
        {
            if (recipeIds == null || recipeIds.Count == 0)
                return string.Empty;

            string[] described = new string[recipeIds.Count];
            for (int i = 0; i < recipeIds.Count; i++)
                described[i] = DescribeRecipe(recipeIds[i]);

            return string.Join(", ", described);
        }
    }
}
