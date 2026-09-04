using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public interface ILaserReflector
    {
        Vector3 Reflect(Vector3 direction, Vector3 normal);
    }
}