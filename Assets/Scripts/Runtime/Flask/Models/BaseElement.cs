using UnityEngine;

namespace Core.Flask.Models
{
    public abstract class BaseElement : ScriptableObject
    {
        [field: SerializeField] public Sprite Texture { get; private set; }
    }
}
