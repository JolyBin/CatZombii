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

        /// <summary>
        /// Таймер удара в ТАКТАХ мира, а не в секундах (docs/10 §0.2). Игроку показывается
        /// целое «ходов до удара»: дробные секунды в пошаговой игре не означают ничего —
        /// между его действиями таймер вообще не двигается.
        /// </summary>
        public void SetTimer(int currentTacts, int maxTacts)
        {
            _timerText.text = currentTacts.ToString();
            _fillTimerImage.fillAmount = maxTacts <= 0 ? 0f : (float)(maxTacts - currentTacts) / maxTacts;
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
