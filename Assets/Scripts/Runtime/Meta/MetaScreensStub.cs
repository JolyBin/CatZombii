using System.Collections.Generic;
using Core.Battle;
using Core.Flask.Models;
using Core.Spells;
using Meta.Models;
using UnityEngine;

namespace Meta
{
    /// <summary>
    /// ⚠️ ВРЕМЕННЫЙ ИСТОЧНИК ДАННЫХ ДЛЯ ТРЁХ ЭКРАНОВ МЕТЫ. Существует ровно затем,
    /// чтобы каркас можно было увидеть и потыкать до того, как появится настоящий
    /// профиль. **Удаляется целиком**, как только слой данных отдаст свои
    /// <c>Func&lt;...Model&gt;</c> в <see cref="MetaScreensController"/>.
    ///
    /// Что здесь ПРАВДА, а не выдумка:
    ///  • число узлов берётся из числа уровней в <c>Resources/Battle</c>, а недостающие
    ///    до пятнадцати (docs/10 §13.4) добираются запертыми — экран не врёт про контент,
    ///    которого нет;
    ///  • стихии и рецепты читаются из настоящих ассетов книги, поэтому цвета, спрайты
    ///    и имена — те же, что в бою.
    ///
    /// Что здесь ЛОЖЬ и должно уйти вместе с классом: прогресс, кошелёк, цены,
    /// принадлежность рецептов и признак «куплено».
    /// </summary>
    public static class MetaScreensStub
    {
        /// <summary>Узлов в кампании по docs/10 §13.4. Здесь — только чтобы добить витрину запертыми.</summary>
        private const int DESIGNED_NODE_COUNT = 15;

        private const int STUB_YARN = 240;
        private const int STUB_CLEARED_NODES = 3;
        private const int STUB_UNLOCKED_SLOTS = 4;
        private const int SLOTS_MAX = 8;

        /// <summary>Цены слотов 5-го … 8-го (docs/10 §14.4).</summary>
        private static readonly int[] SlotPrices = { 150, 250, 400, 600 };

        public static MapScreenModel BuildMap()
        {
            BattleConfig[] levels = Resources.LoadAll<BattleConfig>("Battle");
            int playable = Mathf.Max(levels.Length, 1);
            int total = Mathf.Max(playable, DESIGNED_NODE_COUNT);

            MapNodeView[] nodes = new MapNodeView[total];
            for (int i = 0; i < total; i++)
            {
                int number = i + 1;
                bool playableNode = i < playable;

                nodes[i] = new MapNodeView
                {
                    Number = number,
                    IsBoss = number % 5 == 0,
                    Reward = number <= 5 ? 60 : number <= 10 ? 100 : 150,
                    State = !playableNode ? MapNodeState.Locked
                          : i < STUB_CLEARED_NODES ? MapNodeState.Cleared
                          : i == STUB_CLEARED_NODES ? MapNodeState.Open
                          : MapNodeState.Locked,
                };
            }

            return new MapScreenModel { Nodes = nodes, Yarn = STUB_YARN };
        }

        public static DeckScreenModel BuildDeck()
        {
            RecipeView[] collection = BuildCollection();

            // Экипировано столько, сколько влезает в открытые слоты. Это и создаёт
            // главное число экрана: стихии считаются по ЭКИПИРОВАННЫМ (docs/10 §13.1).
            int equipped = Mathf.Min(STUB_UNLOCKED_SLOTS, collection.Length);
            for (int i = 0; i < collection.Length; i++)
                collection[i].Equipped = i < equipped;

            DeckSlotView[] slots = new DeckSlotView[SLOTS_MAX];
            for (int i = 0; i < SLOTS_MAX; i++)
            {
                bool unlocked = i < STUB_UNLOCKED_SLOTS;
                slots[i] = new DeckSlotView
                {
                    Index = i,
                    State = !unlocked ? DeckSlotState.Locked
                          : i < equipped ? DeckSlotState.Filled
                          : DeckSlotState.Empty,
                    Recipe = unlocked && i < equipped ? collection[i] : null,
                    UnlockPrice = unlocked ? 0 : SlotPrices[Mathf.Min(i - STUB_UNLOCKED_SLOTS, SlotPrices.Length - 1)],
                };
            }

            return new DeckScreenModel
            {
                Slots = slots,
                Collection = collection,
                Elements = CollectElements(collection, onlyEquipped: true),
                SlotsUnlocked = STUB_UNLOCKED_SLOTS,
                SlotsMax = SLOTS_MAX,
                Yarn = STUB_YARN,
            };
        }

