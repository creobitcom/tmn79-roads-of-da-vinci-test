using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuideTargetView : MonoBehaviour
    {
        [field: SerializeField]
        public GuideSettings GuideSettings { get; private set; }
    }
}