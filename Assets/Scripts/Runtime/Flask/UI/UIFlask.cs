using Core.Flask.Models;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utility.Collections;

namespace Core.Flask.UI
{
    public class UIFlask : MonoBehaviour, IAction
    {
        private const float _animDuration = 0.5f;

        public event Action ButtonClickCommand;

        [field: SerializeField] public RectTransform RectTransform { get; private set; }

        [SerializeField] private RectTransform _ballContainer;
        [SerializeField] private UIBall _bullPrefab;
        [SerializeField] private Button _button;
        [SerializeField] private RectTransform _selectedElementPosition;
        [SerializeField] private RectTransform[] _uiElementPositions;

        private Pool<UIBall> _ballsPool;
        private Stack<UIBall> _ballsStack;
        private Sequence _sequence;
        private Tween _currentTween;
        private Queue<Tween> _animateQueue;

        public void InitializeFlask(int ballsNumber)
        {
            _sequence = DOTween.Sequence();
            _animateQueue = new();
            _ballsStack = new(ballsNumber);

            Func<UIBall> initializer = new(() => Instantiate(_bullPrefab, _ballContainer));
            Func<UIBall, bool> predicate = new((uiBall) => !uiBall.gameObject.activeInHierarchy);
            Action<UIBall> returnToPoolAction = new((uiBall) =>
            {
                uiBall.transform.SetParent(_ballContainer, false);
                uiBall.gameObject.SetActive(false);
                uiBall.transform.localScale = Vector3.one;
                uiBall.transform.rotation = Quaternion.identity;
            });
            _ballsPool = new(initializer, predicate, returnToPoolAction, ballsNumber);

            _button.onClick.AddListener(() => ButtonClickCommand?.Invoke());
        }

        public void SetElements(BaseElement[] startElements)
        {
            _sequence.Complete();
            _sequence = DOTween.Sequence();
            foreach (var element in startElements)
            {
                AddElement(element, false);
            }
        }

        public void AddElement(BaseElement startElements, bool isCompleteTween = true)
        {
            UIBall uiBall = _ballsPool.GetFreePooledObject();
            uiBall.SetConfig(startElements);
            uiBall.gameObject.SetActive(true);
            _ballsStack.Push(uiBall);
            if(isCompleteTween)
            {
                _sequence.Complete();
                _sequence = DOTween.Sequence();
            }
            SetPosition(uiBall, _ballsStack.Count - 1);
        }

        public void SelectElement()
        {
            UIBall selectedBall = _ballsStack.Peek();

            _currentTween?.Complete();
            _currentTween = selectedBall.transform.DOMove(_selectedElementPosition.position, _animDuration);
        }

        public void DeselectElement()
        {
            UIBall selectedBall = _ballsStack.Peek();

            _currentTween?.Complete();
            _currentTween = selectedBall.transform.DOMove(_uiElementPositions[_ballsStack.Count - 1].position, _animDuration);
        }

        public void RemoveElement()
        {
            UIBall selectedBall = _ballsStack.Pop();
            _sequence.Complete();
            _sequence = DOTween.Sequence();
            _sequence
                .Join(selectedBall.transform.DOScale(0, _animDuration))
                .Join(selectedBall.transform.DORotate(new Vector3(0, 0, 360), _animDuration, RotateMode.FastBeyond360))
                .OnComplete(() => _ballsPool.TryReturnToPool(selectedBall));
        }

        public void RemoveAllElements(BaseElement[] newElements)
        {
            if(_sequence.IsPlaying())
            {
                _sequence.OnComplete(() => RemoveAllElements(newElements));
                Debug.Log("Remo All Elements Call");
                return;
            }
            _sequence = DOTween.Sequence();
            while (_ballsStack.Count > 0)
            {
                UIBall selectedBall = _ballsStack.Pop();
                _sequence
                    .Join(selectedBall.transform.DOScale(0, _animDuration))
                    .Join(selectedBall.transform.DORotate(new Vector3(0, 0, 360), _animDuration, RotateMode.FastBeyond360))
                    .OnComplete(() => _ballsPool.TryReturnToPool(selectedBall));
            }
            _sequence.OnComplete(() => SetElements(newElements));
        }

        public void Dispose()
        {
            _button.onClick.RemoveAllListeners(); //TODO: надо нормально пул как-то очистить
        }


        private void SetPosition(UIBall uiBall, int count)
        {
            uiBall.transform.SetParent(_uiElementPositions[count], false);
            uiBall.RectTransform.offsetMin = Vector2.zero;
            uiBall.RectTransform.offsetMax = Vector2.zero;
            uiBall.transform.localScale = Vector3.zero;
            _sequence
                .Join(uiBall.transform.DOScale(1, _animDuration))
                .Join(uiBall.transform.DORotate(new Vector3(0, 0, 360), _animDuration, RotateMode.FastBeyond360));
        }

        private void Reset()
        {
            RectTransform = transform as RectTransform;
        }

        public void ClearAction() =>  ButtonClickCommand = null;
    }
}
