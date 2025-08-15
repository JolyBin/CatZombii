using Core.Spells;
using Meta.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utility.Services.UI;

namespace Meta
{
    public class EquipmentController
    {

        private IUIService _uiService;
        private UIEquipmentWindow _window;

        public EquipmentController(IUIService uiService)
        {
            _uiService = uiService;
        }

        public void OpenWindow(Book selectedBook)
        {
            _window = _uiService.Show<UIEquipmentWindow>();
            _window.OnBackButtonClick += CloseWidow;
            _window.SetBook(selectedBook);

        }

        public void CloseWidow()
        {
            _window.Hide();
        }
    }
}
