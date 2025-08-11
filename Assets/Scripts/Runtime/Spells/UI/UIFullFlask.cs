using UnityEngine;
using UnityEngine.UI;


public class UIFullFlask : MonoBehaviour
{
    [SerializeField] private Image _icon;
    public void SetSprite(Sprite sprite) => _icon.sprite = sprite;

}
