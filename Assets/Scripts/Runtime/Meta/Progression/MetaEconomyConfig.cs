using UnityEngine;
using Utility.Services.Saves;

namespace Meta
{
    /// <summary>
    /// ЭКОНОМИКА МЕТЫ КАК АССЕТ — цены лавки и награды за узлы (docs/10 §14.4, §15.1, §15.3).
    ///
    /// ═══ ЗАЧЕМ ЭТОТ ФАЙЛ СУЩЕСТВУЕТ ═══
    ///
    /// До него те же числа лежали константами в <see cref="MetaEconomy"/>, и автор честно
    /// пометил это «временным жильём». Причина переезда одна и она не про красоту кода:
    /// <b>баланс — работа геймдизайнера, а не программиста</b>. Пока лестница цен живёт
    /// в <c>.cs</c>, каждая правка «а если слот будет стоить 200» стоит пересборки и чужого
    /// времени; в ассете она стоит один клик в инспекторе.
    ///
    /// Так же устроены ВСЕ конфиги проекта — <c>BattleConfig</c>, <c>UnitConfig</c>,
    /// <c>Book</c>, <c>LocalizationTable</c>. Новая сущность не заводит второго способа
    /// хранить данные.
    ///
    /// ═══ ГДЕ ЛЕЖИТ И КАК ПРАВИТЬ ═══
    ///
    /// <c>Assets/Resources/Meta/Meta Economy.asset</c>. Выделить в Project, править
    /// в инспекторе, сохранить проект. Перезапуск игры подхватит новые числа —
    /// пересборка не нужна.
    ///
    /// ═══ ЧИСЛА ЗДЕСЬ — НЕ ВЫДУМАННЫЕ ═══
    ///
    /// Инициализаторы полей — дословная транскрипция опубликованных таблиц docs/10.
    /// Они же становятся значениями по умолчанию для нового ассета, и они же —
    /// аварийный фолбэк, если ассет потеряли. Единственное место, где эти числа
    /// написаны, — прямо здесь; сам <c>.asset</c> их переопределяет.
    ///
    /// ⚠️ Баланс сознательно ОТЛОЖЕН владельцем до замера мастер-константы (docs/10 §13.6).
    /// Числа переехали как есть; крутить их — отдельная работа, у которой есть хозяин.
    /// </summary>
    [CreateAssetMenu(fileName = "Meta Economy", menuName = "Meta/Экономика меты")]
    public class MetaEconomyConfig : ScriptableObject
    {
        /// <summary>Путь внутри <c>Resources/</c>. Меняешь путь — меняй здесь, второго места нет.</summary>
        public const string RESOURCES_PATH = "Meta/Meta Economy";

        [Header("Лавка: слоты колоды (docs/10 §14.4)")]
        [Tooltip("Цены слотов, начиная с ПЯТОГО. Длина списка = сколько слотов вообще продаётся; " +
                 "по §14.4 их четыре (5-й…8-й), и лестница растёт: 150 / 250 / 400 / 600.")]
        [SerializeField] private int[] _slotPrices = { 150, 250, 400, 600 };

        [Header("Лавка: рецепты (docs/10 §15.1)")]
        [Tooltip("Цена рецепта по его ДЛИНЕ: индекс = длина цепочки стихий. " +
                 "Индекс 0 не используется, индекс 1 — рецепты длины 1 (они бесплатны, выдаются " +
                 "вместе с героем), индекс 2 — длина 2, индекс 3 — длина 3. Рецепт длиннее списка " +
                 "берёт последнюю цену. По §15.1: длина 2 ≈ 100, длина 3 ≈ 250.")]
        [SerializeField] private int[] _recipePriceByLength = { 0, 0, 100, 250 };

        [Header("Награды за узлы (docs/10 §15.3)")]
        [Tooltip("Награда за ПЕРВОЕ прохождение узла, по главам: 60 / 100 / 150. " +
                 "Глав три (docs/10 §13.4), поэтому и чисел три.")]
        [SerializeField] private int[] _nodeRewardByChapter = { 60, 100, 150 };

        [Tooltip("Добавка за босса главы, сверх обычной награды: +100 / +200 / +300. " +
                 "Босс — последний узел главы.")]
        [SerializeField] private int[] _bossBonusByChapter = { 100, 200, 300 };

        [Tooltip("Доля первой награды за ПЕРЕПРОХОЖДЕНИЕ узла (§15.3: «40%»). " +
                 "Это содержание недели после кампании: ферма третьей главы — 60 клубков за бой.")]
        [Range(0f, 1f)]
        [SerializeField] private float _replayShare = 0.4f;

