using System;
using UnityEngine;
#if HAS_AD
using Yodo1.MAS;
#endif

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/Yodo1RewardManager")]
    public class Yodo1RewardManager : GoogleAndAppStoreOnly
    {
        public int retryAttempt = 0;
        
        protected override void DestroyRealisation()
        {
            Destroy(this);
        }

#if HAS_AD
        private void Start()
        {
            Init();
        }

        private void Init()
        {
            Yodo1U3dRewardAd.GetInstance().autoDelayIfLoadFail = true;
            
            Yodo1U3dMasCallback.OnSdkInitializedEvent += (success, error) =>
            {
                Debug.Log(
                    Yodo1U3dMas.TAG + "OnSdkInitializedEvent, success:" + success + ", error: " + error.ToString());
                if (success)
                {
                    InitializeRewardedAds();
                }
                else
                {
                    Debug.Log("[Yodo1 Mas] The initialization has failed");
                }
            };

            var config = new Yodo1AdBuildConfig();
            Yodo1U3dMas.SetAdBuildConfig(config);

            Yodo1U3dMas.InitializeMasSdk();
        }

        private void InitializeRewardedAds()
        {
            // Instantiate
            Yodo1U3dRewardAd.GetInstance();

            // Ad Events
            Yodo1U3dRewardAd.GetInstance().OnAdLoadedEvent += OnRewardAdLoadedEvent;
            Yodo1U3dRewardAd.GetInstance().OnAdLoadFailedEvent += OnRewardAdLoadFailedEvent;
            Yodo1U3dRewardAd.GetInstance().OnAdOpenedEvent += OnRewardAdOpenedEvent;
            Yodo1U3dRewardAd.GetInstance().OnAdOpenFailedEvent += OnRewardAdOpenFailedEvent;
            Yodo1U3dRewardAd.GetInstance().OnAdClosedEvent += OnRewardAdClosedEvent;
            // Yodo1U3dRewardAd.GetInstance().OnAdEarnedEvent += OnRewardAdEarnedEvent;
            RequestRewardedAds();
        }

        public void RequestRewardedAds()
        {
            if (!Yodo1U3dRewardAd.GetInstance().IsLoaded())
                Yodo1U3dRewardAd.GetInstance().LoadAd();
        }

        public void ShowReward()
        {
            Yodo1U3dRewardAd.GetInstance().ShowAd();
            //Debug.Log("SHow");
        }

        private void OnRewardAdLoadedEvent(Yodo1U3dRewardAd ad)
        {
            retryAttempt = 0;
            Debug.Log("[Yodo1 Mas] OnRewardAdLoadedEvent event received");
        }

        private void OnRewardAdLoadFailedEvent(Yodo1U3dRewardAd ad, Yodo1U3dAdError adError)
        {
            retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));
            Invoke(nameof(RequestRewardedAds), (float)retryDelay);
            Debug.Log("[Yodo1 Mas] OnRewardAdLoadFailedEvent event received with error: " + adError.ToString());
        }

        private void OnRewardAdOpenedEvent(Yodo1U3dRewardAd ad)
        {
            Debug.Log("[Yodo1 Mas] OnRewardAdOpenedEvent event received");
        }

        private void OnRewardAdOpenFailedEvent(Yodo1U3dRewardAd ad, Yodo1U3dAdError adError)
        {
            Debug.Log("[Yodo1 Mas] OnRewardAdOpenFailedEvent event received with error: " + adError.ToString());
            // Load the next ad
            RequestRewardedAds();
        }

        private void OnRewardAdClosedEvent(Yodo1U3dRewardAd ad)
        {
            Debug.Log("[Yodo1 Mas] OnRewardAdClosedEvent event received");
            // Load the next ad
            RequestRewardedAds();
        }
        //
        // private void OnRewardAdEarnedEvent(Yodo1U3dRewardAd ad)
        // {
        //     Debug.Log("[Yodo1 Mas] OnRewardAdEarnedEvent event received");
        //     // Add your reward code here
        // }
#endif
    }
}