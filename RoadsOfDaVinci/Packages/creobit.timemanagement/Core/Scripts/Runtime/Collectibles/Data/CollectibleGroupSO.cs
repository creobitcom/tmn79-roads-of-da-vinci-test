using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    [CreateAssetMenu(fileName = "CollectibleGroup", menuName = "8floor/TimeManager/Collectibles/Group (Book Page)")]
    public class CollectibleGroupSO : ScriptableObject
    {
        [field: SerializeField] public CollectibleType Type { get; private set; }
        [field: SerializeField] public string GroupNameLocalizeKey { get; private set; }

        [field: TextArea]
        [field: SerializeField]
        public string DescriptionLocalizeKey { get; private set; }

        [field: SerializeField, ListDrawerSettings(Expanded = true)]
        public CollectibleItemSO[] Items { get; private set; } = new CollectibleItemSO[4];

        public CollectibleType GetGroupType()
        {
            if (Items != null)
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    if (Items[i] != null) return Items[i].Type;
                }
            }
            return Type;
        }
    }
}
