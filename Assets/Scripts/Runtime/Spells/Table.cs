using Core.Flask.Models;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Spells
{
    public class Table
    {
        private Dictionary<int, Chain> _allCombinations;

        public Table(Combination[] combinations)
        {
            GenerateCombinationsDict(combinations);
        }

        private void GenerateCombinationsDict(Combination[] combinations)
        {
            _allCombinations = new Dictionary<int, Chain> ();
            foreach (Combination combination in combinations)
            {
                Chain currentChain = new (0);
                for (int i = 0; i < combination.Elements.Length; i++)
                {
                    Element currentElement = combination.Elements[i];
                    if(i == 0)
                    {
                        if(!_allCombinations.TryGetValue(currentElement.ID, out currentChain))
                        {
                            currentChain = new (currentElement.ID);
                            _allCombinations.Add(currentElement.ID, currentChain);
                        }
                    }
                    else
                    {
                        if (!currentChain.Chains.TryGetValue(currentElement.ID, out currentChain))
                        {
                            Chain newChain = new Chain(currentElement.ID);
                            currentChain.Chains.Add(currentElement.ID, newChain);
                            currentChain = newChain;
                        }
                    }
                }
                currentChain.SetSpell(combination.Spell);
            }
        }

        public bool TryGetSpell(Element[] elements, out Spell spell)
        {
            spell = null;
            Chain currentChain = new(0);
            for (int i = 0; i < elements.Length; i++)
            {
                Element currentElement = elements[i];
                if (i == 0)
                {
                    if (!_allCombinations.TryGetValue(currentElement.ID, out currentChain))
                        return false;
                }
                else
                {
                    if (!currentChain.Chains.TryGetValue(currentElement.ID, out currentChain))
                        return false;
                }
            }
            spell = currentChain.Spell;
            return true;
        }
    }
}
