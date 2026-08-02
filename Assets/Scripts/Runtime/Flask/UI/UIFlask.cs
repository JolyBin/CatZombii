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

        /// <summary>
        /// Вместимость колбы: сколько «уровней воды» нарисовано в префабе.
        /// Источник истины — сцена, а не константа в коде.
        /// </summary>
        public int Capacity => _waterImages.Length;

        [field: SerializeField] public RectTransform RectTransform { get; private set; }

        [SerializeField] private RectTransform _ballContainer;
        [SerializeField] private RectTransform _selectedPosition;
        [SerializeField] private UIBall _bullPrefab;
        [SerializeField] private Button _button;
        [SerializeField] private Image[] _waterImages;


        /// <summary>
        /// Место колбы на полке — в КООРДИНАТАХ РАСКЛАДКИ, а не в мировых, и снятое
        /// В МОМЕНТ ПОДЪЁМА, а не при инициализации.
        ///
        /// Раньше здесь лежал <c>transform.position</c>, взятый в <c>InitializeFlask()</c>.
        /// Это было неверно дважды. Во-первых, по времени: <c>InitializeFlask</c> зовут
        /// сразу после того, как <c>UIFlaskWindow.GetUIFlasks</c> воткнул колбу в слот,
        /// то есть ДО того, как <c>HorizontalLayoutGroup</c> разложит слоты, — колба
        /// возвращалась не на своё место, а туда, где она была полкадра.
        /// Во-вторых, по системе координат: мировая позиция зависит от размера канваса,
        /// а он меняется от смены ориентации и от вылезающей адресной строки браузера.
        /// </summary>
        private Vector2 _baseAnchoredPosition;

        /// <summary>Колба поднята. Без флага повторный <c>SelectElement</c> поднял бы её ещё раз.</summary>
        private bool _isSelected;

        private Flask _flask;
        private readonly List<Element> _content = new();

        public void InitializeFlask()
        {
            _content.Clear();
            Redraw();
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => ButtonClickCommand?.Invoke());
            // колба могла прийти из пула поднятой — опускаем до того, как её увидят
            _isSelected = false;
            RectTransform.anchoredPosition = Vector2.zero;
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

        public void SelectElement()
        {
            if (_isSelected)
                return;
            _isSelected = true;
            // базу читаем здесь: к этому кадру раскладка полки уже отработала
            _baseAnchoredPosition = RectTransform.anchoredPosition;
            RectTransform.position = _selectedPosition.position;
        }

        public void DeselectElement()
        {
            if (!_isSelected)
                return;
            _isSelected = false;
            RectTransform.anchoredPosition = _baseAnchoredPosition;
        }

        public void Dispose()
        {
            _button.onClick.RemoveAllListeners(); //TODO: надо нормально пул как-то очистить
        }

        public void ClearAction()
        {
            ButtonClickCommand = null;
            DeselectElement();
            Unbind();
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
