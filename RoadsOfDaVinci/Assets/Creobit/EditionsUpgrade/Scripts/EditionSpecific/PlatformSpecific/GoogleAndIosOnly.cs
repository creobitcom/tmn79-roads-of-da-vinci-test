using System;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/GoogleAndIosOnly")]
    public class GoogleAndIosOnly : MonoBehaviour
    {
        protected virtual void Awake()
        {
#if !UNITY_IOS && !UNITY_ANDROID
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
            if (Application.isEditor)
            {
                return Application.identifier.Contains("google");
            }
            
            var cls = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var currentActivity = cls.GetStatic<AndroidJavaObject>("currentActivity");
            var applicationContext = currentActivity.Call<AndroidJavaObject>("getApplicationContext");

            return IsPackageInstalled("com.google.android.gms", applicationContext) && Application.identifier.Contains("google");
        }
        
        private static bool IsPackageInstalled(string packageName, AndroidJavaObject context) {
            var packageManager = context.Call<AndroidJavaObject>("getPackageManager");
            try {
                packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                return true;
            } catch (AndroidJavaException e) {
                return false;
            }
        }

        protected virtual void DestroyRealisation()
        {
            Destroy(gameObject);
        }
    }
}