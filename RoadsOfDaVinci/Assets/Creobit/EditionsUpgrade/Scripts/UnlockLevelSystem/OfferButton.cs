using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using R3;
using TimeManagerEngine;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(OpenPrefabService))]
    public class OfferButton : MonoBehaviour
    {
        [Header("Parameters")]
        [SerializeField]
        private bool _pauseGameAfterClick = false;
        [SerializeField]
        private bool _useLevelCheck = false;
        [Min(1)]
        [SerializeField]
        private int _freeLevelsCount = 6;

        [Header("Components")]
#if !CREOBIT
        [SerializeField]
        private Button _fakeButton;
#else
        [SerializeField]
        private UltEvent _gamePurchased;
#endif
        [SerializeField]
        private OpenPrefabService _openPrefabService;

#if TK2D
        [SerializeField] private tk2dUIItem _button;
#else
        [SerializeField] private Button _button;
#endif


        private void OnValidate()
        {
            if (_openPrefabService == null)
            {
                _openPrefabService = GetComponent<OpenPrefabService>();
            }
#if TK2D
            _button = GetComponent<tk2dUIItem>();

            if (TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
            }
#endif
        }


#if !CREOBIT

        private void OnDestroy()
        {
            _fakeButton.onClick.RemoveListener(OnFakeButtonClicked);
        }

        private void Start()
        {
            _fakeButton.onClick.AddListener(OnFakeButtonClicked);
        }
#else

        private void Start()
        {
            _button
                .OnClickAsObservable()
                .Subscribe((_) => OnFakeButtonClicked())
                .AddTo(this);
        }

#endif
        private void PauseGame()
        {
#if TOYMAN
            AppTimer.Instance.gameTimeScale = 0.0f;
#elif ARGUNOV
            GameManager.I.IsPause = true;
#elif CREOBIT
            var pauseBridge = FindFirstObjectByType<PauseBridge>();

            if (pauseBridge != null)
            {
                pauseBridge.Pause();
            }
#elif GAME_ON
            EmergencyCrewLevel.Instance.Paused = true;
#else
            Debug.LogError($"No handlers for pause game", gameObject);
#endif
        }

        private bool IsNeedToShowOffer()
        {
#if PREMIUM
    return false;
#endif

            bool gameIsPurchased = PlayerPrefs.HasKey("AllLevelsBuyKey") && PlayerPrefs.GetString("AllLevelsBuyKey") == "true";

            if (gameIsPurchased)
            {
                return false;
            }

            bool isFreeLevel = true;
#if TOYMAN
            isFreeLevel = Player.I.CurrentLevel <= _freeLevelsCount;
#elif ARGUNOV || GAME_ON || CREOBIT
            isFreeLevel = PlayerPrefs.GetInt(UnlockLevelManager.LastVisitedLevelIndexKey, 0) <= _freeLevelsCount;
#else
            Debug.LogError($"No handlers for bool isFreeLevel", gameObject);
#endif
            if (_useLevelCheck && isFreeLevel)
            {
                return false;
            }

            return true;
        }

        private void OnFakeButtonClicked()
        {
            if (_pauseGameAfterClick)
            {
                PauseGame();
            }

            if (IsNeedToShowOffer())
            {
                _openPrefabService.OnClick();
            }
            else
            {
#if TK2D
                _button.SimulateClick();
#elif CREOBIT
                _gamePurchased?.Invoke();
#else
_button.onClick.Invoke();
#endif
            }

        }
    }
}
