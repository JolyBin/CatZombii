using Core.Flask.Models;
using Core.Flask.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Utility.Diagnostics; // TactMeter (временный замер, Шаг 0) — удалить вместе с TactMeter.cs
using Utility.Services.UI;

namespace Core.Flask
{
    public class FlaskController : IAction
    {


        public event Action<Element> OnFlaskFull;
        public event Action MoveCommand;

        private readonly IUIService _uiService;

        private Flask _selectedFlask;
        private Dictionary<Flask, UIFlask> _uiFlasks = new();
        private ElementsGenerator _generator;
        private List<IAction> _actions = new();
        private Element[] _uniqElements;
        UIFlaskWindow _window;

        public FlaskController(IUIService uiService, Element[] currentElements)
        {
            _uiService = uiService;
            _uniqElements = currentElements;
            _uiFlasks = new();

        }

        public void Init()
        {
            _window = _uiService.Show<UIFlaskWindow>();
            _actions = new();
            _uiFlasks = new();
            // сколько колб — решает сцена (массив позиций окна), а не константа в коде
            UIFlask[] uIFlasks = _window.GetUIFlasks(_window.FlaskPositionsCount);
            Flask[] flasks = new Flask[uIFlasks.Length];
            _generator = new ElementsGenerator(_uniqElements, 500);
            // вместимость колбы тоже задана сценой — числом «уровней воды» в префабе колбы
            int flaskMaxSize = uIFlasks.FirstOrDefault()?.Capacity ?? 0;
            InitializeFlasks(_uniqElements, flasks, uIFlasks, flaskMaxSize);

            TactMeter.BeginBattle();                              // TactMeter (временный замер, Шаг 0)
            OnFlaskFull += _ => TactMeter.RegisterCollapse();     // TactMeter (временный замер, Шаг 0)
            MoveCommand += TactMeter.RegisterMove;                // TactMeter (временный замер, Шаг 0)
        }



        public void SubscribeToMove()
        {
            foreach(var flaskKeyValue in _uiFlasks)
            {
                flaskKeyValue.Value.ButtonClickCommand += () => ReactClickCommand(flaskKeyValue.Key);
                flaskKeyValue.Key.RepitsCommand += (Element element) => 
                {
                    OnFlaskFull?.Invoke(element);
                    UpdateFlask(flaskKeyValue);
                };

                _actions.Add(flaskKeyValue.Value);
                _actions.Add(flaskKeyValue.Key);
            }
        }

        private void InitializeFlasks(Element[] elements, Flask[] flasks, UIFlask[] uIFlasks, int maxSize)
        {
            // две последние колбы стартуют пустыми, остальные полными; при одной-двух
            // позициях на сцене полных не остаётся — но за границы массива не выходим
            int fullCount = Math.Max(0, flasks.Length - 2);

            for (int i = 0; i < fullCount; i++)
            {
                Element[] generatorResults = _generator.GetElements(maxSize, maxSize);
                uIFlasks[i].InitializeFlask();
                Flask flask = new Flask(maxSize, generatorResults);
                uIFlasks[i].Bind(flask);
                _uiFlasks.Add(flask, uIFlasks[i]);
            }

            for (int i = fullCount; i < flasks.Length; i++)
            {
                Element[] generatorResults = _generator.GetElements(0, maxSize);
                uIFlasks[i].InitializeFlask();
                Flask flask = new Flask(maxSize, generatorResults);
                uIFlasks[i].Bind(flask);
                _uiFlasks.Add(flask, uIFlasks[i]);
            }
        }

        private void UpdateFlask(KeyValuePair<Flask, UIFlask> keyValue)
        {
            // вместимость знает сама модель — второго числа рядом с ней не заводим
            int maxSize = keyValue.Key.MaxSize;
            Element[] generatorResults = _generator.GetElements(maxSize, maxSize);
            // вид привязан к модели: перерисовка придёт из UpdateFlask через OnChanged
            keyValue.Key.UpdateFlask(generatorResults);
        }

        private void ReactClickCommand(Flask flask)
        {
            if (_selectedFlask == null)
                SelectFask(flask);
            else if (_selectedFlask == flask)
                UnselectFlask();
            else
                MoveBall(flask);
        }

        private void SelectFask(Flask flask)
        {
            if (!flask.IsPossiblePopElement)
                return;
            _selectedFlask = flask;
            _uiFlasks[_selectedFlask].SelectElement();
        }

        private void UnselectFlask()
        {
            _uiFlasks[_selectedFlask].DeselectElement();
            _selectedFlask = null;
        }

        private void MoveBall(Flask flask)
        {
            if(flask.IsPossiblePushElement)
            {
                
                // оба вида перерисуются сами по OnChanged модели — отсюда содержимое не трогаем
                Element element = _selectedFlask.PopElement();
                _uiFlasks[_selectedFlask].DeselectElement();
                flask.PushElement(element);
                _selectedFlask = null;
                MoveCommand?.Invoke();
                
            }
            else
            {
                UnselectFlask();
            }
        }

        public void ClearAction()
        {
            foreach(var action in _actions)
                action.ClearAction();
            _actions = new();
            //OnFlaskFull = null;
        }

        public void Exit()
        {
            TactMeter.EndBattle();                               // TactMeter (временный замер, Шаг 0)
            ClearAction();
            OnFlaskFull = null;
            _window.Hide();
        }
    }
}
