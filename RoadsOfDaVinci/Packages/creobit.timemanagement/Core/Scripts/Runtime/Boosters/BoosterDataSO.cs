using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters {

    [CreateAssetMenu(fileName = "BoosterDataSO", menuName = "Creobit/Booster/Create new BoosterDataSO")]
    public class BoosterDataSO : ScriptableObject
    {

        [field: SerializeField] public string Id { get; private set; } = string.Empty;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Tags)]
        [field: SerializeField]
        public GameplayTagsContainsMode TypeTagsContainsMode { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Tags)]
        [field: SerializeField]
        public GameplayTagSO[] UnitsTypeTags { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Durations)]
        [field: SerializeField]
        public float UsageDurationInSeconds { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Durations)]
        [field: SerializeField]
        public float ChargeDurationInSeconds { get; private set; }


        [field: InlineProperty]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Boosts)]
        [field: SerializeField]
        public ValueMode UnitsWalkSpeed { get; private set; }
        [field: InlineProperty]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Boosts)]
        [field: SerializeField]
        public ValueMode UnitsInteractionSpeed { get; private set; }
        [field: InlineProperty]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Boosts)]
        [field: SerializeField]
        public ValueMode TimerSpeed { get; private set; }


        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Sprites)]
        [field: SerializeField]
        public Sprite DischargedBoosterSprite { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Sprites)]
        [field: SerializeField]
        public Sprite ChargedBoosterSprite { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Sprites)]
        [field: SerializeField]
        public Sprite ActiveBoosterSprite { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Sprites)]
        [field: SerializeField]
        public Sprite ExtraBoosterSprite { get; private set; }


        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.AudioSettings)]
        [field: SerializeField]
        public AudioClip UsageSound { get; private set; }

    }

}