        public static ShopScreenModel BuildShop()
        {
            RecipeView[] collection = BuildCollection();
            int equipped = Mathf.Min(STUB_UNLOCKED_SLOTS, collection.Length);
            for (int i = 0; i < collection.Length; i++)
                collection[i].Equipped = i < equipped;

            int elementsNow = CollectElements(collection, onlyEquipped: true).Length;

            List<ShopOfferView> offers = new();

            for (int i = 0; i < collection.Length; i++)
            {
                RecipeView recipe = collection[i];
                bool owned = i < equipped;

                // «Приносит новую стихию» — то, ради чего §13.3 требует предупреждения.
                // В заглушке признак условный: настоящий считает слой данных, сравнивая
                // стихии рецепта со стихиями экипированной колоды.
                bool bringsNew = !owned && i % 3 == 1;
                int price = recipe.Chain.Length >= 3 ? 250 : recipe.Chain.Length == 2 ? 100 : 40;

                offers.Add(new ShopOfferView
                {
                    Id = recipe.Id,
                    Kind = ShopOfferKind.Recipe,
                    Name = recipe.Name,
                    Icon = recipe.Icon,
                    Price = price,
                    Owned = owned,
                    Affordable = STUB_YARN >= price,
                    BringsNewElement = bringsNew,
                    ElementsBefore = elementsNow,
                    ElementsAfter = bringsNew ? elementsNow + 1 : elementsNow,
                });
            }

            for (int i = 0; i < SlotPrices.Length; i++)
            {
                int slotNumber = STUB_UNLOCKED_SLOTS + i + 1;
                offers.Add(new ShopOfferView
                {
                    Id = "slot." + slotNumber,
                    Kind = ShopOfferKind.DeckSlot,
                    Price = SlotPrices[i],
                    Owned = false,
                    Affordable = STUB_YARN >= SlotPrices[i],
                    SlotNumber = slotNumber,
                    ElementsBefore = elementsNow,
                    ElementsAfter = elementsNow,
                });
            }

            return new ShopScreenModel { Offers = offers.ToArray(), Yarn = STUB_YARN };
        }

        /// <summary>
        /// Рецепты из настоящих книг. Берутся ВСЕ книги, какие есть в ресурсах:
        /// герой в заглушке не выбран, а рисовать пустой экран бессмысленно.
        /// </summary>
        private static RecipeView[] BuildCollection()
        {
            List<RecipeView> recipes = new();

            foreach (Book book in Resources.LoadAll<Book>("Spells"))
            {
                foreach (Combination combination in book.Combinations)
                {
                    if (combination == null || combination.Spell == null)
                        continue;

                    recipes.Add(new RecipeView
                    {
                        Id = book.HeroId + "/" + combination.name,
                        Name = combination.Spell.Name,
                        Icon = combination.Spell.Icon,
                        Chain = ToElementViews(combination.Elements),
                    });
                }
            }

            return recipes.ToArray();
        }

        private static ElementView[] ToElementViews(Element[] elements)
        {
            if (elements == null)
                return System.Array.Empty<ElementView>();

            ElementView[] views = new ElementView[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                views[i] = elements[i] == null
                    ? new ElementView()
                    : new ElementView { Id = elements[i].ID, Color = elements[i].Color, Icon = elements[i].Texture };
            }

            return views;
        }

        /// <summary>
        /// Уникальные стихии колоды. Ровно та же логика, что в <c>Book.OnValidate</c>,
        /// и ровно то число, которое экран показывает игроку как «стихий в колбах».
        /// </summary>
        private static ElementView[] CollectElements(RecipeView[] recipes, bool onlyEquipped)
        {
            List<ElementView> unique = new();
            HashSet<int> seen = new();

            foreach (RecipeView recipe in recipes)
            {
                if (onlyEquipped && !recipe.Equipped)
                    continue;

                foreach (ElementView element in recipe.Chain)
                {
                    if (seen.Add(element.Id))
                        unique.Add(element);
                }
            }

            return unique.ToArray();
        }
    }
}
