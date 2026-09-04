using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data
{
    public struct CocActionData
    {
        public ComplexObject ComplexObjectView { get; set; }

        public Dictionary<CocActionType, bool> AvailableActions { get; set; }

        public Vector3 CocPosition { get; set; }
    }
}