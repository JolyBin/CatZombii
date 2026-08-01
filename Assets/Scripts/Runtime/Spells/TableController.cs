using Core.Flask;
using Core.Flask.Models;
using Core.Spells.UI;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.Services.UI;

namespace Core.Spells
{
    public class TableController
    {
        public event Action<BaseSpell> OnSuccessfulMerge;

        private readonly IUIService _uiService;

        private UITableWindow _window;
        private Table _table;
        private List<Element> _currentElements;

        public TableController(Book currentBook, FlaskController flaskController, IUIService uiService)
        {
            _uiService = uiService;
            _table = new(currentBook.Combinations);
            _currentElements = new ();
            _window = _uiService.Show<UITableWindow>();

            flaskController.OnFlaskFull += (Element element) => 
            {
                _currentElements.Add(element);
                _window.ShowFullFlask(element.Texture);
            };

            _window.OnClickCheckCombinationButton += CheckRepit;
        }

        public void CheckRepit()
        {
            BaseSpellConfig spell;
            if (_table.TryGetSpell(_currentElements.ToArray(), out spell))
            {
                _window.ShowResult(true, spell.Name);
                OnSuccessfulMerge?.Invoke(spell.GetSpell());
            }
            else
            {
                _window.ShowResult(false, $"Ничего не получилось((");
            }

            _currentElements = new();
            _window.ClearFlasks();
        }

        public void Exit()
        {
            OnSuccessfulMerge = null;
            _window.Hide();
        }
    }
}
