using System;
using UnityEngine;

namespace Meta.Models
{
    /// <summary>
    /// МОДЕЛИ ТРЁХ ЭКРАНОВ МЕТЫ — карта узлов, колода, лавка (docs/10 §13).
    ///
    /// Зачем отдельный слой, если данные уже есть в профиле и в книгах. Затем, что
    /// экран не имеет права знать, ОТКУДА пришло число: сегодня узлы берутся из
    /// <c>BattleConfig[]</c> и <c>Saves.Profile.LevelIndex</c>, завтра — из карты
    /// с ветвлением, и переписывать вёрстку под это второй раз никто не будет.
    /// Здесь лежит ровно то, что рисуется, и ничего больше: ни ссылок на ассеты
    /// конфигов, ни ключей локализации, ни цен, которые экран не показывает.
    ///
    /// ⛔ ВСЕ строки в этих моделях — УЖЕ ЛОКАЛИЗОВАННЫЕ значения
    /// (<c>Localization.Get(...)</c> зовёт тот, кто собирает модель), потому что
    /// имя заклинания живёт ключом в ассете (<c>BaseSpellConfig.NameKey</c>),
    /// а не в <see cref="Utility.Services.Localization.LocKeys"/>. Класть сюда ключ
    /// значило бы завести второе место, где решают, каким ключом зовётся заклинание.
    ///
    /// Все поля — публичные поля, а не свойства: это DTO, который собирают снаружи
    /// объектным инициализатором, и лишняя церемония здесь ничего не защищает.
    /// </summary>
    public enum MapNodeState
    {
        /// <summary>Узел ещё не открыт прогрессом. Виден, но не играбелен.</summary>
        Locked = 0,

        /// <summary>Открыт и не пройден — следующая цель.</summary>
        Open = 1,

        /// <summary>Пройден. Узлы ПЕРЕПРОХОДИМЫ (docs/10 §13.4), значит остаётся играбельным.</summary>
        Cleared = 2,
    }

    public sealed class MapNodeView
    {
        /// <summary>Номер узла для игрока, 1-based. Он же приходит в событие клика.</summary>
        public int Number;

        public MapNodeState State;

        /// <summary>Босс (узлы 5, 10, 15 — docs/10 §13.4). Рисуется другим силуэтом, не цветом.</summary>
        public bool IsBoss;

        /// <summary>Награда в клубках. 0 — не показывать.</summary>
        public int Reward;
    }

    public sealed class MapScreenModel
    {
        /// <summary>
        /// Узлы. Длина берётся ОТСЮДА, а не из константы 15: уровней в сборке сегодня
        /// семь, и экран обязан честно показать это, а не соврать про пятнадцать.
        /// </summary>
        public MapNodeView[] Nodes = Array.Empty<MapNodeView>();

        public int Yarn;
    }

    /// <summary>Стихия так, как её видит экран: цвет И силуэт (docs/12 §4.3 — цвет один запрещён).</summary>
    public sealed class ElementView
    {
        public int Id;
        public Color Color = Color.white;

        /// <summary>
        /// <c>Element.Texture</c>. Может быть <c>null</c> — тогда карточка оставляет
        /// свой запасной силуэт, а не рисует пустоту.
        /// </summary>
        public Sprite Icon;
    }

    public sealed class RecipeView
    {
        /// <summary>
        /// Стабильный идентификатор рецепта — им экран отвечает наружу («надеть этот»).
        /// Не индекс: список рецептов перестраивается на каждом открытии окна.
        /// </summary>
        public string Id;

        /// <summary>УЖЕ локализованное имя заклинания (<c>BaseSpellConfig.Name</c>).</summary>
        public string Name;

        public Sprite Icon;

        /// <summary>Цепочка стихий рецепта. Длина 1–3 (docs/10 §14).</summary>
        public ElementView[] Chain = Array.Empty<ElementView>();

        public bool Equipped;
    }

    public enum DeckSlotState
    {
        /// <summary>В слоте лежит рецепт.</summary>
        Filled = 0,

        /// <summary>Слот куплен и пуст.</summary>
        Empty = 1,

        /// <summary>Слот ещё не куплен (5-й … 8-й, docs/10 §14.4).</summary>
        Locked = 2,
    }

    public sealed class DeckSlotView
    {
        /// <summary>0-based позиция слота. Приходит в событие клика.</summary>
        public int Index;

        public DeckSlotState State;

        /// <summary><c>null</c>, если слот пуст или закрыт.</summary>
        public RecipeView Recipe;

        /// <summary>Цена открытия слота, если <see cref="DeckSlotState.Locked"/>.</summary>
        public int UnlockPrice;
    }

    public sealed class DeckScreenModel
    {
        /// <summary>Все слоты — и купленные, и закрытые. Закрытые показываются намеренно: это витрина.</summary>
        public DeckSlotView[] Slots = Array.Empty<DeckSlotView>();

        /// <summary>Купленные рецепты героя — то, что можно надеть.</summary>
        public RecipeView[] Collection = Array.Empty<RecipeView>();

        /// <summary>
        /// ГЛАВНОЕ ЧИСЛО ЭКРАНА (docs/10 §13.1). Стихии ЭКИПИРОВАННОЙ колоды,
        /// а не купленной коллекции: именно они сыплются в колбы, и именно поэтому
        /// «взял ещё рецепт» = «пазл стал труднее».
        /// </summary>
        public ElementView[] Elements = Array.Empty<ElementView>();

        public int SlotsUnlocked;

        public int SlotsMax = 8;

        public int Yarn;
    }

    public enum ShopOfferKind
    {
        Recipe = 0,
        DeckSlot = 1,
    }

    public sealed class ShopOfferView
    {
        /// <summary>Стабильный идентификатор предложения — им экран отвечает наружу.</summary>
        public string Id;

        public ShopOfferKind Kind;

        /// <summary>УЖЕ локализованное имя. Для слота — соберёт сам экран, здесь можно оставить пустым.</summary>
        public string Name;

        public Sprite Icon;

        public int Price;

        public bool Owned;

        /// <summary>Хватает ли клубков. Считает слой данных: экран не знает про кошелёк.</summary>
        public bool Affordable;

        /// <summary>
        /// Рецепт приносит СТИХИЮ, которой у игрока ещё нет (docs/10 §13.3).
        /// Ради этого флага и существует окно подтверждения.
        /// </summary>
        public bool BringsNewElement;

        public int ElementsBefore;

        public int ElementsAfter;

        /// <summary>Номер слота для игрока, 1-based. Только для <see cref="ShopOfferKind.DeckSlot"/>.</summary>
        public int SlotNumber;
    }

    public sealed class ShopScreenModel
    {
        public ShopOfferView[] Offers = Array.Empty<ShopOfferView>();

        public int Yarn;
    }
}
