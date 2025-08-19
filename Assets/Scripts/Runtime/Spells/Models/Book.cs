using Core.Flask.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Book", menuName = "Spells/Create Book")]
    public class Book : ScriptableObject
    {
        public Combination[] Combinations => _combinations.ToArray();
        public Element[] UniqElements => _uniqElements;

        [field: SerializeField] public string NameHero { get; private set; }
        [field: SerializeField] public string ClassHero { get; private set; }
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
