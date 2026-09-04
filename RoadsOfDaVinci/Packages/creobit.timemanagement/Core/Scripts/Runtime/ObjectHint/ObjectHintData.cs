using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectHint
{
    public readonly struct ObjectHintData
    {
        public readonly Sprite Icon;
        public readonly string NameKey;
        public readonly string NameSuffixKey;

        public ObjectHintData(Sprite icon, string nameKey, string nameSuffixKey)
        {
            Icon = icon;
            NameKey = nameKey;
            NameSuffixKey = nameSuffixKey;
        }
    }
}
