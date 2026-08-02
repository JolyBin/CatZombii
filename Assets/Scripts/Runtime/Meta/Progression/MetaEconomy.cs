using Core.Spells;
using UnityEngine;

namespace Meta
{
    /// <summary>
    /// ВХОД К ЦИФРАМ ЭКОНОМИКИ. Сами цифры живут в ассете
    /// <see cref="MetaEconomyConfig"/> (<c>Assets/Resources/Meta/Meta Economy.asset</c>) —
    /// здесь только доступ к нему и перевод «рецепт → длина → цена».
    ///
    /// ═══ ПОЧЕМУ ОСТАЛСЯ СТАТИЧЕСКИЙ ФАСАД, А НЕ ССЫЛКА НА АССЕТ ВЕЗДЕ ═══
    ///
    /// Цену спрашивают из двух мест (<see cref="MetaScreensBinding"/> и
    /// <see cref="HomeController.RegisterNodeCleared"/>), и обоим она нужна ровно на одну
    /// строку. Протаскивать ссылку на конфиг через конструкторы <c>HomeController</c> →
    /// <c>MetaController</c> → биндинг значило бы четыре новых параметра ради двух вызовов.
    /// Так же в проекте уже устроены <c>BookCatalog</c> и <c>Localization</c>: ассет один
    /// на игру, грузится лениво из <c>Resources</c>, и <c>Resources.Load</c> работает
    /// на WebGL без Addressables.
    ///
    /// ═══ ЗАКОНЫ ЦЕН ЗДЕСЬ НЕ ЖИВУТ ═══
    ///
    /// <c>MetaController.TryBuyRecipe</c> и <c>TryBuySlot</c> по-прежнему принимают цену
    /// ПАРАМЕТРОМ и про этот класс не знают. Это не случайность: законы меты (что можно
    /// купить, что можно надеть) обязаны быть проверяемы без ассетов, а цена — это баланс,
    /// который меняется без единой правки кода.
    /// </summary>
    public static class MetaEconomy
    {
        private static MetaEconomyConfig _config;

        /// <summary>
        /// Конфиг. Лениво грузится из <c>Resources</c> и кэшируется на сессию.
        ///
        /// Потерянный ассет — НЕ повод остаться без экономики: без наград нет клубков,
        /// без клубков мертва лавка, а с ней и весь закон «каждое новое заклинание делает
        /// пазл труднее». Поэтому фолбэк — экземпляр в памяти со значениями по умолчанию
        /// (они же — опубликованные числа docs/10), и громкая ошибка в консоль.
        /// </summary>
        public static MetaEconomyConfig Config => _config != null ? _config : _config = Load();

        private static MetaEconomyConfig Load()
        {
            MetaEconomyConfig loaded = Resources.Load<MetaEconomyConfig>(MetaEconomyConfig.RESOURCES_PATH);
            if (loaded != null)
                return loaded;

            Debug.LogError($"[Экономика] Нет ассета «Resources/{MetaEconomyConfig.RESOURCES_PATH}». " +
                           "Играем на значениях по умолчанию из MetaEconomyConfig — они верны по docs/10, " +
                           "но правки геймдизайнера в них не попадут. Создать: " +
                           "Assets/Resources/Meta → Create → Meta → Экономика меты.");

            return ScriptableObject.CreateInstance<MetaEconomyConfig>();
        }

        /// <summary>
        /// ЦЕНА РЕЦЕПТА. Считается от ДЛИНЫ цепочки стихий: длина — единственное свойство
        /// рецепта, которое видно и коду, и игроку, и она же его настоящая цена
        /// (слоты в колоде и такты в котле). Рецепты длины 1 не продаются никогда —
        /// они выдаются вместе с героем (docs/10 §14.4).
        /// </summary>
        public static int RecipePrice(string recipeId) => Config.RecipePriceForLength(RecipeId.Length(recipeId));

        /// <summary>
        /// Цена следующего слота. <paramref name="step"/> — какой по счёту слот покупается,
        /// считая с единицы (1 — пятый, 4 — восьмой); ровно то, что отдаёт
        /// <see cref="MetaController.NextSlotStep"/>. Ноль — покупать нечего.
        /// </summary>
        public static int SlotPrice(int step) => Config.SlotPrice(step);

        /// <summary>Сколько слотов вообще продаётся.</summary>
        public static int SellableSlots => Config.SellableSlots;

        /// <summary>
        /// НАГРАДА ЗА УЗЕЛ. <paramref name="chapter"/> считается с единицы;
        /// <paramref name="firstClear"/> различает первое прохождение и ферму (docs/10 §15.3).
        /// </summary>
        public static int NodeReward(int chapter, bool isBoss, bool firstClear)
            => Config.NodeReward(chapter, isBoss, firstClear);
    }
}
