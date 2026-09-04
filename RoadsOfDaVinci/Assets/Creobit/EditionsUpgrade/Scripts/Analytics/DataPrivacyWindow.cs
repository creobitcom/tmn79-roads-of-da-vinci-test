using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_ANALYTICS
using Cysharp.Threading.Tasks;
using Unity.Services.Analytics;
#endif
using Unity.Services.Core;
using Creobit.Bootstrap.Core.Scripts.Runtime.Analytics;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/DataPrivacy/DataPrivacyWindow")]
    public class DataPrivacyWindow : MonoBehaviour
    {
        private const string FallbackPrivacyUrl = "https://unity3d.com/legal/privacy-policy";

        private void Start()
        {
#if !TK2D
            if (TryGetComponent<BoxCollider>(out var boxCollider))
            {
                boxCollider.enabled = false;
            }
#endif
#if UNITY_ANALYTICS
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                UnityServices.InitializeAsync();
            }
#endif
        }

        public void OpenDataPrivacy()
        {
#if UNITY_ANALYTICS
            Application.OpenURL(ResolvePrivacyUrl());
#endif
        }

        public void DisableAnalytics()
        {
#if UNITY_ANALYTICS
            SetConsent(false);
#endif
        }

        public void EnableAnalytics()
        {
#if UNITY_ANALYTICS
            SetConsent(true);
#endif
        }

#if UNITY_ANALYTICS
        private static void SetConsent(bool isGiven)
        {
            PlayerPrefs.SetInt(AnalyticsRecorder.ConsentPrefKey, isGiven ? 1 : 0);
            PlayerPrefs.Save();

            ApplyConsentToService(isGiven).Forget();
        }

        private static async UniTaskVoid ApplyConsentToService(bool isGiven)
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (isGiven)
                {
                    AnalyticsService.Instance.StartDataCollection();
                }
                else
                {
                    AnalyticsService.Instance.StopDataCollection();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DataPrivacy] Согласие сохранено, но применить его в UGS не удалось: {exception.Message}");
            }
        }

        private static string ResolvePrivacyUrl()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized)
                {
                    return AnalyticsService.Instance.PrivacyUrl;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DataPrivacy] Ссылка из UGS недоступна: {exception.Message}");
            }

            return FallbackPrivacyUrl;
        }
#endif
    }
}
