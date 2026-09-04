using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    [CreateAssetMenu(fileName = "CollectibleItem", menuName = "8floor/TimeManager/Collectibles/Item")]
    public class CollectibleItemSO : ScriptableObject, ITimeManagerSO
    {
        [field: SerializeField] public string Id { get; private set; }
        [field: SerializeField] public CollectibleType Type { get; private set; }
        [field: SerializeField, MinValue(1)] public int MaxParts { get; private set; } = 1;
        [field: SerializeField] public string NameLocalizeKey { get; private set; }
        
        [field: SerializeField] public Sprite Icon { get; private set; }

        [field: SerializeField]
        public CutsceneSequenceSO ComicOnCollect { get; private set; }
    }
}
