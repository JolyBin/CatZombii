using Core.Flask.Models;
using System;
using System.Collections.Generic;

namespace Core.Flask
{
    public class Flask: IAction
    {
        public event Action<Element> RepitsCommand;

        /// <summary>
        /// Единственный источник истины о содержимом колбы.
        /// Поднимается после любой мутации — на него подписывается UIFlask.
        /// </summary>
        public event Action<IReadOnlyList<Element>> OnChanged;

        public bool IsPossiblePushElement => _stack.Count < _maxSize;
        public bool IsPossiblePopElement => _stack.Count > 0;
        public int Count => _stack.Count;
        public int MaxSize => _maxSize;

        private readonly Stack<Element> _stack;
        private readonly int _maxSize;
        private readonly int _repitNumber;

        public Flask(int maxSize, Element[] startElements)
        {
            _stack = new(startElements);
            _maxSize = maxSize;
            _repitNumber = maxSize;
        }

        public Flask(int maxSize, Element[] startElements, int repitNumber)
        {
            _stack = new(startElements);
            _maxSize = maxSize;
            _repitNumber = repitNumber;
        }

        /// <summary>
        /// Содержимое от дна к горлышку: индекс 0 — элемент, положенный первым.
        /// </summary>
        public IReadOnlyList<Element> GetElements()
        {
            Element[] result = _stack.ToArray();
            Array.Reverse(result);
            return result;
        }

        public Element PopElement()
        {
            Element element = _stack.Pop();
            NotifyChanged();
            return element;
        }

        public void PushElement(Element element)
        {
            _stack.Push(element);
            NotifyChanged();
            CheckRepits();
        }

        public void UpdateFlask(Element[] startElements)
        {
            _stack.Clear();
            for (int i = 0; i < startElements.Length; i++)
            {
                _stack.Push(startElements[i]);
            }
            NotifyChanged();
        }

        private void CheckRepits()
        {
            if (_stack.Count < _repitNumber)
                return;
            Element firstElement = _stack.Peek();
            foreach (Element element in _stack)
            {
                if (element.ID != firstElement.ID)
                    return;
            }
            _stack.Clear();
            NotifyChanged();
            RepitsCommand?.Invoke(firstElement);
        }

        private void NotifyChanged() => OnChanged?.Invoke(GetElements());

        public void ClearAction()
        {
            RepitsCommand = null;
            OnChanged = null;
        }
    }
}
