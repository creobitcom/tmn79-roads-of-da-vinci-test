using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.ScanSweep
{
    [CreateAssetMenu(fileName = "ScanSweepSettings", menuName = "8floor/TimeManager/Gameplay/Scan Sweep Settings")]
    public class ScanSweepSettings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Общий пресет развёртки. Один ассет можно повесить на все сканируемые объекты.")]
        private ScanSweepParams sweep = new ScanSweepParams();

        public ScanSweepParams Sweep => sweep;
    }
}
