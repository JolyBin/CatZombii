using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Book", menuName = "Spells/Create Book")]
    public class Book : ScriptableObject
    {
        public Combination[] Combination { get => _combinations.ToArray(); }

        [SerializeField] private Combination[] _combinations;

    }
}
