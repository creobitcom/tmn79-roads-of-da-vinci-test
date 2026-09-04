using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Audio;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production
{
    [Serializable]
    [InlineProperty]
    [HideLabel]
    public class ProductionData
    {
        [field: SerializeField]
        public UltEvent IntervalStarted { get; private set; }

        [field: SerializeField]
        public UltEvent IntervalCompleted { get; private set; }

        [field: SerializeField]
        [field: InlineProperty]
        [field: HideLabel]
        public GameplayIntervalGeneralParameters GameplayIntervalGeneralParameters { get; private set; }

        [field: SerializeField]
        public bool StartProductionImmediately { get; private set; } = true;

        [field: SerializeField]
        public AudioClip IntervalEndedSound { get; private set; }

        [field: SerializeField]
        public bool UseObjectSpawn { get; private set; } = true;

        [field: ShowIf(nameof(UseObjectSpawn))]
        [field: SerializeField]
        public Transform SpawnPoint { get; private set; }

        [field: ShowIf(nameof(UseObjectSpawn))]
        [field: SerializeField]
        public ObjectView SpawnedSpitObject { get; private set; }

        [field: ShowIf("@!UseObjectSpawn")]
        [field: SerializeField]
        public StaticObjectView SpawnedCollectibleObject { get; private set; }
        
        [field: ShowIf("@UseObjectSpawn")]
        [field: SerializeField]
        public UpgradedResourceBehaviours UpgradedResourceBehaviour { get; private set; }

        // Breakable
        [FoldoutGroup(RuntimeConstants.FoldoutNames.BreakableData)]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool CanBeBroken { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.BreakableData)]
        [field: ShowIf(nameof(CanBeBroken))]
        [field: SerializeField]
        public ObjectView BreakableView { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.BreakableData)]
        [field: ShowIf(nameof(CanBeBroken))]
        [field: SerializeField]
        public UltEvent OnBroke { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.BreakableData)]
        [field: ShowIf(nameof(CanBeBroken))]
        [field: SerializeField]
        public UltEvent OnRepaired { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.BreakableData)]
        [field: ShowIf(nameof(CanBeBroken))]
        [field: SerializeField]
        public AudioClipCollection BreakSounds { get; private set; }

        [field: SerializeField]
        public bool BlockProduction { get; private set; }

        public void SetBlockProduction(bool isSteal)
        {
            BlockProduction = isSteal;
        }

        // Не мутирует CanBeBroken: раньше сам факт наличия здания с RepairTag на уровне
        // принудительно включал поломку на объекте, игнорируя то, что реально стоит в инспекторе.
        // Теперь это чистая проверка валидности найденного тега мастерской.
        public bool IsValidRepairTag(Utils.GameplayTags.GameplayTagSO repairTag) =>
            repairTag != null && repairTag.name == RuntimeConstants.SpecialObjects.RepairTag;
    }
}