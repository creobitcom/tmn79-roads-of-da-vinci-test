using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/MacOnly")]
    public class MacOnly : MonoBehaviour
    {
#if !UNITY_STANDALONE_OSX
        private void Awake()
        {
            DestroyImmediate(gameObject);
        }
#endif          
    }
}