using Core.Flask;
using Core.Steps;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private UIService _uiService;

    private StepsController _stepsController;

    private void Start()
    {
        _stepsController = new StepsController(_uiService);
        _stepsController.Init();


    }
}
