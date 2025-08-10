using Core.Flask.Models;
using Core.Flask.UI;
using System.Collections.Generic;
using UnityEngine;
using Utility.Services.UI;
using System;

namespace Core.Flask
{
    public class FlaskController : IAction
    {
        private const int FLASK_MAX_SIZE = 4;
        private const int FLASK_COUNT = 6;


        public event Action MoveCommand;

        private readonly IUIService _uiService;

        private Flask _selectedFlask;
        private Dictionary<Flask, UIFlask> _uiFlasks = new();
        private ElementsGenerator _generator;
        private List<IAction> _actions = new();

        public FlaskController(IUIService uiService)
        {
            _uiService = uiService;
            _uiFlasks = new();
        }

        public void Init()
        {
            UIFlaskWindow window = _uiService.Show<UIFlaskWindow>();
            _actions = new();
            BaseElement[] elements = Resources.LoadAll<BaseElement>("Elements");
            UIFlask[] uIFlasks = window.GetUIFlasks(FLASK_COUNT);
            Flask[] flasks = new Flask[uIFlasks.Length];
            _generator = new ElementsGenerator(elements, 500);
            InitializeFlasks(elements, flasks, uIFlasks, FLASK_MAX_SIZE);
        }



        public void SubscribeToMove()
        {
            foreach(var flaskKeyValue in _uiFlasks)
            {
                flaskKeyValue.Value.ButtonClickCommand += () => ReactClickCommand(flaskKeyValue.Key);
                flaskKeyValue.Key.RepitsCommand += (_) => UpdateFlask(flaskKeyValue);

                _actions.Add(flaskKeyValue.Value);
                _actions.Add(flaskKeyValue.Key);
            }
        }

        private void InitializeFlasks(BaseElement[] elements, Flask[] flasks, UIFlask[] uIFlasks, int maxSize)
        {
            
            for (int i = 0; i < flasks.Length - 2; i++)
            {
                BaseElement[] generatorResults = _generator.GetElements(4, 4);
                uIFlasks[i].InitializeFlask(4);
                uIFlasks[i].SetElements(generatorResults);
                _uiFlasks.Add(new Flask(4, generatorResults), uIFlasks[i]);
            }

            for (int i = flasks.Length - 2; i < flasks.Length; i++)
            {
                BaseElement[] generatorResults = _generator.GetElements(0, 4);
                uIFlasks[i].InitializeFlask(4);
                uIFlasks[i].SetElements(generatorResults);
                _uiFlasks.Add(new Flask(4, generatorResults), uIFlasks[i]);
            }
        }

        private void UpdateFlask(KeyValuePair<Flask, UIFlask> keyValue)
        {
            BaseElement[] generatorResults = _generator.GetElements(4, 4);
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
                BaseElement element = _selectedFlask.PopElement();
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
        }
    }
}
