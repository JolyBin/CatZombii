using Core.Flask.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Flask.UI
{
    public class UIFlask : MonoBehaviour, IAction
    {
        private const float _animDuration = 0.5f;

        public event Action ButtonClickCommand;

        [field: SerializeField] public RectTransform RectTransform { get; private set; }

        [SerializeField] private RectTransform _ballContainer;
        [SerializeField] private RectTransform _selectedPosition;
        [SerializeField] private UIBall _bullPrefab;
        [SerializeField] private Button _button;
        [SerializeField] private Image[] _waterImages;


        private Vector3 _basePosition;

        private int _currentIndex = 0;

        public void InitializeFlask()
        {
            foreach (var item in _waterImages)
            {
                item.fillAmount = 0;
            }
            _button.onClick.AddListener(() => ButtonClickCommand?.Invoke());
            _basePosition = transform.position;
        }

        public void SetElements(Element[] startElements)
        {
            foreach (var element in startElements)
            {
                AddElement(element);
            }
        }

        public void AddElement(Element startElements)
        {
            Image _currentImage = _waterImages[_currentIndex];
            _currentImage.color = startElements.Color;
            _currentImage.fillAmount = (float)(_currentIndex + 1) / 4;
            _currentIndex++;
        }

        public void SelectElement()
        {
            RectTransform.position = _selectedPosition.position;
        }

        public void DeselectElement()
        {
            RectTransform.position = _basePosition;
        }

        public void RemoveElement()
        {
            _currentIndex--;
            Image _currentImage = _waterImages[_currentIndex];
            _currentImage.fillAmount = 0;
        }

        public void RemoveAllElements(Element[] newElements)
        {
            _currentIndex = 0;
            foreach (var item in _waterImages)
            {
                item.fillAmount = 0;
            }
            SetElements(newElements);
        }

        public void Dispose()
        {
            _button.onClick.RemoveAllListeners(); //TODO: надо нормально пул как-то очистить
        }

        public void ClearAction() =>  ButtonClickCommand = null;
    }
}
