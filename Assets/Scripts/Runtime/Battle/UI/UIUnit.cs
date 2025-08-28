using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Battle
{
    public class UIUnit : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _fillHealthImage;
        [SerializeField] private GameObject _timer;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private Image _fillTimerImage;
        public void SetTimer(int currentTime, int maxTime)
        {
            _timerText.text = $"{(float)currentTime / 1000:0.0}";
            _fillTimerImage.fillAmount = (float)(maxTime - currentTime) / maxTime;
        }

        public void SetActiveTimer(bool value) => _timer.SetActive(value);

        public void SetHealth(int currentHealth, int maxHealth)
        {
            _fillHealthImage.fillAmount = (float)currentHealth / maxHealth;
        }

        public void SetName(string name)
        {
            if (name == null || name == "")
            {
                _nameText.enabled = false;
                return;
            }
            _nameText.text = name;
            _nameText.enabled = true;
        }
    }
}
