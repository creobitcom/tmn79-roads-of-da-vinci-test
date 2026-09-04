using System.Collections.Generic;
using System.Linq;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using R3;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using VContainer;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/UnlockLevelManager")]
    public class UnlockLevelManager : MonoBehaviour
    {
        public const string AllLevelsBuyKey = "AllLevelsBuyKey";
        public const string LastUnlockedLevelIndexKey = "LastUnlockedLevelIndex";
        public const string LastVisitedLevelIndexKey = "LastVisitedLevelIndexKey";

        public static int LastKnownFreeLevelsCount { get; private set; } = 6;

        public static bool IsLevelPlayableWithoutPurchase(int levelNumber)
        {
            return levelNumber <= LastKnownFreeLevelsCount
                   || PlayerPrefs.HasKey(AllLevelsBuyKey)
                   || PlayerPrefs.GetInt(UnlockLevelItem.UnlockedLevelKey + levelNumber) > 0;
        }

        public static void RaiseUnlockProgress(int levelNumber)
        {
            if (PlayerPrefs.GetInt(LastUnlockedLevelIndexKey) >= levelNumber)
            {
                return;
            }

            PlayerPrefs.SetInt(LastUnlockedLevelIndexKey, levelNumber);
            PlayerPrefs.Save();
        }

        public enum LevelInteractions
        {
            Locked,
            Unlocked,
        }

        [SerializeField] private List<UnlockLevelItem> _unlockLevels = new List<UnlockLevelItem>();
        public List<UnlockLevelItem> UnlockLevels => _unlockLevels;

        [SerializeField] private LevelInteractions _levelInteraction;
        public LevelInteractions LevelInteraction => _levelInteraction;
        [SerializeField] private int _freeLevelsCount = 7;
        public int FreeLevelsCount => _freeLevelsCount;

        [SerializeField] private UnlockLevelWindow _unlockLevelWindow;
        [SerializeField] private RateUsWindow _rateUsWindow;
        [SerializeField] private Transform _windowsParent;

        private void ExpandToParent(RectTransform rect)
        {
            if (_windowsParent == null || rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        [SerializeField] private int _rateUsShowLevelFirst = 6;
        [SerializeField] private int _rateUsShowLevelSecond = 14;
        [SerializeField] private bool _disableAutoValidation;

#if CREOBIT
        [SerializeField] private GameObject _mapBackground;

        private ISaveController _saveController;
#endif

        public void OnValidate()
        {
            SetUnlockLevels();
        }

#if CREOBIT

        [Inject]
        private void Construct(ISaveController saveController)
        {
            _saveController = saveController;
        }
#endif

        protected void Awake()
        {
            LastKnownFreeLevelsCount = _freeLevelsCount;
            SpawnPrefabs();
            SetUnlockLevels();
            Subscribe();

#if CREOBIT
            Observable.EveryValueChanged(_mapBackground, (x) => x.activeSelf)
            .Where((x) => x)
            .Subscribe((_) => OnMapActivated()).AddTo(this);
#endif

            if (!PlayerPrefs.HasKey(LastUnlockedLevelIndexKey))
            {
                PlayerPrefs.SetInt(LastUnlockedLevelIndexKey, 1);
#if TOYMAN
                if (PlayerPrefs.GetInt(LastUnlockedLevelIndexKey) < TimeManagerEngine.Player.I.MaxAvailableLevel)
                {
                    PlayerPrefs.SetInt(LastUnlockedLevelIndexKey, TimeManagerEngine.Player.I.MaxAvailableLevel);
                }
#endif
                PlayerPrefs.Save();
                for (int i = 0; i < _unlockLevels.Count; i++)
                {
                    if (i < PlayerPrefs.GetInt(LastUnlockedLevelIndexKey))
                    {
                        _unlockLevels[i].SetUnlockedStatus(true);
                    }
                }

                var lastUnlockedLevel = _unlockLevels[PlayerPrefs.GetInt(LastUnlockedLevelIndexKey) - 1];

                if (lastUnlockedLevel.Index > _freeLevelsCount)
                {
                    lastUnlockedLevel.SetUnlockedStatus(false);
                }

            }

#if PREMIUM
            _freeLevelsCount = _unlockLevels.Count;
            if (PlayerPrefs.HasKey(AllLevelsBuyKey))
            {
                foreach (var level in Enumerable.Where(_unlockLevels, level => !level.Unlocked))
                {
                    level.SetUnlockedStatus(true);
                }

                return;
            }

            PlayerPrefs.SetString(AllLevelsBuyKey,"true");
            PlayerPrefs.Save();
#endif

        }
#if !CREOBIT
        private void OnEnable()
        {
            OnMapActivated();
        }
#endif

        private void OnMapActivated()
        {
            if (!PlayerPrefs.HasKey(LastUnlockedLevelIndexKey))
            {
                PlayerPrefs.SetInt(LastUnlockedLevelIndexKey, 1);
                PlayerPrefs.Save();
                return;
            }
#if TOYMAN
            if (PlayerPrefs.GetInt(LastUnlockedLevelIndexKey) > TimeManagerEngine.Player.I.MaxAvailableLevel)
            {
                PlayerPrefs.SetInt(LastUnlockedLevelIndexKey, TimeManagerEngine.Player.I.MaxAvailableLevel);
            }
#elif CREOBIT

            if (PlayerPrefs.GetInt(LastUnlockedLevelIndexKey) > _saveController.CurrentSaveData.LastUnlockedLevel)
            {
                PlayerPrefs.SetInt(LastUnlockedLevelIndexKey, _saveController.CurrentSaveData.LastUnlockedLevel);
            }
#endif

#if UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_WSA || UNITY_EDITOR

            if ((PlayerPrefs.GetInt(LastUnlockedLevelIndexKey) == _rateUsShowLevelFirst ||
                 PlayerPrefs.GetInt(LastUnlockedLevelIndexKey) == _rateUsShowLevelSecond) &&
                !PlayerPrefs.HasKey(RateUsWindow.RateUsPrefsKey))
            {
#if UNITY_ANDROID && !GOOGLE_PLAY && !HUAWEI
                return;    
#endif
                _rateUsWindow.ShowRateUs();
            }
#endif
        }


        private void SpawnPrefabs()
        {
            _unlockLevelWindow.gameObject.SetActive(false);
            _unlockLevelWindow = Instantiate(_unlockLevelWindow, _windowsParent);
            ExpandToParent(_unlockLevelWindow.transform as RectTransform);
            _unlockLevelWindow.transform.SetAsLastSibling();
            _rateUsWindow.gameObject.SetActive(false);
            _rateUsWindow = Instantiate(_rateUsWindow, _windowsParent);
            ExpandToParent(_rateUsWindow.transform as RectTransform);
            _rateUsWindow.transform.SetAsLastSibling();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void SetUnlockLevels()
        {
            var index = 0;

            if (_unlockLevels.Count > 0)
            {
                var missedLevels = _unlockLevels.Where(x => x == null).ToList();
                foreach (var unlockLevel in missedLevels)
                {
                    _unlockLevels.Remove(unlockLevel);
                }

            }

            if (!_disableAutoValidation)
            {
                var allLevelsArray = FindObjectsOfType<UnlockLevelItem>();

                for (int i = allLevelsArray.Length - 1; i >= 0; i--)
                {
                    if (Enumerable.All(_unlockLevels, x => x != allLevelsArray[i]))
                    {
                        _unlockLevels.Add(allLevelsArray[i]);
                    }
                }
            }

            var steps = 0;
            var firstPayLevel = _freeLevelsCount + 1;
            var isOdd = firstPayLevel % 2 != 0;

            if (_unlockLevels.Count <= 0)
            {
                return;
            }

            foreach (var unlockLevel in _unlockLevels)
            {
                index++;
                if (index % 2 == 0)
                {
                    unlockLevel.Initialization(index, 1);
                }
                else
                {
                    if (isOdd)
                    {
                        if (index != _freeLevelsCount + 1)
                        {
                            steps++;
                        }
                        unlockLevel.Initialization(index, steps < 3 || index == _freeLevelsCount ? 2 : 3);
                    }
                    else
                    {
                        steps++;
                        unlockLevel.Initialization(index, steps < 3 ? 2 : 3);
                    }

                    if (steps == 3)
                    {
                        steps = 0;
                    }
                }
            }
        }

        private void Subscribe()
        {
            foreach (var unlockLevel in _unlockLevels)
            {
                unlockLevel.OnInteract += UnlockLevelHasInteraction;
                unlockLevel.OnUnlocked += LevelWasUnlocked;
            }
        }

        private void Unsubscribe()
        {
            foreach (var unlockLevel in _unlockLevels)
            {
                unlockLevel.OnInteract -= UnlockLevelHasInteraction;
                unlockLevel.OnUnlocked -= LevelWasUnlocked;
            }
        }

        private void LevelWasUnlocked(int index)
        {
            var lastUnlockedIndex = PlayerPrefs.GetInt(LastUnlockedLevelIndexKey);

            if (index == lastUnlockedIndex)
            {
                var nextLevel = lastUnlockedIndex + 1;
                PlayerPrefs.SetInt(LastUnlockedLevelIndexKey, nextLevel);
                PlayerPrefs.Save();
            }

        }


        public void UnlockLevelHasInteraction(int index)
        {
#if !PREMIUM

            if (PlayerPrefs.HasKey(LastUnlockedLevelIndexKey))
            {
                if (index > PlayerPrefs.GetInt(LastUnlockedLevelIndexKey))
                {
                    return;
                }

#if TOYMAN
                Debug.LogError("index = " + index + " avail " + TimeManagerEngine.Player.I.MaxAvailableLevel);
                if (index > TimeManagerEngine.Player.I.MaxAvailableLevel)
                {
                    return;
                }
#endif
            }
#endif
            var currentLevel = _unlockLevels.First(x => x.Index == index);
            if (_levelInteraction == LevelInteractions.Unlocked)
            {
                if (currentLevel.Unlocked ||
#if TOYMAN
                    currentLevel.Index <= TimeManagerEngine.Player.I.MaxAvailableLevel)
#else              
                    currentLevel.Index <= PlayerPrefs.GetInt(LastUnlockedLevelIndexKey))
#endif
                {
                    currentLevel.CallClick();
                }
            }

            PlayerPrefs.SetInt(LastVisitedLevelIndexKey, currentLevel.Index);
            PlayerPrefs.Save();

            if (_levelInteraction == LevelInteractions.Unlocked)
            {
                return;
            }

            if (LevelCondition(currentLevel))
            {
                OpenLevel(currentLevel);
                return;
            }

            if (currentLevel.WatchedAdsCount < currentLevel.TotalAdsCount)
            {
#if TOYMAN
                OpenLevel(currentLevel);
                if (_levelInteraction == LevelInteractions.Locked)
                {
                    ShowUnlockWindow(currentLevel);
                }
#else
                ShowUnlockWindow(currentLevel);
#endif
            }
            else
            {
                OpenLevel(currentLevel);
            }

        }

        public bool LevelCondition(UnlockLevelItem currentLevel)
        {
            return currentLevel.Index <= _freeLevelsCount || PlayerPrefs.HasKey(AllLevelsBuyKey) || currentLevel.Unlocked;
        }

        public void ShowUnlockWindow(UnlockLevelItem unlockLevelItem)
        {
            _unlockLevelWindow.OpenWindow(unlockLevelItem);
        }

        private void OpenLevel(UnlockLevelItem unlockLevelItem)
        {
            unlockLevelItem.OpenLevel();
            PlayerPrefs.SetInt(LastVisitedLevelIndexKey, unlockLevelItem.Index);
            PlayerPrefs.Save();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UnlockLevelManager))]
    public class UnlockLevelManagerEditor : Editor
    {
        private UnlockLevelManager _unlockLevelManager;
        private int _timeDelay;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (Application.isPlaying)
            {
                return;
            }

            _unlockLevelManager = (UnlockLevelManager)target;
            _timeDelay++;

            if (_timeDelay <= 20)
            {
                return;
            }

            _unlockLevelManager.OnValidate();
            _timeDelay = 0;
        }
    }
#endif
}