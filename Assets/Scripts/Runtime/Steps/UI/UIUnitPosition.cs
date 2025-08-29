using UnityEngine;

namespace Core.Steps.UI
{
    public class UIUnitPosition: MonoBehaviour
    {
        [SerializeField] private RectTransform _unitPosition;

        public bool IsFree { get; private set; } = true;

        public void SetFree() => IsFree = true;

        public void SetUnit(RectTransform rect)
        {
            IsFree = false;
            rect.SetParent(_unitPosition, false);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}