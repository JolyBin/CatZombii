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

        private void OnValidate()
        {
            List<Element> uniqElements = new List<Element>();
            foreach(var combination in Combinations)
            {
                foreach (var element in combination.Elements)
                {
                    if(!uniqElements.Contains(element))
                        uniqElements.Add(element);
                }
            }
            _uniqElements = uniqElements.ToArray();
        }

    }
}
