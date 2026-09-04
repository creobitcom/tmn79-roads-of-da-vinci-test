using System;
using System.Linq;
using UltEvents;

#if TOYMAN
using TimeManagerEngine;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/PlayButton")]
    public class PlayButton : MonoBehaviour
    {
#if TK2D
        [SerializeField] private tk2dUIItem _button;

#elif CREOBIT
        [SerializeField] private UltEvent _continue;
        [SerializeField] private _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data.CurrentLevelSO _currentLevel;
#endif

        [SerializeField] private Button _button;

        private void OnValidate()
        {
#if TK2D
            _button ??= GetComponent<tk2dUIItem>();      
#else 
            _button ??= GetComponent<Button>();
#endif
        }

        private enum ContinueType
        {
            LastUnlocked,
            LastVisited,
            LastClicked,
        }

        [SerializeField] private ContinueType _continueType;
#if !CREOBIT
        [SerializeField] private FakeLevelButton _fakeLevelButton;
#endif

        private UnlockLevelManager _cachedUnlockLevelManager;

        private UnlockLevelManager _unlockLevelManager => _cachedUnlockLevelManager ??= FindFirstObjectByType<UnlockLevelManager>();


        private Button _fakeButton;
        private int _continueIndex;

        protected void Awake()
        {
#if TK2D
            _button.enabled = false;
#endif

#if !CREOBIT

            var fakeLevelObject = Instantiate(_fakeLevelButton, transform);

            _fakeButton = fakeLevelObject.FakeButton;
            _fakeButton.onClick.AddListener(OnClickContinue);
#else
            // If using the Creobit engine, there's no need to create a fakeButton.
            // The main button (_button) is used directly for handling the continue action.
            // OnClickContinue will trigger an UltEvent to notify listeners.
            _button.onClick.AddListener(OnClickContinue);
#endif
        }

        private void OnEnable()
        {
#if TK2D
            _button.enabled = false;
#endif

            switch (_continueType)
            {
                default:
                case ContinueType.LastUnlocked:
                    _continueIndex = PlayerPrefs.GetInt(UnlockLevelManager.LastUnlockedLevelIndexKey);
                    break;
                case ContinueType.LastVisited:
                    _continueIndex = PlayerPrefs.GetInt(UnlockLevelManager.LastVisitedLevelIndexKey);
                    break;
                case ContinueType.LastClicked:
                    _continueIndex = 0;

                    foreach (var level in _unlockLevelManager.UnlockLevels)
                    {
                        level.OnInteract += ChangeContinueOnInteract;
                    }
                    break;
            }
        }

        private void OnDisable()
        {
            if (_continueType == ContinueType.LastClicked)
            {
                foreach (var level in _unlockLevelManager.UnlockLevels)
                {
                    level.OnInteract -= ChangeContinueOnInteract;
                }
            }
        }

        private void ChangeContinueOnInteract(int levelId)
        {
#if TOYMAN
            if (levelId > Player.I.MaxAvailableLevel)
            {
                return;
            }
#endif
            _continueIndex = levelId;
        }

        private void OnClickContinue()
        {
#if TK2D
            _button.enabled = false;
#endif
            if (_continueIndex <= 0)
            {
                _continueIndex = 1;
            }

#if CREOBIT
            if (_currentLevel != null && _currentLevel.LevelNum > 0)
            {
                _continueIndex = _currentLevel.LevelNum;
            }
#endif

#if TOYMAN
            var currentLevel = _unlockLevelManager.UnlockLevels.First(x => x.Button.GetComponent<MapLevelButton>().flag.activeSelf);
            Debug.Log(currentLevel.Index + " CURRENT");
#else
            var currentLevel = _unlockLevelManager.UnlockLevels.First(x => x.Index == _continueIndex);
#endif

            if (_unlockLevelManager.LevelCondition(currentLevel))
            {
                if (_continueType == ContinueType.LastClicked)
                {
                    _unlockLevelManager.UnlockLevelHasInteraction(_continueIndex);

                    if (_unlockLevelManager.LevelInteraction == UnlockLevelManager.LevelInteractions.Unlocked)
                    {
                        currentLevel.OpenLevel();
                    }
                }
#if TK2D
                _button.SimulateClick();
#elif CREOBIT
                _continue?.Invoke();
#else
                _button.onClick.Invoke();
#endif
                return;
            }


            if (currentLevel.WatchedAdsCount < currentLevel.TotalAdsCount)
            {
#if TK2D
                _button.enabled = false;
#endif
                if (_continueType == ContinueType.LastClicked)
                {
                    _unlockLevelManager.UnlockLevelHasInteraction(_continueIndex);
                    if (_unlockLevelManager.LevelInteraction == UnlockLevelManager.LevelInteractions.Unlocked)
                    {
                        _unlockLevelManager.ShowUnlockWindow(currentLevel);
                    }
                }
                else
                {
                    _unlockLevelManager.ShowUnlockWindow(currentLevel);
                }
            }
            else
            {
                if (_continueType == ContinueType.LastClicked)
                {
                    _unlockLevelManager.UnlockLevelHasInteraction(_continueIndex);
                    if (_unlockLevelManager.LevelInteraction == UnlockLevelManager.LevelInteractions.Unlocked)
                    {
                        currentLevel.OpenLevel();
                    }
                }
#if TK2D
                _button.SimulateClick();
#elif CREOBIT
                _continue?.Invoke();
#else
                _button.onClick.Invoke();
#endif
            }
        }
    }
}