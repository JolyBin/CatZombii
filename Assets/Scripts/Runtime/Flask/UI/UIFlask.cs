using Core.Flask.Models;
using System;
using System.Collections.Generic;
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

        private Flask _flask;
        private readonly List<Element> _content = new();

        public void InitializeFlask()
        {
            _content.Clear();
            Redraw();
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => ButtonClickCommand?.Invoke());
            _basePosition = transform.position;
        }

        /// <summary>
        /// Привязывает виджет к модели: после этого содержимое рисуется только
        /// из <see cref="Flask"/>, собственного счётчика у виджета нет.
        /// </summary>
        public void Bind(Flask flask)
        {
            Unbind();
            _flask = flask;
            if (_flask == null)
                return;
            _flask.OnChanged += Render;
            Render(_flask.GetElements());
        }

        public void Unbind()
        {
            if (_flask == null)
                return;
            _flask.OnChanged -= Render;
            _flask = null;
        }

        public void Render(IReadOnlyList<Element> elements)
        {
            _content.Clear();
            if (elements != null)
                _content.AddRange(elements);
            Redraw();
        }

        public void SetElements(Element[] startElements) => RenderFromModelOr(startElements);

        public void AddElement(Element startElements)
        {
            if (_flask != null)
            {
                Render(_flask.GetElements());
                return;
            }
            if (_content.Count >= _waterImages.Length)
                return;
            _content.Add(startElements);
            Redraw();
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
            if (_flask != null)
            {
                Render(_flask.GetElements());
                return;
            }
            if (_content.Count == 0)
                return;
            _content.RemoveAt(_content.Count - 1);
            Redraw();
        }

        public void RemoveAllElements(Element[] newElements) => RenderFromModelOr(newElements);

        public void Dispose()
        {
            _button.onClick.RemoveAllListeners(); //TODO: надо нормально пул как-то очистить
        }

        public void ClearAction()
        {
            ButtonClickCommand = null;
            Unbind();
        }

        private void RenderFromModelOr(Element[] fallbackElements)
        {
            if (_flask != null)
            {
                Render(_flask.GetElements());
                return;
            }
            Render(fallbackElements);
        }

        private void Redraw()
        {
            for (int i = 0; i < _waterImages.Length; i++)
            {
                if (i < _content.Count)
                {
                    _waterImages[i].color = _content[i].Color;
                    _waterImages[i].fillAmount = (float)(i + 1) / _waterImages.Length;
                }
                else
                {
                    _waterImages[i].fillAmount = 0;
                }
            }
        }
    }
}
