using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.HumoristService
{
    public class HumoristAttackArea : MonoBehaviour
    {
        [SerializeField] private HumoristAttackService _attackService;

        private void Awake()
        {
            _attackService ??= GetComponentInParent<HumoristAttackService>();
        }

        private void OnTriggerEnter(Collider other)
        {
            _attackService?.TryAttack(other);
        }
    }
}
