using Core.Flask.Models;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Flask
{
    public class ElementsGenerator
    {
        private readonly Element[] _elements;
        private readonly List<int> _elemntIndexes;
        private readonly int _maxElementPool;

        public ElementsGenerator(Element[] elements, int maxElementPool)
        {
            _elements = elements;
            _maxElementPool = Mathf.Max(1, maxElementPool);
            _elemntIndexes = new List<int>(_maxElementPool);

            RefillIndexes();
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
            if (_elemntIndexes.Count == 0)
                RefillIndexes();

            int rndIndex = Random.Range(0, _elemntIndexes.Count);
            int elementIndex = _elemntIndexes[rndIndex];
            if(exclusiveElement == _elements[elementIndex])
            {
                elementIndex = (elementIndex + 1) % _elements.Length;
            }
            _elemntIndexes.RemoveAt(rndIndex);
            return _elements[elementIndex];
        }

        /// <summary>
        /// Пул выдаётся без возврата, поэтому его надо перезаполнять при опустошении —
        /// иначе Random.Range(0, 0) и обращение к пустому списку.
        /// </summary>
        private void RefillIndexes()
        {
            _elemntIndexes.Clear();

            if (_elements == null || _elements.Length == 0)
                return;

            int j = 0;
            for (int i = 0; i < _maxElementPool; i++)
            {
                _elemntIndexes.Add(j++);
                if (j == _elements.Length)
                    j = 0;
            }
        }
    }
}
