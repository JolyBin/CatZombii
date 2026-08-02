using Core.Flask.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility.Services.Localization;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Book", menuName = "Spells/Create Book")]
    public class Book : ScriptableObject
    {
        public Combination[] Combinations => _combinations.ToArray();
        public Element[] UniqElements => _uniqElements;

        [Tooltip("ИДЕНТИФИКАТОР героя для сохранения. Пусто = берётся имя ассета. " +
                 "Заполняй, только если ассет собираются переименовывать.")]
        [SerializeField] private string _heroId;

        /// <summary>
        /// ЧЕМ ЭТОТ ГЕРОЙ ЗАПИСАН В СЕЙВЕ (<c>PlayerProfile.HeroId</c>).
        ///
        /// Не индекс в списке окна героев: индекс сломался бы от любой перестановки
        /// героев на сцене, а «герой пропал» в вебе неотличимо от «сейв слетел».
        /// Не ссылка на ассет: в JSON её не положить.
        ///
        /// По умолчанию — имя ассета: оно уже уникально в папке и не требует заводить
        /// поле в четырёх книгах руками. Цена честная: ПЕРЕИМЕНОВАНИЕ ассета книги
        /// сбросит выбор героя у всех, кто его выбрал (откатится на героя по умолчанию,
        /// прогресс уровней не пострадает). Кто собирается переименовывать — заполняет
        /// <c>_heroId</c> явно, и тогда имя ассета уже ни на что не влияет.
        /// </summary>
        public string HeroId => string.IsNullOrWhiteSpace(_heroId) ? name : _heroId;

        [Tooltip("КЛЮЧ имени героя. Соглашение: hero.<герой>.name.")]
        [SerializeField] private string _nameHeroKey;

        [Tooltip("КЛЮЧ класса героя. Соглашение: hero.<герой>.class.")]
        [SerializeField] private string _classHeroKey;

        /// <summary>Ключи — для редакторной проверки и миграций, не для игры.</summary>
        public string NameHeroKey => _nameHeroKey;
        public string ClassHeroKey => _classHeroKey;

        /// <summary>
        /// ИМЯ ГЕРОЯ. Тоже через ключ, хотя это имя собственное.
        ///
        /// Причина не в переводе, а в письменности: docs/09 называет целями фазы 3
        /// упрощённый китайский и японский, а имя собственное в них не остаётся
        /// латиницей — оно транслитерируется. Ключ это позволяет и стоит ноль;
        /// литерал в ассете пришлось бы выковыривать вместе со всей книгой.
        /// Русское значение сегодня — та же латиница, что и была: выдумывать герою
        /// новое имя не моё решение.
        /// </summary>
        public string NameHero => Localization.Get(_nameHeroKey);

        /// <summary>
        /// КЛАСС ГЕРОЯ. В отличие от имени — обычный игровой термин («Воин», «Маг»),
        /// и переводится он безусловно: «Warrior» в русской игре — это тот же дефект,
        /// что и «Wave: 1/3» в окне боя.
        /// </summary>
        public string ClassHero => Localization.Get(_classHeroKey);

        [field: SerializeField] public int HP { get; private set; } = 200;
        [field: SerializeField] public Sprite IconClass { get; private set; }
        [field: SerializeField] public Sprite HeroIcon { get; private set; }

        [SerializeField] private Combination[] _combinations;

        [SerializeField] private Element[] _uniqElements;

        [Tooltip("ГЛАВА, боссом которой открывается герой (docs/10 §13.4: боссы узлов 5 и 10). " +
                 "0 — герой доступен с самого начала. Валютой герои не покупаются никогда.")]
        [SerializeField] private int _unlockChapter;

        /// <summary>
        /// КОГДА ГЕРОЙ ОТКРЫВАЕТСЯ. docs/10 §13.4: героев трое, стартовый и два за прогресс,
        /// и «карта даёт идентичность, валюта даёт глубину» — поэтому анлок героя живёт
        /// в книге рядом с самим героем, а не в таблице расписания где-то ещё.
        ///
        /// Ноль по умолчанию выбран сознательно: пока геймдизайнер не расставил главы,
        /// все книги остаются доступны — ровно как сегодня. Мета не должна отбирать
        /// у проекта играбельность за то, что её расписание ещё не сверстано.
        /// </summary>
        public int UnlockChapter => Mathf.Max(0, _unlockChapter);

        /// <summary>
        /// Пересчёт пула стихий книги. САМ ВЫВОД ЖИВЁТ НЕ ЗДЕСЬ, а в
        /// <see cref="SpellDeck.CollectUniqElements"/>: тот же вывод нужен рантайму,
        /// который собирает колоду из подмножества рецептов (docs/10 §13.1), и две копии
        /// одного правила разъехались бы молча — «в колбах не те шарики» не выглядит
        /// как ошибка кода.
        /// </summary>
        private void OnValidate()
        {
            _uniqElements = SpellDeck.CollectUniqElements(_combinations);
        }

    }
}
