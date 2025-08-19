using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Battle
{
    public class UIUnitHealthBar : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _fillHealthImage;

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
