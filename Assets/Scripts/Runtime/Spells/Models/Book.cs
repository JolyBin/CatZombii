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

        [field: SerializeField] public string NameHero;
        [field: SerializeField] public string ClassHero;
        [field: SerializeField] public Sprite IconClass;
        [field: SerializeField] public Sprite HeroIcon;

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
