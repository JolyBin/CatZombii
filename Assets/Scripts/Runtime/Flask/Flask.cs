using Core.Flask.Models;
using System;
using System.Collections.Generic;

namespace Core.Flask
{
    public class Flask: IAction
    {
        public event Action<Element> RepitsCommand;
        public bool IsPossiblePushElement => _stack.Count < _maxSize;
        public bool IsPossiblePopElement => _stack.Count > 0;

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

        public Element PopElement() => _stack.Pop();

        public void PushElement(Element element)
        {
            _stack.Push(element);
            CheckRepits();
        }

        public void UpdateFlask(Element[] startElements)
        {
            _stack.Clear();
            for (int i = 0; i < startElements.Length; i++)
            {
                _stack.Push(startElements[i]);
            }
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
            RepitsCommand.Invoke(firstElement);
        }

        public void ClearAction() => RepitsCommand = null;
    }
}
