using Core.Flask;
using Core.Steps.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Steps
{
    public class StepsController
    {
        private const int _maxSteps = 10;
        private const int _stepAdditive = 4;

        private readonly FlaskController _flaskController;
        private readonly IUIService _uiService;

        private StepsWindow _window;
        private StepState _currentState;
        private int _currentNumbersStep;

        public StepsController(IUIService uIService)
        {
            _flaskController = new FlaskController(uIService);
            _uiService = uIService;
        }

        public void Init()
        {
            _flaskController.Init();
            _window = _uiService.Show<StepsWindow>();
            _currentNumbersStep = 0;
            SetPlayerState();
        }

        private void SetPlayerState()
        {
            _currentState = StepState.PlayerStep;
            _window.SetCurrentStateText(_currentState.ToString());

            _currentNumbersStep = Mathf.Clamp(_currentNumbersStep + _stepAdditive, 0, _maxSteps);
            _window.SetStepCounerText(_currentNumbersStep);

            _flaskController.SubscribeToMove();
            _flaskController.MoveCommand += ToStep;
            _window.EndStepButtonClickCommand += SetEnemyStep;
        }

        private async void SetEnemyStep()
        {
            _currentState = StepState.EnemyStep;
            _window.SetCurrentStateText(_currentState.ToString());
            _flaskController.ClearAction();
            await UniTask.WaitForSeconds(5f);
            SetPlayerState();
        }

        private void ToStep()
        {
            _currentNumbersStep--;
            _window.SetStepCounerText(_currentNumbersStep);
            if (_currentNumbersStep == 0)
            {
                SetEnemyStep();
            }
        }
    }

    public enum StepState
    {
        PlayerStep,
        EnemyStep
    }
}
