using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/GoogleAndIosComponent")]
    public class GoogleAndIosComponent : GoogleAndIosOnly
    {
        [SerializeField] private Component _component;
        
        protected sealed override void DestroyRealisation()
        {
            Destroy(_component);
            Destroy(this);
        }
    }
}