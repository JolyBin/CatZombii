using Core.Flask;
using Core.Spells;
using Core.Steps;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private UIService _uiService;
    [SerializeField] private Book _currentBook;

    private StepsController _stepsController;

    private void Start()
    {
        _stepsController = new StepsController(_uiService, _currentBook);
        _stepsController.Init();


    }
}
