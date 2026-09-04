using UltEvents;
using UnityEngine;
using UnityEngine.Events;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public sealed class LaserReceiverView : MonoBehaviour, ILaserReceiver
    {
        [SerializeField] private UltEvent onLaserEnter;
        [SerializeField] private UltEvent onLaserExit;
        [SerializeField] private bool isTransparent;

        private int _hits;

        public bool IsTransparent => isTransparent;

        public void OnLaserEnter()
        {
            _hits++;

            if (_hits == 1)
            {
                onLaserEnter.Invoke();
            }
        }

        public void OnLaserExit()
        {
            _hits--;

            if (_hits <= 0)
            {
                _hits = 0;
                onLaserExit.Invoke();
            }
        }
    }
}