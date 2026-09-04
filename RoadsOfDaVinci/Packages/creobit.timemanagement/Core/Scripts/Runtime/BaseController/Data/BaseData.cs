using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Audio;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.Data
{
    [Serializable]
    [FoldoutGroup(RuntimeConstants.FoldoutNames.BaseData)]
    [InlineProperty]
    [HideLabel]
    public class BaseData
    {
        [field: SerializeField]
        public int MaxUnitCount { get; private set; }

        [field: SerializeField]
        public bool IsActiveOnStart { get; private set; }

        [field: SerializeField]
        public bool IsStandingPoint { get; private set; }

        [field: SerializeField]
        public bool UseOwnPivotAsUnitHome { get; private set; }

        [field: SerializeField]
        public MovableObjectView UnitPrefab { get; private set; }

        [field: SerializeField]
        public SimpleResource UnitResource { get; private set; }

        [field: SerializeField]
        public GameplayTagSO[] BaseTypeTags { get; private set; }
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.DiseasableData)]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool CanBeDiseased { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.DiseasableData)]
        [field: ShowIf(nameof(CanBeDiseased))]
        [field: SerializeField]
        public StaticObjectView DiseasableView { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.DiseasableData)]
        [field: ShowIf(nameof(CanBeDiseased))]
        [field: SerializeField]
        public UltEvent OnDiseased { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.DiseasableData)]
        [field: ShowIf(nameof(CanBeDiseased))]
        [field: SerializeField]
        public UltEvent OnHealed { get; private set; }

        // Перенесено из ObjectDataSO.DiseasableData (SO-уровневый чекбокс никогда не читался
        // рантаймом — было мёртвой настройкой). CanBeDiseased выше теперь единственный переключатель.
        [FoldoutGroup(RuntimeConstants.FoldoutNames.DiseasableData)]
        [field: ShowIf(nameof(CanBeDiseased))]
        [field: SerializeField]
        public Color DiseaseIconColor { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.DiseasableData)]
        [field: ShowIf(nameof(CanBeDiseased))]
        [field: SerializeField]
        public int MaxDiseasedUnits { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.DiseasableData)]
        [field: ShowIf(nameof(CanBeDiseased))]
        [field: SerializeField]
        public AudioClipCollection HealSounds { get; private set; }
    }
}