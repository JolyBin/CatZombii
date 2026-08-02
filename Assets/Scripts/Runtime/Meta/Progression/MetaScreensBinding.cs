using System;
using System.Collections.Generic;
using Core.Flask.Models;
using Core.Spells;
using Meta.Models;
using UnityEngine;
using Utility.Services.Localization;
using Utility.Services.Saves;

namespace Meta
{
    /// <summary>
    /// ШОВ СО СТОРОНЫ ДАННЫХ — пара к <see cref="MetaScreensController"/>.
    ///
    /// Экраны меты просят три модели (<c>MapScreenModel</c>, <c>DeckScreenModel</c>,
    /// <c>ShopScreenModel</c>) и присылают обратно пять событий. Этот класс собирает
    /// модели из настоящего профиля и применяет клики ПО ЗАКОНАМ docs/10 §13.
    ///
    /// ПОДКЛЮЧЕНИЕ живёт в <see cref="HomeController.OpenMap"/> и выглядит так:
    /// <code>
    /// var binding = homeController.CreateScreensBinding();
    /// var screens = new MetaScreensController(uiService);
    /// binding.AttachTo(screens);   // расставит источники моделей и подпишет обработчики
    /// screens.OnExit += ...;       // «назад» с карты — единственный выход из меты
    /// screens.ShowMap();
    /// // на выходе, обязательно обе половины: binding.ClearAction(); screens.Exit();
    /// </code>
    ///
    /// ═══ ЧТО ЗДЕСЬ СОЗНАТЕЛЬНО НЕ ТАК, КАК В ЗАГЛУШКЕ ═══
    ///
    /// 1. УЗЛОВ РОВНО СТОЛЬКО, СКОЛЬКО СОБРАНО УРОВНЕЙ. Заглушка добивала витрину
    ///    до пятнадцати запертыми узлами. Здесь этого нет: недостающие восемь уровней —
    ///    работа геймдизайнера, и рисовать их запертыми значит обещать контент, которого
    ///    может не быть ни в этой форме, ни в этом количестве. Число берётся из
    ///    <see cref="MapProgress.NodeCount"/>, то есть из <c>BattleConfig[]</c>.
    /// 2. БОСС — НЕ «номер кратен пяти», а последний узел главы (§13.4). При семи узлах
    ///    кратность пяти дала бы одного босса на узле 5 и ни одного финала.
    /// 3. РЕЦЕПТЫ — ТОЛЬКО ТЕКУЩЕГО ГЕРОЯ. Заглушка сваливала книги всех героев в одну
    ///    витрину, потому что героя у неё не было. Колода принадлежит герою: чужой
    ///    рецепт нельзя ни надеть, ни применить.
    /// 4. «ПРИНОСИТ НОВУЮ СТИХИЮ» СЧИТАЕТСЯ, а не выдумывается по остатку от деления —
    ///    это и есть закон §13.3, ради которого окно подтверждения существует.
    /// </summary>
    public sealed class MetaScreensBinding : IAction
    {
        /// <summary>Префикс идентификатора предложения «слот колоды» в лавке.</summary>
        public const string SLOT_OFFER_PREFIX = "slot.";

        private readonly HomeController _home;
        private readonly MetaController _meta;

        private MetaScreensController _screens;

        /// <summary>
        /// Почему последнее действие не прошло — уже локализованная строка. Живёт
        /// для логов и отладки: <b>не</b> гасится показом, поэтому её всегда можно
        /// спросить «а что было последним отказом».
        /// </summary>
        public string LastRefusal { get; private set; } = string.Empty;

        /// <summary>
        /// Отказ, ЕЩЁ НЕ ПОКАЗАННЫЙ игроку. Отдаётся ровно один раз —
        /// <see cref="ConsumeRefusal"/> забирает его в модель и обнуляет.
        ///
        /// Одноразовость здесь не оптимизация, а способ не заводить у сообщения
        /// собственную жизнь. Иначе всплывашке нужен либо таймер (в <c>Runtime</c>
        /// намеренно нет ни одного <c>UniTask.Delay</c> — docs/10 §0.2), либо явное
        /// «погасить» из каждого места, где что-то поменялось, и первое же забытое
        /// место оставит игроку «нет свободного слота» поверх лавки. Одноразовая
        /// строка гаснет сама: следующая перерисовка соберёт модель уже с пустым
        /// отказом, а перерисовка случается и после успеха, и при смене экрана.
        /// </summary>
        private string _pendingRefusal = string.Empty;

