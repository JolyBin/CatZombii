using UnityEngine;
using UnityEngine.UI;

namespace Core.Battle
{
    public class UIUnitHealthBar : MonoBehaviour
    {
        [SerializeField] private Image _fillHealthImage;

        public void SetHealth(int currentHealth, int maxHealth)
        {
            _fillHealthImage.fillAmount = (float) currentHealth / maxHealth;
        }


    }
}
