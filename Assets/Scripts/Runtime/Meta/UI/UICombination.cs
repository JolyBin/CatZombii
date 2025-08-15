using Core.Flask.Models;
using Core.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meta.UI
{
    public class UICombination : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _spellDescription;
        [SerializeField] private Image[] _elementIcons;

        public void Show(Combination combination)
        {
            _spellDescription.text = combination.Spell.Name;
            for (int i = 0; i < combination.Elements.Length; i++)
            {
                Image currentImage = _elementIcons[i];
                currentImage.gameObject.SetActive(true);
                currentImage.color = combination.Elements[i].Color;
            }
            for (int i = combination.Elements.Length; i < _elementIcons.Length; i++)
            {
                _elementIcons[i].gameObject.SetActive(false);
            }
        }
    }
}
