using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.Transportable;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    public class TransportableObjectDataSO : ObjectDataSO
    {
        [field: SerializeField]
        [field: HideLabel]
        [field: InlineProperty]
        public TransportableSettings TransportableSettings { get; private set; }
    }
}