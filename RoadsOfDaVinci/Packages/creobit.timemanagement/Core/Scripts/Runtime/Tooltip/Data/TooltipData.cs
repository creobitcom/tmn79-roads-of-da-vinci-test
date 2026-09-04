using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data
{
    [InlineEditor]
    public class TooltipData : ScriptableObject, ITimeManagerSO
    {
        [field: SerializeField, Space(10f)]
        public string TooltipObjectName { get; set; }

        [field: SerializeField, Space(10f)]
        public string TooltipObjectNameSuffix { get; private set; }

        [field: SerializeField, Space(10f)]
        public string TooltipInputText { get; set; }
        
        [field: SerializeField, Space(10f)]
        public string TooltipOutputText { get; set; }
        
        [field: SerializeField, TextArea, Space(10f)]
        public string TooltipObjectDescription { get; private set; }
        
        [field: SerializeField, TextArea, Space(10f)]
        public string CantReachThisObjectText { get; private set; }
        
        [field: SerializeField, Space(10f)]
        public Sprite TooltipObjectIcon { get; private set; }
    }
}