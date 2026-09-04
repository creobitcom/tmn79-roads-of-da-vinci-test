using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data
{
        [Serializable]
        public class TransitionData
        {
            [field: SerializeField] 
            public StaticObjectView TransitionFrom { get; set; }

            [field: SerializeField] 
            public StaticObjectView TransitionTo { get; set; }

            [field: SerializeField]
            public ResourceAmount[] CostsData { get; set; }

            [field: SerializeField]
            public GameplayIntervalSpecificParametersUlt GameplayIntervalSpecificParametersUlt { get; private set; }

            [field: SerializeField]
            public GameplayIntervalGeneralParameters GameplayIntervalGeneralParameters { get; private set; }
            
            [field: SerializeField]
            public UltEvent OnPrimaryAction { get; private set; }

            [field: SerializeField]
            public BuildingSettings BuildingSettings { get; private set; }

            [field: SerializeField]
            public List<ObjectView.ObjectView> ObjectsToAutoBuild { get; private set; }
        }
}