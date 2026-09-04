using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data
{
    [Serializable]
    public class BuildingSettings
    {
        //[field: SerializeField] 
        //public TooltipData BuildingTooltipData { get; private set; }

        [field: SerializeField]
        public List<UnitTypeCount> UnitTypeCounts { get; set; }
        
        
        [field: SerializeField]
        public GameplayTagSO[] TaskTypeTags { get; private set; }

        [field: ToggleLeft]
        [field: SerializeField]
        public bool CanBeReturnedToInitialState { get; private set; }
        
        [field: ToggleLeft]
        [field: SerializeField]
        public bool UseUpgradeMark { get; private set; }
        
        [field: SerializeField]
        public float InteractionOffset { get; private set; } = .1f;

        [field: SerializeField]
        public ObjectViewInteractionType InteractionType { get; set; }
    }
}