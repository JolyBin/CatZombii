using System;
using UnityEngine;
using Utility.Services.Localization;

namespace Utility.Services.UI
{
    [RequireComponent(typeof(Canvas))]
    public abstract class UIWindow : MonoBehaviour, IWindow
    {
        [SerializeField]
        private Canvas _canvas;

        private WindowState _state = WindowState.Close;

        /// <summary>
        /// Все <see cref="LocalizedText"/> внутри окна. Собираются один раз лениво:
        /// <c>Show()</c> зовут из <c>UIService</c> в том числе раньше, чем отработает
        /// <c>Awake</c> самого окна (так уже происходит с <c>UITableWindow</c>),
        /// поэтому сбор в <c>Awake</c> был бы гонкой.
        /// </summary>
        private LocalizedText[] _localizedTexts;

        public WindowState State
        {
            get => _state;
            protected set => _state = value;
        }

        public virtual void Show()
        {
            ApplyLocalization();
            _canvas.enabled = true;
        }

        /// <summary>
        /// ТОЧКА УСТАНОВКИ ТЕКСТА ПРИ ПОКАЗЕ ОКНА.
        ///
        /// Она нужна отдельно от <c>OnEnable</c> у самих текстов, потому что окна
        /// в проекте гасятся <c>Canvas.enabled</c>, а не <c>SetActive</c> (docs/04,
        /// решение 4): объект не выключается, значит <c>OnEnable</c> при показе окна
        /// НЕ СРАБОТАЕТ — он отработает ровно один раз, при загрузке сцены.
        /// Пока язык не меняется, этого хватило бы; как только меняется — открытое
        /// окно осталось бы на старом языке навсегда.
        ///
        /// Дети, созданные пулом уже после первого показа (карточки комбинаций),
        /// в кэш не попадут — их закрывает их собственный <c>OnEnable</c>.
        /// </summary>
        protected void ApplyLocalization()
        {
            _localizedTexts ??= GetComponentsInChildren<LocalizedText>(true);

            for (int i = 0; i < _localizedTexts.Length; i++)
            {
                if (_localizedTexts[i] != null)
                    _localizedTexts[i].Apply();
            }
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