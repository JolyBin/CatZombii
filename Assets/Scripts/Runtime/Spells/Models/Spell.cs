
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Spell", menuName = "Spells/Create Spell")]
    public class Spell : ScriptableObject
    {
        [field: SerializeField] public string Name {  get; private set; }
    }
}
