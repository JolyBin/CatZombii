using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Core.Steps.UI
{
    public class StepsWindow : UIWindow
    {
        public event Action OnClickHomeButton;

        [SerializeField] private Button _homeButton;
        [SerializeField] private TextMeshProUGUI _stepCounterTXT, _currentStateTXT;

        public override void Show()
        {
            _homeButton.onClick.AddListener(() => OnClickHomeButton?.Invoke()); 
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            OnClickHomeButton = null;
            _homeButton.onClick.RemoveAllListeners();
            base.Hide(onHide);
        }

        public void SetStepCounerText(int value)
        {
            _stepCounterTXT.text = string.Format("Numbers Step: {0}", value);
        }

        public void SetCurrentStateText(string state)
        {
            _currentStateTXT.text = string.Format("Current State:\n{0}", state);
        }
    }
}
