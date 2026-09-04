using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags
{
    [CreateAssetMenu(fileName = "Tag", menuName = "8floor/TimeManager/Gameplay/Tag")]
    public class GameplayTagSO : ScriptableObject, ITimeManagerSO
    {
        [field: SerializeField]
        private string Name { get; set; }
        
        [field: SerializeField]
        private TagType Type { get; set; }
        
        [field: SerializeField]
        private string NameLocalizeKey { get; set; }
        
        public string TagName => Name;
        public TagType TagType => Type;
        public string TagNameLocalizeKey => NameLocalizeKey;

        public override bool Equals(object other)
        {
            if (other is GameplayTagSO otherTag)
            {
                return otherTag.TagName == TagName;
            }

            return false;
        }
    
        public override int GetHashCode()
        {
            return string.IsNullOrEmpty(TagName) ? 0 : TagName.GetHashCode();
        }
    }
}
