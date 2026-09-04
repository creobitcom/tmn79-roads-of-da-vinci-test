using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    [CreateAssetMenu(fileName = "MovableObjectDataSO", menuName = "Creobit/Units/Create new MovableObjectDataSO")]
    public class MovableObjectDataSO : ObjectDataSO
    {
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Tags)]
        [field: SerializeField]
        public GameplayTagSO[] BaseTypeTags { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Resources)]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool StashResourcesAfterEndMove { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectData)]
        [field: SerializeField]
        public float Speed { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectData)]
        [field: MinValue(0.012f)]
        [field: SerializeField]
        public float InteractionSpeed { get; private set; } = 1f;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectData)]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool UseRotator { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectData)]
        [field: ShowIf(nameof(UseRotator))]
        [field: MinValue(5f)]
        [field: SerializeField]
        public float RotatorRotationSpeed { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ResourcesStealer)]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool UseResourcesStealer { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ResourcesStealer)]
        [field: ShowIf(nameof(UseResourcesStealer))]
        [field: SerializeField]
        public GameplayIntervalGeneralParameters ResourceStealInterval { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.BaseData)]
        [field: SerializeField]
        public Sprite UnitIconInsideBase { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.BaseData)]
        [field: SerializeField]
        public Sprite UnitIconOutsideBase { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.BaseData)]
        [field: SerializeField]
        public bool UnitHideInBase;
    }
}