using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/OnlyDontMac")]
    public class OnlyDontMac : MonoBehaviour
    {
#if UNITY_STANDALONE_OSX
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif          
    }

}