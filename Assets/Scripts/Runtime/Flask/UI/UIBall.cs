using Core.Flask.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Flask.UI
{
    public class UIBall : MonoBehaviour
    {
        [field: SerializeField] public RectTransform RectTransform { get; private set; }

        [SerializeField] private Image _texture;

        public UIBall SetConfig(Element element)
        {
            _texture.sprite = element.Texture;
            return this;
        }

        public void Reset()
        {
            RectTransform = transform as RectTransform;
        }

        public void CopyBallSettings(UIBall uIBall)
        {
            _texture.sprite = uIBall._texture.sprite;
        }
    }
}
