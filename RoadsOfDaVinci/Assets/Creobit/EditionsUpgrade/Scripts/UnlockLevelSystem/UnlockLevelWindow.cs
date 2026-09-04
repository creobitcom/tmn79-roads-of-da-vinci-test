using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
using Yodo1.MAS;
#endif

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/UnlockLevelWindow")]
    public class UnlockLevelWindow : MonoBehaviour
    {
        public enum OfferKind
        {
            Start,
            Season,
            OneMinute
        }

#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
        [SerializeField] private Yodo1RewardManager _yodo1RewardManager;
#endif
        [SerializeField] private GameObject _loaderObject;
        [SerializeField] private GameObject _tryAgainWindow;
        [SerializeField] private LocalizeStringEvent _addCountTextEvent;
        private UnlockLevelItem _levelItem;

#region Window2

        [SerializeField] private GameObject[] _startObjects;
        [SerializeField] private GameObject[] _sessionObjects;
        [SerializeField] private GameObject[] _oneMinuteObjects;

        private GameObject[] _currentObjectSet;
        [SerializeField] private float _timer = 60f;
        [SerializeField] private TMP_Text _timerText;
        private bool _isOneMinuteOffer;
        private UnlockLevelManager _currentUnlockLevelManager;
#endregion

        public string AddsCountString { get; private set; }

        public event Action CurrentObjectSetChanged;

        protected void Awake()
        {
            _currentUnlockLevelManager ??= FindObjectOfType<UnlockLevelManager>();
#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
            _yodo1RewardManager ??= FindObjectOfType<Yodo1RewardManager>();
#endif
        }

        private void OnEnable()
        {
            _isOneMinuteOffer = false;
#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
            _yodo1RewardManager.RequestRewardedAds();
            Yodo1U3dRewardAd.GetInstance().OnAdEarnedEvent += OnRewardAdEarnedEvent;
#endif

            var firstWindowNumber = PlayerPrefs.GetInt("FirstWindowNumber", _currentUnlockLevelManager
                .FreeLevelsCount + 1);
            PlayerPrefs.SetInt("UnlockLevelWindow", _levelItem.Index);
            var windowNumber = PlayerPrefs.GetInt("UnlockLevelWindow", _levelItem.Index);

            switch (GetScheduledOffer(firstWindowNumber, windowNumber))
            {
                case OfferKind.Start:
                    _currentObjectSet = _startObjects;
                    break;
                case OfferKind.OneMinute:
                    _currentObjectSet = _oneMinuteObjects;
                    _isOneMinuteOffer = true;
                    if (PlayerPrefs.HasKey("Timer" + _levelItem.Index))
                    {
                        if (DateTime.Now >= DateTime.Parse(PlayerPrefs.GetString("StartTime" + _levelItem.Index)))
                        {
                            _currentObjectSet = _sessionObjects;
                            _isOneMinuteOffer = false;
                        }
                        else
                        {
                            _timer = DateTime.Now.Subtract(DateTime.Parse(PlayerPrefs.GetString("StartTime" + _levelItem.Index))).Seconds;
                            _timer *= -1;
                            Debug.Log($"Timer = {_timer}");
                        }
                    }
                    else
                    {
                        PlayerPrefs.SetInt("Timer" + _levelItem.Index, 60);
                        PlayerPrefs.SetString("StartTime" + _levelItem.Index, DateTime.Now.AddSeconds(60).ToString());
                        _timer = 60f;
                    }
                    break;
                default:
                    _currentObjectSet = _sessionObjects;
                    PlayerPrefs.SetFloat("Timer", _levelItem.Index);
                    break;
            }

            foreach (var startObject in _startObjects)
            {
                startObject.SetActive(false);
            }
            foreach (var startObject in _sessionObjects)
            {
                startObject.SetActive(false);
            }
            foreach (var startObject in _oneMinuteObjects)
            {
                startObject.SetActive(false);
            }

            foreach (var currentObject in _currentObjectSet)
            {
                currentObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
            Yodo1U3dRewardAd.GetInstance().OnAdEarnedEvent -= OnRewardAdEarnedEvent;
#endif
            // var windowNumber = PlayerPrefs.GetInt("UnlockLevelWindow", 0);
            PlayerPrefs.SetInt("UnlockLevelWindow", _levelItem.Index);
        }

        private void Update()
        {
            if (!_isOneMinuteOffer)
                return;

            _timer -= Time.deltaTime;
            if (_timer < 0)
            {
                _currentObjectSet = _sessionObjects;
                _isOneMinuteOffer = false;
                foreach (var startObject in _startObjects)
                {
                    startObject.SetActive(false);
                }
                foreach (var startObject in _sessionObjects)
                {
                    startObject.SetActive(false);
                }
                foreach (var startObject in _oneMinuteObjects)
                {
                    startObject.SetActive(false);
                }

                foreach (var currentObject in _currentObjectSet)
                {
                    currentObject.SetActive(true);
                }
                CurrentObjectSetChanged?.Invoke();
            }
            _timerText.text = $":{Mathf.FloorToInt(_timer).ToString()}";
        }

        public static OfferKind GetScheduledOffer(int firstWindowNumber, int windowNumber)
        {
            if (windowNumber == firstWindowNumber)
                return OfferKind.Start;

            var oneMinuteDivision = windowNumber > firstWindowNumber + 10 ? 5 : 4;
            return (windowNumber - firstWindowNumber) % oneMinuteDivision == 0
                ? OfferKind.OneMinute
                : OfferKind.Season;
        }

        public void OpenWindow(UnlockLevelItem unlockLevelItem)
        {
            _levelItem = unlockLevelItem;
#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
            AddsCountString = $"({_levelItem.WatchedAdsCount}/{_levelItem.TotalAdsCount})";
#endif
            gameObject.SetActive(true);
        }

        public void ResetIfNotFullyWatched()
        {
            if (_levelItem.WatchedAdsCount < _levelItem.TotalAdsCount)
            {
                _levelItem.ResetWatchedAds();
            }
        }
        
        public void ShowRewardedAds()
        {
#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
            if (Yodo1U3dRewardAd.GetInstance().IsLoaded())
            {
                // OnRewardWatched();
                _yodo1RewardManager.ShowReward();

                _loaderObject.SetActive(false);

                if (_levelItem.WatchedAdsCount >= _levelItem.TotalAdsCount)
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                _loaderObject.SetActive(true);
                _yodo1RewardManager.RequestRewardedAds();
                Yodo1U3dRewardAd.GetInstance().OnAdLoadedEvent += OnRewardAdLoadedEvent;
                double retryDelay = Math.Pow(2, Math.Min(6, _yodo1RewardManager.retryAttempt));
                Invoke(nameof(OpenTryAgainWindow), Mathf.Max((float)retryDelay, 4f));
            }
#endif
        }

#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
        private void OnRewardAdLoadedEvent(Yodo1U3dRewardAd ad)
        {
            CancelInvoke();
            // OnRewardWatched();
            _yodo1RewardManager.ShowReward();

            Debug.Log("[Yodo1 Mas] OnRewardAdLoadedEvent event received");
            Yodo1U3dRewardAd.GetInstance().OnAdLoadedEvent -= OnRewardAdLoadedEvent;

            // if (_levelItem.WatchedAdsCount >= _levelItem.TotalAdsCount)
            // {
            //     gameObject.SetActive(false);
            // }
        }
#endif

        private void OnRewardWatched()
        {
#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
            // _yodo1RewardManager.ShowReward();

            _loaderObject.SetActive(false);

            _levelItem.OnAddWatched();
            AddsCountString = $"({_levelItem.WatchedAdsCount}/{_levelItem.TotalAdsCount})";
            _addCountTextEvent.RefreshString();
            if (_levelItem.WatchedAdsCount >= _levelItem.TotalAdsCount)
            {
                gameObject.SetActive(false);
            }
#endif
        }

        public void OnAllLevelsBuy()
        {
            PlayerPrefs.SetString(UnlockLevelManager.AllLevelsBuyKey, "true");
            PlayerPrefs.Save();
        }

#if (UNITY_ANDROID || UNITY_IOS) && HAS_AD 
        private void OpenTryAgainWindow()
        {
            Yodo1U3dRewardAd.GetInstance().OnAdLoadedEvent -= OnRewardAdLoadedEvent;
            _loaderObject.SetActive(false);
            _tryAgainWindow.SetActive(true);
        }
        
        private void OnRewardAdEarnedEvent(Yodo1U3dRewardAd ad)
        {
            Debug.Log("[Yodo1 Mas] OnRewardAdEarnedEvent event received");
            // Add your reward code here
            OnRewardWatched();
        }
#endif

        public GameObject GetCurrentBuyButton()
        {
            return _currentObjectSet[2];
        }
    }
}
