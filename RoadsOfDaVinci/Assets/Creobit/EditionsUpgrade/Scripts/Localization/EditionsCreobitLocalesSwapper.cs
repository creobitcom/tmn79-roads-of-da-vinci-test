#if CREOBIT
using Creobit.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;

namespace _8floor.EditionsUpgrade.Scripts.Localization
{
    public class EditionsCreobitLocalesSwapper : MonoBehaviour
    {
        private static EditionsCreobitLocalesSwapper _instance;

        [SerializeField]
        private SerializedDictionary<string, Locale> _localesDictionary;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            DontDestroyOnLoad(this);

            SetLocale(LocalizationService.Instance.CurrentLanguage);
        }

        private void OnEnable()
        {
            LocalizationService.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationService.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(string language) => SetLocale(language);

        private void SetLocale(string language)
        {
            if (!_localesDictionary.ContainsKey(language))
            {
                return;
            }

            LocalizationSettings.Instance.SetSelectedLocale(_localesDictionary[language]);
        }

    }
}
#endif