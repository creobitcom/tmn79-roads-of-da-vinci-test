using System;
using System.Linq;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/AppleOnly")]
    public class AppleOnly : MonoBehaviour
    {
#if !UNITY_IOS && !UNITY_STANDALONE_OSX
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif
    }
}