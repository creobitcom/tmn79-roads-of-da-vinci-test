using System;
using System.Collections.Generic;
using Creobit.Bootstrap.Core.Scripts.Runtime.Splash;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
#if HAS_AD
using Yodo1.MAS.PrivacyPolicy;
#endif

namespace Creobit.EditionsUpgrade
{
    public interface IPrivacyPolicyManager
    {
        public UniTask Init();
    }
    
    [AddComponentMenu("Creobit/EditionsUpgrade/PrivacyPolicyManager")]
    public class PrivacyPolicyManager : MonoBehaviour, IPrivacyPolicyManager
    {
        private const string PrivacyAcceptedKey = "PrivacyAcceptedKey";
        [SerializeField] private List<GameObject> _blockedObjects = new List<GameObject>();
        [SerializeField] private PrivacyWindow8floor _privacyWindow8Floor;
        public bool Inited { get; private set; }
        private bool _accepted
        {
            get => PlayerPrefs.GetInt(PrivacyAcceptedKey) > 0;
            set
            {
                PlayerPrefs.SetInt(PrivacyAcceptedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
        
        private void Initialization()
        {
#if !UNITY_ANALYTICS
            UnlockBlockedObjects();
            return;
#endif
            if (!_accepted)
            {
                ShowPrivacyWindow();
            }
            else
            {
                UnlockBlockedObjects();
            }
            
            _privacyWindow8Floor.OnAccept += UnlockBlockedObjects;
        }

        private void OnDestroy()
        {
            _privacyWindow8Floor.OnAccept -= UnlockBlockedObjects;
        }

        private void ShowPrivacyWindow()
        {
#if HAS_AD
            ShowYodo1Privacy();
#elif UNITY_ANALYTICS
            Show8floorPrivacy();
#else
            UnlockBlockedObjects();
#endif
        }

        private void UnlockBlockedObjects()
        {
            _accepted = true;
            
            foreach (var blockedObject in _blockedObjects)
            {
                blockedObject.SetActive(true);
            }
#if !HAS_AD && UNITY_ANALYTICS
            _privacyWindow8Floor.gameObject.SetActive(false);
#endif
            Inited = true;
        }
    
#if HAS_AD
        [ContextMenu("ShowYodo1Privacy")]
        private void ShowYodo1Privacy()
        {
            Yodo1.MAS.PrivacyPolicy.Yodo1PrivacyPolicy.ShowPrivacyPopUp((userAge) =>
            {
                bool isCoppaChild = (userAge < 13);
                bool userConsent = (userAge >= 16);
                bool doNotSell = (userAge < 16);

                Yodo1.MAS.Yodo1U3dMas.SetCOPPA(isCoppaChild);
                Yodo1.MAS.Yodo1U3dMas.SetGDPR(userConsent);
                Yodo1.MAS.Yodo1U3dMas.SetCCPA(doNotSell);
                
                Yodo1.MAS.Yodo1U3dMas.InitializeMasSdk();
                UnlockBlockedObjects();
            });
        }
#endif

        private void Show8floorPrivacy()
        {
            _privacyWindow8Floor.gameObject.SetActive(true);
        }

        public async UniTask Init()
        {
            gameObject.SetActive(true);
            Inited = false;
            Initialization();
            while (!Inited)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            }
        }
    }
}