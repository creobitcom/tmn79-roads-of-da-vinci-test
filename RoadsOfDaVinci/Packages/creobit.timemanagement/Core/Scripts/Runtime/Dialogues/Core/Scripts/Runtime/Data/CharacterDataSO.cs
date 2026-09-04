using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "CharacterData", menuName = "8floor/TimeManager/Dialogues/CharacterData")]
public class CharacterDataSO : ScriptableObject
{
    [Tooltip("Key is state of character; Value is portrait of character")]
    [PropertyOrder(1)]
    [SerializeField]
    private SerializedDictionary<string, Sprite> _icons;

    [field: Tooltip("Name of character (Witch, Fireman etc)")]
    [field: PropertyOrder(0)]
    [field: SerializeField]
    public string ID { get; private set; }

    public IReadOnlyDictionary<string, Sprite> Icons => _icons;

    public override bool Equals(object other)
    {
        if (other is CharacterDataSO otherData)
        {
            return ID == otherData.ID;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return string.IsNullOrEmpty(ID) ? 0 : ID.GetHashCode();
    }
}
