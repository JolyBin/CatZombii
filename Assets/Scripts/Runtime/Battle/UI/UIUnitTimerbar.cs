using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Battle
{
    public class UIUnitTimerbar: MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private Image _fillTimerImage;
        public void SetTimer(int currentTime, int maxTime)
        {
            _timerText.text = $"{(float)currentTime / 1000:0.0}";
            _fillTimerImage.fillAmount = (float)(maxTime - currentTime) / maxTime;
        }
    }
}