        /// <summary>
        /// Сколько слотов продаётся. Ограничено потолком профиля: продать девятый слот
        /// нельзя, даже если геймдизайнер дописал в лестницу пятую цену, — <c>PlayerProfile</c>
        /// всё равно обрежет покупку, и предложение висело бы в лавке вечно недоступным.
        /// Расхождение не проглатывается молча, о нём говорит <see cref="OnValidate"/>.
        /// </summary>
        public int SellableSlots
        {
            get
            {
                int sellableByProfile = PlayerProfile.MAX_DECK_SLOTS - PlayerProfile.BASE_DECK_SLOTS;
                int ladder = _slotPrices == null ? 0 : _slotPrices.Length;
                return Mathf.Min(ladder, sellableByProfile);
            }
        }

        /// <summary>
        /// Цена следующего слота. <paramref name="step"/> — какой по счёту слот покупается,
        /// считая с единицы (1 — пятый, 4 — восьмой); ровно то, что отдаёт
        /// <c>MetaController.NextSlotStep</c>. Ноль — покупать нечего.
        /// </summary>
        public int SlotPrice(int step)
        {
            if (_slotPrices == null || step < 1 || step > _slotPrices.Length)
                return 0;
            return Mathf.Max(0, _slotPrices[step - 1]);
        }

        /// <summary>
        /// ЦЕНА РЕЦЕПТА ПО ДЛИНЕ. Длина — единственное свойство рецепта, которое видно
        /// и коду, и игроку, и она же его настоящая цена (слоты в колоде и такты в котле).
        /// Рецепты длины 1 не продаются никогда — они выдаются вместе с героем (§14.4).
        /// </summary>
        public int RecipePriceForLength(int length)
        {
            if (_recipePriceByLength == null || _recipePriceByLength.Length == 0 || length <= 0)
                return 0;

            int index = Mathf.Min(length, _recipePriceByLength.Length - 1);
            return Mathf.Max(0, _recipePriceByLength[index]);
        }

        /// <summary>
        /// НАГРАДА ЗА УЗЕЛ. <paramref name="chapter"/> считается с единицы;
        /// <paramref name="firstClear"/> различает первое прохождение и ферму (§15.3).
        /// </summary>
        public int NodeReward(int chapter, bool isBoss, bool firstClear)
        {
            int reward = ByChapter(_nodeRewardByChapter, chapter);
            if (isBoss)
                reward += ByChapter(_bossBonusByChapter, chapter);

            if (firstClear)
                return reward;

            // Перепрохождение платит долю ПЕРВОЙ награды, включая бонус за босса:
            // босс остаётся самым выгодным боем и в ферме, что и делает третью главу
            // содержанием недели после кампании.
            return Mathf.Max(1, Mathf.RoundToInt(reward * _replayShare));
        }

        /// <summary>
        /// Глава за пределами списка берёт крайнее значение, а не ноль: карта может
        /// вырасти раньше, чем геймдизайнер допишет столбец, и «бой перестал платить»
        /// — худший из возможных способов об этом узнать.
        /// </summary>
        private static int ByChapter(int[] values, int chapter)
        {
            if (values == null || values.Length == 0)
                return 0;

            int index = Mathf.Clamp(chapter - 1, 0, values.Length - 1);
            return Mathf.Max(0, values[index]);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Проверки, которые ловят молчаливые расхождения прямо в инспекторе.
        /// Ни одна из них не «исправляет» число за геймдизайнера: правка баланса —
        /// его решение, дело кода — не дать ей разойтись с остальным проектом.
        /// </summary>
        private void OnValidate()
        {
            int sellableByProfile = PlayerProfile.MAX_DECK_SLOTS - PlayerProfile.BASE_DECK_SLOTS;
            int ladder = _slotPrices == null ? 0 : _slotPrices.Length;

            if (ladder != sellableByProfile)
            {
                Debug.LogWarning(
                    $"[Экономика] В лестнице цен слотов {ladder} ступеней, а профиль позволяет купить " +
                    $"{sellableByProfile} (PlayerProfile.BASE_DECK_SLOTS={PlayerProfile.BASE_DECK_SLOTS}, " +
                    $"MAX_DECK_SLOTS={PlayerProfile.MAX_DECK_SLOTS}). Лишние ступени не продадутся, " +
                    "недостающие оставят слот без цены. Меняешь число слотов — меняй и потолок в PlayerProfile.",
                    this);
            }

            if (_nodeRewardByChapter == null || _nodeRewardByChapter.Length < MapProgress.DESIGN_CHAPTERS)
            {
                Debug.LogWarning(
                    $"[Экономика] Наград за узел меньше, чем глав на карте ({MapProgress.DESIGN_CHAPTERS}). " +
                    "Главы без своего числа возьмут последнее из списка.", this);
            }

            if (_bossBonusByChapter == null || _bossBonusByChapter.Length < MapProgress.DESIGN_CHAPTERS)
            {
                Debug.LogWarning(
                    $"[Экономика] Бонусов за босса меньше, чем глав на карте ({MapProgress.DESIGN_CHAPTERS}). " +
                    "Главы без своего числа возьмут последнее из списка.", this);
            }
        }
#endif
    }
}
