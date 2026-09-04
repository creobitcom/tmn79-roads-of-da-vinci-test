using System.Diagnostics.CodeAnalysis;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public abstract class ResourceBaseSO : ScriptableObject, ITimeManagerSO
    {
        [field: SerializeField]
        [field: PreviewField]
        public Sprite Image { get; private set; }
        
        [field: SerializeField]
        public Sprite PanelResourcesSprite {get; private set;}
        
        [field: SerializeField]
        public string Name { get; protected set; }
        
        [field: SerializeField]
        public string Description { get; protected set; }
    }
}
