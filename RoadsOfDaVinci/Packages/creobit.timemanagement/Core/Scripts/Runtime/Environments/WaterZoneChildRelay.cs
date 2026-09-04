using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    public sealed class WaterZoneChildRelay : MonoBehaviour
    {
        private WaterZone _zone;

        private WaterZone Zone => _zone != null ? _zone : _zone = GetComponentInParent<WaterZone>(true);

        private void Awake()
        {
            _zone = GetComponentInParent<WaterZone>(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            Zone?.RelayEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            Zone?.RelayExit(other);
        }
    }
}
