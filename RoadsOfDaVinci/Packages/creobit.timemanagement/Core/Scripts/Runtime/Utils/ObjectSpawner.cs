using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils
{
    public class ObjectSpawner : MonoBehaviour
    {

        [field: SerializeField] public Transform SpawnPoint { get; set; }
        [field: SerializeField] public Camera CurrentCamera { get; set; }
        [field: SerializeField] public bool SpawnAtUnitTarget { get; set; }
        [field: SerializeField] public MovableObjectView UnitView { get; set; }
        [field: SerializeField] public float TowardUnitDistance { get; set; }
        [field: SerializeField] public Vector3 UnitTargetOffset { get; set; }

        private void Awake()
        {
            if (SpawnAtUnitTarget && UnitView == null)
            {
                UnitView = GetComponentInParent<MovableObjectView>();
            }
        }

        public void Spawn(GameObject objectToSpawn)
        {
            Instantiate(objectToSpawn, ResolveSpawnPosition(), Quaternion.identity);
        }

        //TODO: Подумать, как получать камеру без введения вручную и без Camera.main
        public void SpawnOnGUI(GameObject objectToSpawn)
        {
            Transform spawnPoint = SpawnPoint ? SpawnPoint : transform;
            Camera camera = CurrentCamera ? CurrentCamera : Camera.main;
            Vector3 spawnPosition = camera.ScreenToWorldPoint(spawnPoint.position);
            Instantiate(objectToSpawn, spawnPosition, Quaternion.identity);
        }

        private Vector3 ResolveSpawnPosition()
        {
            if (!SpawnAtUnitTarget || UnitView == null)
            {
                Transform spawnPoint = SpawnPoint ? SpawnPoint : transform;
                return spawnPoint.position;
            }

            Vector3 position = UnitView.currentDestination;
            Vector3 toUnit = UnitView.transform.position - position;
            toUnit.z = 0f;

            if (toUnit.sqrMagnitude > 0.0001f)
            {
                position += toUnit.normalized * TowardUnitDistance;
            }

            return position + UnitTargetOffset;
        }

    }
}
