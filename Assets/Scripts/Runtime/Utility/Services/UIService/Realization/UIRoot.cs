using UnityEngine;

namespace Utility.Services.UI
{
    public class UIRoot : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private Camera _uiCamera;
        [SerializeField] private RectTransform _poolContainer;
        
        public Camera UICamera
        {
            get => _uiCamera;
            set => _uiCamera = value;
        }
        
        public RectTransform Container => _container;
        public RectTransform PoolContainer => _poolContainer;
    }
}