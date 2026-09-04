using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    public class AccelerationZone : MonoBehaviour
    {
        [Title("Acceleration Zone Settings")] 
        [SerializeField] private bool _state = true;
        [SerializeField] private float _speedMultiplier = 0f;

        private readonly HashSet<MovableObjectView> _unitsInside = new();

        public bool State => _state;
        public float SpeedMultiplier => _speedMultiplier;

        private void OnDisable() => ClearZoneEffects();
        private void OnDestroy() => ClearZoneEffects();
        private void OnTriggerEnter(Collider other) => HandleEnter(other.gameObject);
        private void OnTriggerExit(Collider other) => HandleExit(other.gameObject);
        
        [Button("Toggle State")]
        public void SetState(bool value)
        {
            if (_state == value) return;
            _state = value;

            foreach (var unit in _unitsInside)
            {
                if (unit != null)
                {
                    unit.SetSpeed(unit.CurrentSpeed, false);
                }
            }
        }

        private void HandleEnter(GameObject obj)
        {
            if (obj.TryGetComponent<MovableObjectView>(out var unit))
            {
                if (_unitsInside.Add(unit))
                {
                    unit.AddAccelerationZone(this);
                }
            }
        }

        private void HandleExit(GameObject obj)
        {
            if (obj.TryGetComponent<MovableObjectView>(out var unit))
            {
                if (_unitsInside.Remove(unit))
                {
                    unit.RemoveAccelerationZone(this);
                }
            }
        }

        private void ClearZoneEffects()
        {
            foreach (var unit in _unitsInside)
            {
                if (unit != null)
                {
                    unit.RemoveAccelerationZone(this);
                }
            }

            _unitsInside.Clear();
        }
    }
}