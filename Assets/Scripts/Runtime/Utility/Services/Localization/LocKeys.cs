namespace Utility.Services.Localization
{
    /// <summary>
    /// Ключи, которые запрашивает КОД. Только они — ключи, живущие в сцене
    /// (в <see cref="LocalizedText"/>) и в ассетах (имена заклинаний, юнитов, героев),
    /// сюда не дублируются: у них уже есть единственное место хранения, а второе
    /// разъедется с первым.
    ///
    /// Зачем константы, а не строки по месту: опечатка в ключе — это не ошибка
    /// компиляции, а маркер <c>#battle.wav#</c> в готовой игре. Здесь опечатка ловится
    /// компилятором, а «где используется этот ключ» — обычным «Find Usages».
    /// </summary>
    public static class LocKeys
    {
        /// <summary>Главный экран: «Уровень {0}».</summary>
        public const string HomeLevel = "home.level";

        /// <summary>Окно боя: «Волна: {0}/{1}».</summary>
        public const string BattleWave = "battle.wave";

        /// <summary>Котёл: варка не дала заклинания. Ветка штатная и частая (docs/10 §6).</summary>
        public const string TableBrewFailed = "table.brew_failed";

        #region Мета: карта узлов, колода, лавка (docs/10 §13)

        /// <summary>Карта: подпись узла — «Узел {0}».</summary>
        public const string MapNode = "map.node";

        /// <summary>Карта: подпись узла-босса — «Босс {0}».</summary>
        public const string MapNodeBoss = "map.node_boss";

        /// <summary>Карта: «Пройдено {0} из {1}».</summary>
        public const string MapProgress = "map.progress";

        /// <summary>
        /// Карта: ОТКАЗ на клик по узлу. Запертый узел не кликается вовсе
        /// (<c>UIMapNode</c> гасит кнопку), поэтому строка редкая — но без неё
        /// единственная альтернатива это молчание, то есть «игра сломалась».
        /// </summary>
        public const string MapErrorNodeLocked = "map.error_node_locked";

        /// <summary>
        /// Колода: ГЛАВНАЯ надпись экрана — «Стихий в колбах: {0}».
        /// Закон docs/10 §13.1 держится на том, что игрок это видит.
        /// </summary>
        public const string DeckElementsCount = "deck.elements_count";

        /// <summary>Колода: «Слоты: {0} из {1}».</summary>
        public const string DeckSlots = "deck.slots";

        /// <summary>Колода: подпись пустого слота.</summary>
        public const string DeckSlotEmpty = "deck.slot_empty";

        /// <summary>Колода: подпись закрытого слота — «Слот за {0}».</summary>
        public const string DeckSlotLocked = "deck.slot_locked";

        /// <summary>Колода: рецепт уже в колоде.</summary>
        public const string DeckEquipped = "deck.equipped";

        /// <summary>Колода: рецепт можно надеть.</summary>
        public const string DeckEquip = "deck.equip";

        /// <summary>Лавка: предложение «Слот колоды №{0}».</summary>
        public const string ShopSlotOffer = "shop.slot_offer";

        /// <summary>Лавка: рецепт уже куплен.</summary>
        public const string ShopOwned = "shop.owned";

        /// <summary>Лавка: не хватает валюты.</summary>
        public const string ShopNotEnough = "shop.not_enough";

        /// <summary>
        /// Лавка: ЧЕСТНОЕ ПРЕДУПРЕЖДЕНИЕ при покупке рецепта с новой стихией
        /// (docs/10 §13.3) — «Стихий в колбах станет {0} вместо {1}».
        /// Формулировка нарочно без согласования числительного: она обязана быть
        /// верной и для 4, и для 5, и для 7.
        /// </summary>
        public const string ShopConfirmNewElement = "shop.confirm_new_element";

        /// <summary>Лавка: подпись «рецепт новой стихии не приносит».</summary>
        public const string ShopConfirmSameElements = "shop.confirm_same_elements";

        #endregion

        #region Мета: ОТКАЗЫ колоды (docs/10 §13.2)

        // Формулировка отказа живёт рядом с ключом, а сам отказ — рядом с законом,
        // который его порождает (Meta.HeroLoadout.Describe). Иначе экран однажды начнёт
        // объяснять игроку запрет, которого уже нет.

        /// <summary>Отказ: рецепт уже в колоде.</summary>
        public const string DeckErrorAlreadyEquipped = "deck.error_already_equipped";

        /// <summary>Отказ: свободных слотов нет.</summary>
        public const string DeckErrorNoSlot = "deck.error_no_slot";

        /// <summary>
        /// Отказ: сначала нужны префиксы — «{0}». Главный закон колоды: глубокий рецепт
        /// нельзя взять без его префиксов, и каждый из них занимает свой слот.
        /// </summary>
        public const string DeckErrorNeedsPrefix = "deck.error_needs_prefix";

        /// <summary>Отказ: рецепт ещё не куплен.</summary>
        public const string DeckErrorNotOwned = "deck.error_not_owned";

        /// <summary>Отказ: книга такого рецепта не знает (битый сейв или вырезанный рецепт).</summary>
        public const string DeckErrorUnknownRecipe = "deck.error_unknown_recipe";

        /// <summary>Отказ на снятие: рецепта нет в колоде.</summary>
        public const string DeckErrorNotEquipped = "deck.error_not_equipped";

        /// <summary>Отказ на снятие: на нём держатся другие рецепты — «{0}».</summary>
        public const string DeckErrorRequiredBy = "deck.error_required_by";

        /// <summary>
        /// Отказ на снятие: последний рецепт из колоды не убрать. Не вкусовщина —
        /// пустая колода означает пустой пул стихий, то есть падение генератора шариков.
        /// </summary>
        public const string DeckErrorLastRecipe = "deck.error_last_recipe";

        #endregion
    }
}
