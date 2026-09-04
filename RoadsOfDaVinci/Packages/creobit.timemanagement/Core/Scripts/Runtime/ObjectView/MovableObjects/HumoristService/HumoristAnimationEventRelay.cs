using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.HumoristService
{
    public class HumoristAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private HumoristAttackService _attackService;

        private void Awake()
        {
            _attackService ??= GetComponentInParent<HumoristAttackService>();
        }

        public void StunObject()
        {
            _attackService?.StunObject();
        }

        public void StunAnimFinished()
        {
            _attackService?.StunAnimFinished();
        }
    }
}
