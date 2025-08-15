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
        [SerializeField] private UIFlask _uiFlaskPrefab;
        [SerializeField] private Transform _poolContainer;
        [SerializeField] private RectTransform[] _falskPositions;
        [SerializeField] private TextMeshProUGUI _nameHero;
        [SerializeField] private Image _classIcon;
        [SerializeField] private Image _heroPortret;

        private Pool<UIFlask> _flaskPool;



        public override void Show()
        {
            base.Show();

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
