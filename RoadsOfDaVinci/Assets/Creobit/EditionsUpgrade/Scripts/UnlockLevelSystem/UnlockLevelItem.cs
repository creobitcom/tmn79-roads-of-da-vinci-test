using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/UnlockLevelItem")]
    public class UnlockLevelItem : MonoBehaviour, IReactToClick
    {
        public const string UnlockedLevelKey = "UnlockedLevelKey";
#if TK2D
        [SerializeField] private tk2dUIItem _button;
        public tk2dUIItem Button => _button;
#elif CREOBIT
        [SerializeField] private MapSpotView _spotView;
#else
        [SerializeField] private Button _button;
        public Button Button => _button;
#endif

#if !CREOBIT
        [SerializeField] private FakeLevelButton _fakeLevelButton;
#endif

        private int _index;
        public int Index => _index;

        private Button _fakeButton;

        public event Action<int> OnInteract;
        public event Action<int> OnUnlocked;
        public int TotalAdsCount { get; private set; }
        public int WatchedAdsCount { get; private set; }

        private bool _unlocked
        {
            get => PlayerPrefs.GetInt(UnlockedLevelKey + _index) > 0 || PlayerPrefs.HasKey(UnlockLevelManager.AllLevelsBuyKey);
            set
            {
                PlayerPrefs.SetInt(UnlockedLevelKey + _index, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public bool Unlocked => _unlocked;

        private void OnValidate()
        {
#if TK2D
            _button ??= GetComponent<tk2dUIItem>();   
#elif CREOBIT
            _spotView ??= GetComponent<MapSpotView>();
#else
            _button ??= GetComponent<Button>();
#endif
        }

#if !CREOBIT
        protected void Awake()
        {

            var fakeLevelObject = Instantiate(_fakeLevelButton, transform);
            _fakeButton = fakeLevelObject.FakeButton;
            _fakeButton.onClick.AddListener(InvokeInteraction);
        }

        private void InvokeInteraction()
        {
            OnInteract?.Invoke(_index);
        }
#endif

        public void Initialization(int index, int totalAds)
        {
            _index = index;
            TotalAdsCount = totalAds;
        }

        public void OnAddWatched()
        {
            WatchedAdsCount++;
            if (WatchedAdsCount >= TotalAdsCount)
            {
                OpenLevel();
            }
        }

        public void ResetWatchedAds()
        {
            if (!_unlocked)
            {
                WatchedAdsCount = 0;
            }
        }

        public void SetUnlockedStatus(bool state)
        {
            _unlocked = state;
        }

        public void OpenLevel()
        {
            _unlocked = true;
            OnUnlocked?.Invoke(_index);
            CallClick();
        }

        public void CallClick()
        {
#if TK2D
            _button.SimulateClick();
#elif CREOBIT
            _spotView.OnClick();
#else
            _button.onClick.Invoke();
#endif      
        }


        public void OnClick()
        {
            OnInteract?.Invoke(_index);
        }
    }
}