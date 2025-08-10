using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Core.Steps.UI
{
    public class StepsWindow : UIWindow
    {
        public event Action EndStepButtonClickCommand;

        [SerializeField] private Button _endStepButton;
        [SerializeField] private TextMeshProUGUI _stepCounterTXT, _currentStateTXT;

        public override void Show()
        {
            base.Show();
            _endStepButton.onClick.AddListener(() => EndStepButtonClickCommand.Invoke());
        }

        public override void Hide(Action onHide = null)
        {
            base.Hide(onHide);
            _endStepButton.onClick.RemoveAllListeners();
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
