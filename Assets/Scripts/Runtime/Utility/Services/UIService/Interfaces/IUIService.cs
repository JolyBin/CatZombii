using System;
using UnityEngine;

namespace Utility.Services.UI
{
    public interface IUIService
    {
        /// <summary>
        /// Turns on screen display
        /// </summary>
        /// <typeparam name="T"></typeparam>
        T Show<T>() where T : class, IWindow;

        void HideAll();

        /// <summary>
        /// Turns off screen display
        /// </summary>
        /// <typeparam name="T"></typeparam>
        T Hide<T>() where T : class, IWindow;

        void InitWindows();
        void LoadWindows();

        /// <summary>
        /// Returns screen by type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T Get<T>() where T : class, IWindow;
       
    }
}