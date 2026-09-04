using Sirenix.OdinInspector;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Comics
{
    [InlineEditor]
    [Serializable]
    public class ComicsData
    {
        [field: SerializeField]
        public string ComicsName;

        [field: SerializeField]
        public AssetReference ComicsReference { get; private set; }

        [field: SerializeField]
        public ComicsConditions ComicsCondition { get; set; }

        [field: ShowIf("@ComicsCondition != ComicsConditions.AnyCondition")]
        [field: SerializeField]
        public int LevelToShow { get; private set; }
    }
}