        public MetaScreensBinding(HomeController home)
        {
            _home = home;
            _meta = home.Meta;
        }

        /// <summary>
        /// Связать себя с экранами: три источника моделей и четыре обработчика.
        /// Отписка — в <see cref="ClearAction"/>, по правилу проекта.
        /// </summary>
        public void AttachTo(MetaScreensController screens)
        {
            if (screens == null)
                return;

            _screens = screens;
            _screens.MapModelSource = BuildMap;
            _screens.DeckModelSource = BuildDeck;
            _screens.ShopModelSource = BuildShop;

            _screens.OnNodeChosen += HandleNodeChosen;
            _screens.OnRecipeChosen += HandleRecipeChosen;
            _screens.OnDeckSlotChosen += HandleSlotChosen;
            _screens.OnPurchaseConfirmed += HandlePurchase;
        }

        public void ClearAction()
        {
            if (_screens == null)
                return;

            _screens.OnNodeChosen -= HandleNodeChosen;
            _screens.OnRecipeChosen -= HandleRecipeChosen;
            _screens.OnDeckSlotChosen -= HandleSlotChosen;
            _screens.OnPurchaseConfirmed -= HandlePurchase;

            _screens.MapModelSource = null;
            _screens.DeckModelSource = null;
            _screens.ShopModelSource = null;
            _screens = null;
        }

        // =====================================================================================
        // Модели
        // =====================================================================================

        public MapScreenModel BuildMap()
        {
            MapProgress map = _meta.Map;
            MapNodeView[] nodes = new MapNodeView[map.NodeCount];

            for (int i = 0; i < nodes.Length; i++)
            {
                bool cleared = map.IsCleared(i);
                bool boss = map.IsBoss(i);
                int chapter = map.ChapterOf(i);

                nodes[i] = new MapNodeView
                {
                    Number = i + 1,
                    IsBoss = boss,
                    Reward = MetaEconomy.NodeReward(chapter, boss, !cleared),
                    State = !map.IsUnlocked(i) ? MapNodeState.Locked
                          : cleared ? MapNodeState.Cleared
                          : MapNodeState.Open,
                };
            }

            return new MapScreenModel { Nodes = nodes, Yarn = _meta.Coins, Refusal = ConsumeRefusal() };
        }

        public DeckScreenModel BuildDeck()
        {
            HeroLoadout loadout = _meta.CurrentLoadout;
            if (loadout == null)
                return new DeckScreenModel { Yarn = _meta.Coins, Refusal = ConsumeRefusal() };

            List<RecipeView> collection = new List<RecipeView>();
            foreach (string recipeId in loadout.AllRecipes)
                if (loadout.IsOwned(recipeId))
                    collection.Add(ToRecipeView(loadout, recipeId));

            // Слоты рисуются ПО ЭКИПИРОВАННОМУ ПОРЯДКУ: первый слот — первый взятый
            // рецепт. Порядок хранится в профиле именно затем, чтобы колода не
            // перетасовывалась сама между входами.
            int slotsMax = PlayerProfile.MAX_DECK_SLOTS;
            DeckSlotView[] slots = new DeckSlotView[slotsMax];
            for (int i = 0; i < slotsMax; i++)
            {
                bool unlocked = i < loadout.Slots;
                bool filled = unlocked && i < loadout.Equipped.Count;

                slots[i] = new DeckSlotView
                {
                    Index = i,
                    State = !unlocked ? DeckSlotState.Locked
                          : filled ? DeckSlotState.Filled
                          : DeckSlotState.Empty,
                    Recipe = filled ? ToRecipeView(loadout, loadout.Equipped[i]) : null,
                    UnlockPrice = unlocked ? 0 : MetaEconomy.SlotPrice(i - PlayerProfile.BASE_DECK_SLOTS + 1),
                };
            }

            return new DeckScreenModel
            {
                Slots = slots,
                Collection = collection.ToArray(),
                Elements = ToElementViews(loadout.BuildDeck().UniqElements),
                SlotsUnlocked = loadout.Slots,
                SlotsMax = slotsMax,
                Yarn = _meta.Coins,
                Refusal = ConsumeRefusal(),
            };
        }

