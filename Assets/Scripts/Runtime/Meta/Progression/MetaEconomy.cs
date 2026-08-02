using Core.Spells;

namespace Meta
{
    /// <summary>
    /// ЦИФРЫ ЭКОНОМИКИ, ВЫПИСАННЫЕ ИЗ docs/10. Ни одна из них здесь не придумана —
    /// это транскрипция таблиц §14.4, §15.1 и §15.3, собранная в одно место, чтобы
    /// геймдизайнер правил её в одном файле, а не искал по коду.
    ///
    /// ⚠️ ЭТОТ ФАЙЛ — ВРЕМЕННОЕ ЖИЛЬЁ, И ЭТО НАДО ЗНАТЬ.
    ///
    /// Цены и награды — это КОНТЕНТ, а контенту в проекте положено лежать в
    /// <c>ScriptableObject</c> в <c>Resources/</c>, как лежат <c>BattleConfig</c>,
    /// <c>UnitConfig</c> и <c>Book</c>. Сюда они попали только потому, что без них
    /// экономика не замыкается вообще: нет наград — нет клубков, нет клубков — лавка
    /// мертва, а вместе с ней и весь закон «каждое новое заклинание делает пазл труднее».
    /// Заводить ассет за геймдизайнера — значит принять за него решения о балансе;
    /// выписать его же опубликованные числа — не значит.
    ///
    /// Когда ассет появится: законы меты (<see cref="MetaController"/>, <see cref="HeroLoadout"/>)
    /// цен НЕ ЗНАЮТ — они принимают цену параметром. Поменять источник — значит поменять
    /// вызовы в <see cref="MetaScreensBinding"/>, и больше нигде.
    ///
    /// ═══ ОТКУДА КАЖДОЕ ЧИСЛО ═══
    ///
    /// §14.4 — слоты колоды: 4 на старте, 8 максимум, цены 150 / 250 / 400 / 600.
    /// §15.1 — цены рецептов: «4 рецепта длины 2 × ~100», «3 рецепта длины 3 × ~250».
    ///         Это те же числа, которыми документ считает всю кампанию (4850 полная
    ///         коллекция, ~2150 обязательный минимум), поэтому менять их поодиночке
    ///         нельзя — поедет расчёт числа узлов.
    /// §15.3 — награды: 60 / 100 / 150 за узел по главам, +100 / +200 / +300 за босса,
    ///         перепрохождение платит 40% от первой награды.
    /// </summary>
    public static class MetaEconomy
    {
        /// <summary>
        /// Цены слотов 5-го … 8-го (docs/10 §14.4). Длина лестницы и есть потолок слотов:
        /// <c>PlayerProfile.MAX_DECK_SLOTS = BASE_DECK_SLOTS + SLOT_PRICES.Length</c>,
        /// и это соотношение проверяется в <see cref="SlotPrice"/>.
        /// </summary>
        private static readonly int[] SLOT_PRICES = { 150, 250, 400, 600 };

        /// <summary>Награда за первое прохождение узла, по главам (docs/10 §15.3).</summary>
        private static readonly int[] NODE_REWARD_BY_CHAPTER = { 60, 100, 150 };

        /// <summary>Бонус за босса главы (docs/10 §15.3).</summary>
        private static readonly int[] BOSS_BONUS_BY_CHAPTER = { 100, 200, 300 };

        /// <summary>
        /// Доля первой награды за перепрохождение узла (docs/10 §15.3: «40%»).
        /// Ферма третьей главы — 60 клубков за бой, то есть ~19 боёв на полную
        /// третью книгу; это и есть содержание после кампании.
        /// </summary>
        private const float REPLAY_SHARE = 0.4f;

        /// <summary>
        /// ЦЕНА РЕЦЕПТА. Считается от ДЛИНЫ, а не от силы: длина — единственное свойство
        /// рецепта, которое видно и коду, и игроку, и она же его настоящая цена
        /// (слоты в колоде и такты в котле). Рецепты длины 1 не продаются никогда —
        /// они выдаются вместе с героем (§14.4).
        /// </summary>
        public static int RecipePrice(string recipeId)
        {
            switch (RecipeId.Length(recipeId))
            {
                case 0:
                case 1:
                    return 0;
                case 2:
                    return 100;
                default:
                    return 250;
            }
        }

        /// <summary>
        /// Цена следующего слота. <paramref name="step"/> — какой по счёту слот покупается,
        /// считая с единицы (1 — пятый, 4 — восьмой); ровно то, что отдаёт
        /// <see cref="MetaController.NextSlotStep"/>. Ноль — покупать нечего.
        /// </summary>
        public static int SlotPrice(int step)
        {
            if (step < 1 || step > SLOT_PRICES.Length)
                return 0;
            return SLOT_PRICES[step - 1];
        }

        /// <summary>Сколько слотов вообще продаётся. Потолок должен сходиться с длиной лестницы.</summary>
        public static int SellableSlots => SLOT_PRICES.Length;

        /// <summary>
        /// НАГРАДА ЗА УЗЕЛ. <paramref name="chapter"/> считается с единицы;
        /// <paramref name="firstClear"/> различает первое прохождение и ферму (§15.3).
        /// </summary>
        public static int NodeReward(int chapter, bool isBoss, bool firstClear)
        {
            int index = chapter - 1;
            if (index < 0)
                index = 0;
            if (index >= NODE_REWARD_BY_CHAPTER.Length)
                index = NODE_REWARD_BY_CHAPTER.Length - 1;

            int reward = NODE_REWARD_BY_CHAPTER[index];
            if (isBoss)
                reward += BOSS_BONUS_BY_CHAPTER[index];

            if (firstClear)
                return reward;

            // Перепрохождение платит долю ПЕРВОЙ награды, включая бонус за босса:
            // босс остаётся самым выгодным боем и в ферме, что и делает третью главу
            // содержанием недели после кампании.
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(reward * REPLAY_SHARE));
        }
    }
}
