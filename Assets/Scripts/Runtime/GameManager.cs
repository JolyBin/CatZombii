using Core.Battle;
using Core.Spells;
using Core.Steps;
using UnityEngine;

namespace Meta
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private UIService _uiService;
        [SerializeField] private Book _startBook;
        [SerializeField] private BattleConfig[] _currentLevels;

        private HomeController _homeController;

        private void Start()
        {
            _uiService.HideAll();
            _homeController = new HomeController(_uiService, _startBook, _currentLevels);
        }
    }
}
