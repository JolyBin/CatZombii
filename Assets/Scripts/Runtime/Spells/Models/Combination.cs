using Core.Flask.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Combination", menuName = "Spells/Create Combination")]
    public class Combination : ScriptableObject
    {
        public Element[] Elements { get => _elements; }

        [field: SerializeField] public Spell Spell { get; }

        [SerializeField] private readonly  Element[] _elements;
    }
}
