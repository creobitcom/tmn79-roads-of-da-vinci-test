using System;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/DesktopOnly")]
    public class DesktopOnly : MonoBehaviour
    {
#if UNITY_IOS || UNITY_ANDROID
        private void Awake()
        {
            DestroyImmediate(gameObject);
        }
#endif  
    }
}