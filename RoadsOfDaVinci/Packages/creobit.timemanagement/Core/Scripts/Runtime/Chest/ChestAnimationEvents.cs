using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Chest
{
    public class ChestAnimationEvents : MonoBehaviour
    {
        [SerializeField] private ChestService _chestService;

        public void DeadEvent()
        {
            if (_chestService != null)
                _chestService.OnDeadAnimationEvent();
        }
    }
}