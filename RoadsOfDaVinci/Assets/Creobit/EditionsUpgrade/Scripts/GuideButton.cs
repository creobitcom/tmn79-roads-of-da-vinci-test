using TimeManagerEngine;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{

    public class GuideButton : MonoBehaviour
    {
        private bool _iapBuyed => PlayerPrefs.HasKey("AllLevelsBuyKey") && PlayerPrefs.GetString("AllLevelsBuyKey") == "true";

        [SerializeField] private int _freeLevelsCount;
        [SerializeField] private Button _fakeButton;
        [SerializeField] private GameObject _offerWindow;
        [SerializeField] private Transform _offersWindowParent;
#if TK2D
        [SerializeField] private tk2dUIItem _button;
#else
        [SerializeField] private Button _button;
#endif

        private void Awake()
        {
            _fakeButton.onClick.AddListener(OpenGuide);
#if !PREMIUM
            SpawnOffer();
#endif
        }

        private void OnDestroy()
        {
            _fakeButton.onClick.RemoveListener(OpenGuide);
        }

        private void SpawnOffer()
        {
            _offerWindow.gameObject.SetActive(false);
            _offerWindow = Instantiate(_offerWindow, _offersWindowParent);
            _offerWindow.transform.SetAsLastSibling();
        }

        private void OpenGuide()
        {
            PauseGame();            
#if !PREMIUM
            if (_iapBuyed || IsFreeLevel())
            {
                CallClick();
            }
            else
            {
                CallOffer();       
            }
#else
            CallClick();
#endif
        }

        private void PauseGame()
        {
#if TOYMAN
            AppTimer.Instance.gameTimeScale = 0.0f;
#else
            Debug.LogError("Dont have correct define, please, fix this!");
#endif
        }
        
        private void CallClick()
        {
#if TK2D
            _button.SimulateClick();
#else
                _button.onClick.Invoke();
#endif
        }

        private void CallOffer()
        {
            _offerWindow.SetActive(true);
        }

        private bool IsFreeLevel()
        {
#if TOYMAN
            return Player.I.CurrentLevel <= _freeLevelsCount;
#else
            return false;
#endif
        }
    }
}