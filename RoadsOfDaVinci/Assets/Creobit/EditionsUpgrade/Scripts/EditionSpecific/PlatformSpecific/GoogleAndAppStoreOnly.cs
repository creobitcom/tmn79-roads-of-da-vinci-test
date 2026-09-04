using System;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/GoogleAndAppStoreOnly")]
    public class GoogleAndAppStoreOnly : MonoBehaviour
    {
        protected virtual void Awake()
        {
#if !UNITY_IOS && !UNITY_ANDROID && !MAC_APPSTORE
            DestroyRealisation();
#elif UNITY_ANDROID
            if (!IsGooglePlay())
            {
                DestroyRealisation();
            }
#endif
        }

        private bool IsGooglePlay()
        {
#if GOOGLE_PLAY || HUAWEI
            return true;
#endif
            return false;
        }

        protected virtual void DestroyRealisation()
        {
            Destroy(gameObject);
        }
    }
}