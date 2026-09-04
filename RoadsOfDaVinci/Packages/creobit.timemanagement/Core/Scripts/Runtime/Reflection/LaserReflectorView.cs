using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public sealed class LaserReflectorView : Rotatable, ILaserReflector
    {
        public Vector3 Reflect(Vector3 direction, Vector3 normal)
        {
            return Vector3.Reflect(direction, normal).normalized;
        }
    }
}