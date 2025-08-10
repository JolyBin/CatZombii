using Core.Flask.Models;
using System;
using System.Collections.Generic;

namespace Core.Flask
{
    public class Flask: IAction
    {
        public event Action<BaseElement> RepitsCommand;
        public bool IsPossiblePushElement => _stack.Count < _maxSize;
        public bool IsPossiblePopElement => _stack.Count > 0;

        private readonly Stack<BaseElement> _stack;
        private readonly int _maxSize;
        private readonly int _repitNumber;

        public Flask(int maxSize, BaseElement[] startElements)
        {
            _stack = new(startElements);
            _maxSize = maxSize;
            _repitNumber = maxSize;
        }

        public Flask(int maxSize, BaseElement[] startElements, int repitNumber)
        {
            _stack = new(startElements);
            _maxSize = maxSize;
            _repitNumber = repitNumber;
        }

        public BaseElement PopElement() => _stack.Pop();

        public void PushElement(BaseElement element)
        {
            _stack.Push(element);
            CheckRepits();
        }

        public void UpdateFlask(BaseElement[] startElements)
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
            BaseElement firstElement = _stack.Peek();
            foreach (BaseElement element in _stack)
            {
                if (element != firstElement)
                    return;
            }
            _stack.Clear();
            RepitsCommand.Invoke(firstElement);
        }

        public void ClearAction() => RepitsCommand = null;
    }
}
