using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Battle
{
    public class UIUnit : MonoBehaviour, IAction
    {
        public event Action OnSelectClickButton;

        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _fillHealthImage;
        [SerializeField] private TextMeshProUGUI _healthValueText;
        [SerializeField] private GameObject _timer;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private Image _fillTimerImage;
        [SerializeField] private Image _selectedImage;
        [SerializeField] protected Button _selectedButton;

        public void Init()
        {
            Selected(false);
            _selectedButton.onClick.AddListener(() => OnSelectClickButton?.Invoke());
        }

        public void SetTimer(int currentTime, int maxTime)
        {
            _timerText.text = $"{(float)currentTime / 1000:0.0}";
            _fillTimerImage.fillAmount = (float)(maxTime - currentTime) / maxTime;
        }

        public void SetActiveTimer(bool value) => _timer.SetActive(value);

        public void SetHealth(int currentHealth, int maxHealth)
        {
            _fillHealthImage.fillAmount = (float)currentHealth / maxHealth;
            _healthValueText.text = currentHealth.ToString();
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

        public void Selected(bool value) => _selectedImage.enabled = value;

        public void ClearAction()
        {
            _selectedButton.onClick.RemoveAllListeners();
            OnSelectClickButton = null;
        }
    }
}
