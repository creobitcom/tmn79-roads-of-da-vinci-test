using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

#if BIG_CITY_LAB
using WonderlandEngine.Common.Profile;
using WonderlandEngine.Base.Profile;
using UCLocalizationSystem;
#endif

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/LocalizationChangeButton")]
    [RequireComponent(typeof(Button))]
    public class LocalizationChangeButton : MonoBehaviour
    {
        [SerializeField] private Image _buttonImage;
        [SerializeField] private GameObject _selectedLocaleObject;

        [SerializeField] private Locale _currentLocale;
        public Locale CurrentLocale => _currentLocale;

        [SerializeField] private SystemLanguage _currentSystemLanguage;

        [SerializeField] private Sprite _normalSprite;
        public Sprite NormalSprite => _normalSprite;

        [SerializeField] private Button _button;
        public Button Button => _button;
        
        private EditionsLocalizationManager _editionsLocalizationManager;
        
        private void Awake()
        {
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
            
            _editionsLocalizationManager = FindObjectOfType<EditionsLocalizationManager>();
            _editionsLocalizationManager.OnChangeLocale += OnChangeLocale;
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnClick);
            _editionsLocalizationManager.OnChangeLocale -= OnChangeLocale;
        }

        private void OnClick()
        {
            var language = _currentLocale.Identifier.Code;
            _editionsLocalizationManager.SetLocale(_currentLocale, language, _currentSystemLanguage);
            Debug.Log(language);
        }
        
        private void OnChangeLocale()
        {
            _selectedLocaleObject.SetActive(_editionsLocalizationManager.ActiveSystemLanguage == _currentSystemLanguage);
        }

        public void SetDataOnInstantiate(SystemLanguage systemLanguage)
        {
            _currentSystemLanguage = systemLanguage;
            _selectedLocaleObject.SetActive(_editionsLocalizationManager.ActiveLocale == _currentLocale);
        }
    }
}