using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    [CreateAssetMenu(menuName = "Create ObjectTagSO", fileName = "ObjectTagSO")]
    public abstract class ObjectTagSO : ScriptableObject, ITimeManagerSO
    {
        [field: SerializeField]
        public string Name { get; protected set; }
    }
}