using UnityEngine;

namespace Core.Flask.Models
{
    [CreateAssetMenu(fileName = "Element", menuName = "Elemnts/Create Element")]
    public class Element : ScriptableObject
    {
        [field: SerializeField] public int ID { get; private set; }
        [field: SerializeField] public Sprite Texture { get; private set; }
    }
}
