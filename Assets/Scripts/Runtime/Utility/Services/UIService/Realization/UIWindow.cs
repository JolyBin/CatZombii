using System;
using UnityEngine;

namespace Utility.Services.UI
{
    [RequireComponent(typeof(Canvas))]
    public abstract class UIWindow : MonoBehaviour, IWindow
    {
        [SerializeField]
        private Canvas _canvas;

        private WindowState _state = WindowState.Close;

        public WindowState State
        {
            get => _state;
            protected set => _state = value;
        }
        
        public virtual void Show()
        {
            _canvas.enabled = true;
        }

        public virtual void Hide(Action onHide = null)
        {
            _canvas.enabled = false;
            onHide?.Invoke();
        }

        public virtual void OnHided(){}

        #region On Component Added Funcctionality
#if UNITY_EDITOR
        public void Reset()
        {
            _canvas = GetComponent<Canvas>();
        }
#endif
        #endregion
    }
}