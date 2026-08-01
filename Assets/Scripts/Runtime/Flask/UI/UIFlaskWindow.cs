using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Collections;
using Utility.Services.UI;

namespace Core.Flask.UI
{
    public class UIFlaskWindow : UIWindow
    {
        /// <summary>
        /// Сколько колб реально есть на сцене. Источник истины — массив позиций,
        /// а не константа в коде: иначе код и сцена расходятся.
        /// </summary>
        public int FlaskPositionsCount => _falskPositions.Length;

        [SerializeField] private UIFlask _uiFlaskPrefab;
        [SerializeField] private Transform _poolContainer;
        [SerializeField] private RectTransform[] _falskPositions;

        private Pool<UIFlask> _flaskPool;



        public override void Show()
        {
            base.Show();

            EnsurePool();
        }

        /// <summary>
        /// Пул создаётся один раз на время жизни окна. Пересоздание на каждый Show()
        /// оставляло старые UIFlask в контейнере навсегда.
        /// </summary>
        private void EnsurePool()
        {
            if (_flaskPool != null)
                return;

            Func<UIFlask> initializer = new(() => Instantiate(_uiFlaskPrefab, _poolContainer));
            Func<UIFlask, bool> predicate = new((uiFlask) => !uiFlask.gameObject.activeInHierarchy);
            Action<UIFlask> returnToPoolAction = new((uiBall) =>
            {
                uiBall.transform.SetParent(_poolContainer, false);
                uiBall.gameObject.SetActive(false);
            });

            _flaskPool = new(initializer, predicate, returnToPoolAction, _falskPositions.Length);
        }

        public override void Hide(Action onHide = null)
        {
            _flaskPool?.ReturnObjectsToPool();
            base.Hide(onHide);
            //TODO предусмотреть очистку ресурсов, если нужно
        }

        public UIFlask[] GetUIFlasks(int numbers)
        {
            EnsurePool();

            // больше колб, чем позиций на сцене, отдать нельзя — при рассинхроне
            // вернём сколько есть, а не выйдем за границы массива
            numbers = Mathf.Clamp(numbers, 0, _falskPositions.Length);

            UIFlask[] result = new UIFlask[numbers];
            for (int i = 0; i < numbers; i++)
            {
                UIFlask uiFlask = _flaskPool.GetFreePooledObject();
                result[i] = uiFlask;
                uiFlask.transform.SetParent(_falskPositions[i], false);
                uiFlask.RectTransform.offsetMin = Vector2.zero;
                uiFlask.RectTransform.offsetMax = Vector2.zero;
                uiFlask.gameObject.SetActive(true);
            }
            return result;
        }
    }
}
