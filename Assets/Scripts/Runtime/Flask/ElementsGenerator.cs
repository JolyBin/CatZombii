using Core.Flask.Models;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Flask
{
    public class ElementsGenerator
    {
        private readonly Element[] _elements;
        private readonly List<int> _elemntIndexes;

        public ElementsGenerator(Element[] elements, int maxElementPool)
        {
            _elements = elements;
            _elemntIndexes = new List<int>();

            int j = 0;
            for (int i = 0; i < maxElementPool; i++)
            {
                _elemntIndexes.Add(j++);
                if (j == _elements.Length)
                    j = 0;
            }
        }

        public Element[] GetElements(int size, int maxSize)
        {
            Element[] resultsArray = new Element[size];

            int repitCount = 1;
            for (int i = 0; i < resultsArray.Length; i++)
            {
                if (repitCount != maxSize - 1)
                    resultsArray[i] = GetRandomElement();
                else
                    resultsArray[i] = GetRandomElement(resultsArray[i - 1]);
                if (i > 0 && resultsArray[i] == resultsArray[i - 1])
                    repitCount++;
            }
            return resultsArray;
        }

        private Element GetRandomElement(Element exclusiveElement = null)
        {
            int rndIndex = Random.Range(0, _elemntIndexes.Count);
            int elementIndex = _elemntIndexes[rndIndex];
            if(exclusiveElement == _elements[elementIndex])
            {
                elementIndex = (elementIndex + 1) % _elements.Length;
            }
            _elemntIndexes.RemoveAt(rndIndex);
            return _elements[elementIndex];
        }
    }
}
