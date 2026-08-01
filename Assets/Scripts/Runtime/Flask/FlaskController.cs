using Core.Flask.Models;
using Core.Flask.UI;
using System;
using System.Collections.Generic;
using Utility.Diagnostics; // TactMeter (временный замер, Шаг 0) — удалить вместе с TactMeter.cs
using Utility.Services.UI;

namespace Core.Flask
{
    public class FlaskController : IAction
    {


        private const int FLASK_MAX_SIZE = 4;
        private const int FLASK_COUNT = 6;

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
            UIFlask[] uIFlasks = _window.GetUIFlasks(FLASK_COUNT);
            Flask[] flasks = new Flask[uIFlasks.Length];
            _generator = new ElementsGenerator(_uniqElements, 500);
            InitializeFlasks(_uniqElements, flasks, uIFlasks, FLASK_MAX_SIZE);

            TactMeter.BeginBattle();                              // TactMeter (временный замер, Шаг 0)
            OnFlaskFull += _ => TactMeter.RegisterCollapse();     // TactMeter (временный замер, Шаг 0)
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
            
            for (int i = 0; i < flasks.Length - 2; i++)
            {
                Element[] generatorResults = _generator.GetElements(4, 4);
                uIFlasks[i].InitializeFlask();
                uIFlasks[i].SetElements(generatorResults);
                _uiFlasks.Add(new Flask(4, generatorResults), uIFlasks[i]);
            }

            for (int i = flasks.Length - 2; i < flasks.Length; i++)
            {
                Element[] generatorResults = _generator.GetElements(0, 4);
                uIFlasks[i].InitializeFlask();
                uIFlasks[i].SetElements(generatorResults);
                _uiFlasks.Add(new Flask(4, generatorResults), uIFlasks[i]);
            }
        }

        private void UpdateFlask(KeyValuePair<Flask, UIFlask> keyValue)
        {
            Element[] generatorResults = _generator.GetElements(4, 4);
            keyValue.Value.RemoveAllElements(generatorResults);
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
                
                Element element = _selectedFlask.PopElement();
                _uiFlasks[_selectedFlask].DeselectElement();
                _uiFlasks[_selectedFlask].RemoveElement();
                _uiFlasks[flask].AddElement(element);
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