        public ShopScreenModel BuildShop()
        {
            HeroLoadout loadout = _meta.CurrentLoadout;
            if (loadout == null)
                return new ShopScreenModel { Yarn = _meta.Coins, Refusal = ConsumeRefusal() };

            List<ShopOfferView> offers = new List<ShopOfferView>();
            int elementsNow = loadout.ElementCount;

            foreach (string recipeId in loadout.AllRecipes)
            {
                // Стартовые рецепты в лавке не бывают: они выдаются вместе с героем
                // (docs/10 §14.4), и показывать их как «куплено» — шум.
                if (loadout.Recipes.IsStarter(recipeId))
                    continue;

                bool owned = loadout.IsOwned(recipeId);
                int price = MetaEconomy.RecipePrice(recipeId);
                Element[] newElements = owned ? Array.Empty<Element>() : loadout.NewElementsOf(recipeId);

                Combination combination = loadout.Recipes.Get(recipeId);

                offers.Add(new ShopOfferView
                {
                    Id = recipeId,
                    Kind = ShopOfferKind.Recipe,
                    Name = combination != null && combination.Spell != null ? combination.Spell.Name : string.Empty,
                    Icon = combination != null && combination.Spell != null ? combination.Spell.Icon : null,
                    Price = price,
                    Owned = owned,
                    Affordable = _meta.CanAfford(price),
                    BringsNewElement = newElements.Length > 0,
                    ElementsBefore = elementsNow,
                    ElementsAfter = elementsNow + newElements.Length,
                });
            }

            // Слоты — второй сток валюты (§13.3). Дополнительные КОЛБЫ не продаются
            // никогда: §13.3 отвергает их как покупку снижения сложности.
            for (int step = 1; step <= MetaEconomy.SellableSlots; step++)
            {
                int slotNumber = PlayerProfile.BASE_DECK_SLOTS + step;
                bool owned = _meta.DeckSlots >= slotNumber;
                int price = MetaEconomy.SlotPrice(step);

                offers.Add(new ShopOfferView
                {
                    Id = SLOT_OFFER_PREFIX + slotNumber,
                    Kind = ShopOfferKind.DeckSlot,
                    Price = price,
                    Owned = owned,
                    // Купить можно только СЛЕДУЮЩИЙ слот: лестница цен растёт,
                    // и покупка восьмого через голову шестого сломала бы её порядок.
                    Affordable = !owned && step == _meta.NextSlotStep && _meta.CanAfford(price),
                    SlotNumber = slotNumber,
                    ElementsBefore = elementsNow,
                    ElementsAfter = elementsNow,
                });
            }

            return new ShopScreenModel
            {
                Offers = offers.ToArray(),
                Yarn = _meta.Coins,
                Refusal = ConsumeRefusal(),
            };
        }

        // =====================================================================================
        // Клики
        // =====================================================================================

        /// <summary>
        /// Игрок ткнул в узел карты. Номер 1-based — так его отдаёт экран.
        ///
        /// Отказ здесь редкий: запертый узел уже не кликается (<c>UIMapNode</c> гасит
        /// <c>Button.interactable</c>), поэтому сюда доходит либо узел без собранного
        /// уровня, либо рассинхрон карты с прогрессом. Игроку в обоих случаях говорим
        /// одно и то же — «узел ещё закрыт»; разницу видит только консоль.
        /// </summary>
        public void HandleNodeChosen(int number)
        {
            if (!_home.TryStartNode(number - 1))
                Refuse(Localization.Get(LocKeys.MapErrorNodeLocked));
        }

        /// <summary>
        /// Тап по карточке рецепта — «надеть или снять». Оба закона §13.2 живут
        /// в <see cref="HeroLoadout"/>, здесь только выбор направления и перерисовка.
        /// </summary>
        public void HandleRecipeChosen(string recipeId)
        {
            HeroLoadout loadout = _meta.CurrentLoadout;
            if (loadout == null)
                return;

            if (loadout.IsEquipped(recipeId))
            {
                UnequipVerdict verdict = loadout.CanUnequip(recipeId);
                if (!verdict.IsOk)
                {
                    Refuse(loadout.Describe(verdict));
                    return;
                }
                loadout.TryUnequip(recipeId);
            }
            else
            {
                EquipVerdict verdict = loadout.CanEquip(recipeId);
                if (!verdict.IsOk)
                {
                    Refuse(loadout.Describe(verdict));
                    return;
                }
                loadout.TryEquip(recipeId);
            }

            ClearRefusal();
            _screens?.Refresh();
        }

