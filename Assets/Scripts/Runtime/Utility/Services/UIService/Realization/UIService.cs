using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utility.Services.UI;

public class UIService : MonoBehaviour, IUIService
{
    public static UIService Instance;


    private List<IWindow> _windows;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        InitWindows();
    }

    public void HideAll()
    {
        foreach (var win in _windows)
            win.Hide();
    }

    public void InitWindows()
    {
        _windows = new List<IWindow>();
        _windows.AddRange(FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude));
    }

    public void LoadWindows()
    {
        throw new NotImplementedException();
    }

    T IUIService.Get<T>() => _windows.Find(x => x is T) as T;

    T IUIService.Hide<T>()
    {
        var window = _windows.Find(x => x is T) as T;
        window?.Hide();
        return window;
    }

    T IUIService.Show<T>()
    {
        var window = _windows.Find(x => x is T) as T;
        window?.Show();
        return window;
    }

}
