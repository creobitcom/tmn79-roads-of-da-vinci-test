using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils
{
    public class ObjectDestroyer : MonoBehaviour
    {
        private void Awake()
        {
            DestroyObject();
        }

        public void DestroyObject()
        {
            Destroy(gameObject);
        }
    }
}