        /// <summary>
        /// Тап по слоту колоды — снять то, что в нём лежит.
        ///
        /// ЗАКРЫТЫЙ СЛОТ ВЕДЁТ В ЛАВКУ, а не молчит. Экран колоды рисует закрытые слоты
        /// вместе с ценой («Слот за 150») — это витрина стока валюты (docs/10 §13.3),
        /// и витрина, на которую нажали, а она не ответила, читается как поломка.
        /// Отдельной строки-отказа здесь не нужно: «нажал на цену — попал туда, где
        /// покупают» короче любого объяснения и не требует перевода.
        ///
        /// Пустой открытый слот молчит намеренно: снимать из него нечего, и говорить
        /// об этом — шум.
        /// </summary>
        public void HandleSlotChosen(int index)
        {
            HeroLoadout loadout = _meta.CurrentLoadout;
            if (loadout == null || index < 0)
                return;

            if (index >= loadout.Slots)
            {
                _screens?.ShowShop();
                return;
            }

            if (index >= loadout.Equipped.Count)
                return;

            HandleRecipeChosen(loadout.Equipped[index]);
        }

        /// <summary>
        /// Покупка подтверждена: предупреждение про новую стихию (§13.3) экран уже показал.
        /// Платится только ВЛАДЕНИЕ — экипировка остаётся бесплатной и обратимой (§15.5).
        /// </summary>
        public void HandlePurchase(string offerId)
        {
            if (string.IsNullOrEmpty(offerId))
                return;

            bool done;
            if (offerId.StartsWith(SLOT_OFFER_PREFIX, StringComparison.Ordinal))
                done = _meta.TryBuySlot(MetaEconomy.SlotPrice(_meta.NextSlotStep));
            else
                done = _meta.TryBuyRecipe(_meta.CurrentBook, offerId, MetaEconomy.RecipePrice(offerId));

            if (!done)
            {
                Refuse(Localization.Get(LocKeys.ShopNotEnough));
                return;
            }

            ClearRefusal();
            _screens?.Refresh();
        }

        // =====================================================================================
        // Мелочи
        // =====================================================================================

        /// <summary>Действие удалось — прошлому отказу на экране больше не место.</summary>
        private void ClearRefusal()
        {
            LastRefusal = string.Empty;
            _pendingRefusal = string.Empty;
        }

        /// <summary>
        /// ОТКАЗАТЬ И ПОКАЗАТЬ ЭТО ИГРОКУ. <paramref name="reason"/> — УЖЕ локализованная
        /// строка: формулировки живут рядом с законами, которые их порождают
        /// (<c>HeroLoadout.Describe</c>), а не здесь.
        ///
        /// Перерисовка обязательна и после отказа тоже: именно она донесёт строку
        /// до экрана через <c>model.Refusal</c>. В консоль отказ уходит по-прежнему —
        /// на плейтесте лог переживает закрытое окно, а полоса нет.
        /// </summary>
        private void Refuse(string reason)
        {
            LastRefusal = reason ?? string.Empty;
            _pendingRefusal = LastRefusal;
            Debug.Log($"[Мета] Отказ: {LastRefusal}");
            _screens?.Refresh();
        }

        /// <summary>Забрать неотданный отказ. Второй раз подряд вернёт пусто — см. <see cref="_pendingRefusal"/>.</summary>
        private string ConsumeRefusal()
        {
            string refusal = _pendingRefusal;
            _pendingRefusal = string.Empty;
            return refusal;
        }

        private RecipeView ToRecipeView(HeroLoadout loadout, string recipeId)
        {
            Combination combination = loadout.Recipes.Get(recipeId);
            return new RecipeView
            {
                Id = recipeId,
                Name = combination != null && combination.Spell != null ? combination.Spell.Name : string.Empty,
                Icon = combination != null && combination.Spell != null ? combination.Spell.Icon : null,
                Chain = ToElementViews(combination == null ? Array.Empty<Element>() : combination.Elements),
                Equipped = loadout.IsEquipped(recipeId),
            };
        }

        private static ElementView[] ToElementViews(Element[] elements)
        {
            if (elements == null)
                return Array.Empty<ElementView>();

            ElementView[] views = new ElementView[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                views[i] = elements[i] == null
                    ? new ElementView()
                    : new ElementView { Id = elements[i].ID, Color = elements[i].Color, Icon = elements[i].Texture };
            }
            return views;
        }
    }
}
