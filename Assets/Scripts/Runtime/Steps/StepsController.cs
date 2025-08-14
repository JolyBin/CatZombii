using Core.Flask;
using Core.Spells;
using Core.Steps.UI;
using Cysharp.Threading.Tasks;
using Meta;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Steps
{
    public class StepsController
    {
        private const int _maxSteps = 100;
        private const int _stepAdditive = 100;

        private readonly FlaskController _flaskController;
        private readonly IUIService _uiService;
        private readonly TableController _tableController;

        private StepsWindow _window;
        private StepState _currentState;
        private int _currentNumbersStep;
        private HomeController _homeController;

        public StepsController(IUIService uIService, Book currentBook, HomeController homeController)
        {
            _flaskController = new FlaskController(uIService, currentBook.UniqElements);
            _tableController = new TableController(currentBook, _flaskController, uIService);
            _uiService = uIService;
            _homeController = homeController;
        }

        public void Init()
        {
            _window = _uiService.Show<StepsWindow>();
            _flaskController.Init();
            _window.OnClickHomeButton += Exit;
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

        private void Exit()
        {
            _window.Hide();
            _flaskController.Exit();
            _tableController.Exit();
            _homeController.OpenWindow();

        }
    }

    public enum StepState
    {
        PlayerStep,
        EnemyStep
    }
}
