using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using TimeManagerEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if BIG_CITY_LAB
using UCLocalizationSystem.Common;
#endif

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionsLocalizationManager")]
    public class EditionsLocalizationManager : MonoBehaviour
    {
#region Constants
        public const string SavedLocaleKey = "saved locale";
        private const string SavedLanguageKey = "saved language";
        private const string SavedSystemLanguage = "saved system language";
        private const string DefaultLocalizationFolderName = "Localization";

        private const string ToymanDutch = "Localization_nl";
        private const string ToymanEnglish = "Localization_en";
        private const string ToymanFrench = "Localization_fr";
        private const string ToymanGerman = "Localization_de";
        private const string ToymanRussian = "Localization_ru";
        private const string ToymanCzech = "Localization_cs";
        private const string ToymanCzechlternative = "Localization_cz";
        private const string ToymanPolish = "Localization_pl";
        
        private const string GameOnDefault = "_Default";
        private const string GameOnEnglish = "_English";
        private const string GameOnRussian = "_Russian";
        private const string GameOnDutch = "_Dutch";
        private const string GameOnFrench = "_French";
        private const string GameOnGerman = "_German";
        private const string GameOnCzech = "_Czech";
        private const string GameOnPolish = "_Polish";
        
        private const string GameOnDefaultV2 = "Default";
        private const string English = "English";
        private const string Russian = "Russian";
        private const string Dutch = "Dutch";
        private const string French = "French";
        private const string German = "German";
        private const string Czech = "Czech";
        private const string Polish = "Polish";

        private const string BigCityLabRuntimeDataFile = "RuntimeData";

        private const string ToymanDefine = "TOYMAN";
        private const string ArgunovDefine = "ARGUNOV";
        private const string GameOnDefine = "GAME_ON";
        private const string BigCityLabDefine = "BIG_CITY_LAB";
        private const string UndefinedDefine = "UNDEFINED";
#endregion

        [SerializeField] private string _localizationDefine = "UNDEFINED";

        [SerializeField] private Locale _englishLocale;
        [SerializeField] private Locale _frenchLocale;
        [SerializeField] private Locale _germanLocale;
        [SerializeField] private Locale _russianLocale;
        [SerializeField] private Locale _dutchLocale;
        [SerializeField] private Locale _polishLocale;
        [SerializeField] private Locale _czechLocale;

        [SerializeField] private LocalizationChangeButton _englishButton;
        [SerializeField] private LocalizationChangeButton _frenchButton;
        [SerializeField] private LocalizationChangeButton _germanButton;
        [SerializeField] private LocalizationChangeButton _russianButton;
        [SerializeField] private LocalizationChangeButton _dutchButton;
        [SerializeField] private LocalizationChangeButton _czechButton;
        [SerializeField] private LocalizationChangeButton _polishButton;
        
        [SerializeField] private List<LocalizationGroup> _localizationGroups = new List<LocalizationGroup>();
        public IReadOnlyList<LocalizationGroup> LocalizationGroups => _localizationGroups;

        [SerializeField, HideInInspector] private bool _haveEnglish = false;
        [SerializeField, HideInInspector] private bool _haveFrench = false;
        [SerializeField, HideInInspector] private bool _haveGerman = false;
        [SerializeField, HideInInspector] private bool _haveRussian = false;
        [SerializeField, HideInInspector] private bool _haveDutch = false;
        [SerializeField, HideInInspector] private bool _haveCzech = false;
        [SerializeField, HideInInspector] private bool _havePolish = false;
        
        public event Action OnChangeLocale;

        public Locale ActiveLocale
        {
            get
            {
                if (PlayerPrefs.HasKey(SavedLocaleKey))
                {
                    return Enumerable.FirstOrDefault(_localizationGroups, x =>
                            x.LocalizationChangeButton.CurrentLocale.LocaleName ==
                            PlayerPrefs.GetString(SavedLocaleKey))?.LocalizationChangeButton.CurrentLocale;
                }
                return LocalizationSettings.SelectedLocale;
            }
            set
            {
                PlayerPrefs.SetString(SavedLocaleKey, value.LocaleName);
                PlayerPrefs.Save();
            }
        }
        
        public SystemLanguage ActiveSystemLanguage
        {
            get
            {
                if (PlayerPrefs.HasKey(SavedSystemLanguage))
                {
                    return (SystemLanguage) PlayerPrefs.GetInt(SavedSystemLanguage);
                }
                return Application.systemLanguage;
            }
            set
            {
                PlayerPrefs.SetInt(SavedSystemLanguage, (int)value);
                PlayerPrefs.Save();
            }
        }
        
        public string ActiveLanguage
        {
            get
            {
                if (PlayerPrefs.HasKey(SavedLanguageKey))
                {
                    return PlayerPrefs.GetString(SavedLanguageKey);
                }
                return Application.systemLanguage.ToString();
            }
            set
            {
                PlayerPrefs.SetString(SavedLanguageKey, value);
                PlayerPrefs.Save();
            }
        }

        public void OnValidate()
        {
            FindDeveloperLocalization();
            GenerateLocalesDictionary();
        }

        private void FindDeveloperLocalization()
        {
            if (IsStreamingAssetsExist(DefaultLocalizationFolderName))
            {
                // Debug.Log("Toyman or GameOn");

                var localizationFolder = Path.Combine(Application.streamingAssetsPath, DefaultLocalizationFolderName);
                
                if (File.Exists(Path.Combine(localizationFolder, "string_table.csv")))
                {
                    // Debug.Log("Toyman");
                    _localizationDefine = ToymanDefine;
                    _haveEnglish = true;
                    CheckOtherLanguagesToymanAndArgunov();
                }
                else if (Directory.Exists(Path.Combine(localizationFolder, GameOnEnglish)) ||
                         Directory.Exists(Path.Combine(localizationFolder, GameOnDefault)) ||
                         Directory.Exists(Path.Combine(localizationFolder, English)) ||
                         Directory.Exists(Path.Combine(localizationFolder, GameOnDefaultV2)) )
                {
                    // Debug.Log("GameOn");
                    _localizationDefine = GameOnDefine;
                    _haveEnglish = true;
                    CheckOtherLanguagesGameOn();
                }
                else
                {
                    Debug.Log("undefined template or localization folder is empty! manager is disabled");
                    _haveDutch = _haveFrench = _haveGerman = _haveRussian = false;
                    _localizationDefine = UndefinedDefine;
                }
            }
            else if(IsStreamingAssetsExist(Path.Combine(ToymanEnglish, "string_table.csv")))
            {
                _localizationDefine = ToymanDefine;
                _haveEnglish = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if(IsStreamingAssetsExist(Path.Combine(ToymanCzech, "string_table.csv")))
            {
                _localizationDefine = ToymanDefine;
                _haveCzech = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if(IsStreamingAssetsExist(Path.Combine(ToymanDutch, "string_table.csv")))
            {
                _localizationDefine = ToymanDefine;
                _haveDutch = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if(IsStreamingAssetsExist(Path.Combine(ToymanFrench, "string_table.csv")))
            {
                _localizationDefine = ToymanDefine;
                _haveFrench = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if(IsStreamingAssetsExist(Path.Combine(ToymanPolish, "string_table.csv")))
            {
                _localizationDefine = ToymanDefine;
                _havePolish = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if(IsStreamingAssetsExist(Path.Combine(ToymanRussian, "string_table.csv")))
            {
                _localizationDefine = ToymanDefine;
                _haveRussian = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if(IsStreamingAssetsExist(Path.Combine(ToymanGerman, "string_table.csv")))
            {
                _localizationDefine = ToymanDefine;
                _haveGerman = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if (IsStreamingAssetsExist(Path.Combine(ToymanEnglish, "localization.csv")))
            {
                Debug.Log("Argunov(New gnomes)");
                _localizationDefine = ArgunovDefine;
                _haveEnglish = true;
                CheckOtherLanguagesToymanAndArgunov();
            }
            else if (IsResourcesExist(DefaultLocalizationFolderName))
            {
                Debug.Log("BigCityLab - wonderland engine?");

                if (IsResourcesExist(Path.Combine(DefaultLocalizationFolderName, BigCityLabRuntimeDataFile)))
                {
                    Debug.Log("BigCityLab - wonderland engine");
                    _haveEnglish = _haveDutch = _haveFrench = _haveGerman = _haveRussian = false;
                    _localizationDefine = BigCityLabDefine;
                    
#if BIG_CITY_LAB
                    CheckLanguagesBigCityLab();
#endif
                }
            }
            else
            {
                Debug.Log("undefined template! manager is disabled");
                _haveDutch = _haveFrench = _haveGerman = _haveRussian = false;
                _localizationDefine = UndefinedDefine;
            }
        }

        private void GenerateLocalesDictionary()
        {
            GenerateLocaleDictionaryCondition(_haveEnglish, _englishLocale, SystemLanguage.English);
            GenerateLocaleDictionaryCondition(_haveDutch, _dutchLocale, SystemLanguage.Dutch);
            GenerateLocaleDictionaryCondition(_haveFrench, _frenchLocale, SystemLanguage.French);
            GenerateLocaleDictionaryCondition(_haveGerman, _germanLocale, SystemLanguage.German);
            GenerateLocaleDictionaryCondition(_haveRussian, _russianLocale, SystemLanguage.Russian);
            GenerateLocaleDictionaryCondition(_haveCzech, _czechLocale, SystemLanguage.Czech);
            GenerateLocaleDictionaryCondition(_havePolish, _polishLocale, SystemLanguage.Polish);
        }

        private void GenerateLocaleDictionaryCondition(bool condition, Locale locale, SystemLanguage language)
        {
            if (locale == null)
            {
                return;
            }
            
            if (condition)
            {
                AddInLocalesDictionary(locale, language);
            }
            else
            {
                RemoveFromLocalesDictionary(language);
            }
        }
        
        private void AddInLocalesDictionary(Locale locale, SystemLanguage language)
        {
            if (Enumerable.All(_localizationGroups, x=>x.SystemLanguage != language))
            {
                switch (language)
                {
                    case SystemLanguage.Russian:
                        _localizationGroups.Add(new LocalizationGroup(locale, _russianButton, language));
                        break;
                    case SystemLanguage.Dutch:
                        _localizationGroups.Add(new LocalizationGroup(locale, _dutchButton, language));
                        break;
                    case SystemLanguage.French:
                        _localizationGroups.Add(new LocalizationGroup(locale, _frenchButton, language));
                        break;
                    case SystemLanguage.German:
                        _localizationGroups.Add(new LocalizationGroup(locale, _germanButton, language));
                        break;
                    case SystemLanguage.Czech:
                        _localizationGroups.Add(new LocalizationGroup(locale, _czechButton, language));
                        break;
                    case SystemLanguage.Polish:
                        _localizationGroups.Add(new LocalizationGroup(locale, _polishButton, language));
                        break;
                    default:
                        _localizationGroups.Add(new LocalizationGroup(locale, _englishButton, language));
                        break;
                }

            }
        }

        private void RemoveFromLocalesDictionary(SystemLanguage language)
        {
            // if (language == SystemLanguage.English)
            // {
            //     return;
            // }
            
            if (Enumerable.Any(_localizationGroups, x=>x.SystemLanguage == language))
            {
                _localizationGroups.Remove(Enumerable.FirstOrDefault(_localizationGroups, x=>x.SystemLanguage == language));
            }
        }

        private bool IsStreamingAssetsExist(string directoryName)
        {
            var targetFolderPath = Path.Combine(Application.streamingAssetsPath, directoryName);
            return Directory.Exists(targetFolderPath) || File.Exists(targetFolderPath);
        }

        private bool IsResourcesExist(string directoryName)
        {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources");
            var targetFolderPath = Path.Combine(resourcesPath, directoryName);
            return Directory.Exists(targetFolderPath);
        }

        private void CheckOtherLanguagesGameOn()
        {
            var localizationFolder = Path.Combine(Application.streamingAssetsPath, DefaultLocalizationFolderName);

            _haveRussian = Directory.Exists(Path.Combine(localizationFolder, GameOnRussian)) ||
                           Directory.Exists(Path.Combine(localizationFolder, Russian));
            _haveFrench = Directory.Exists(Path.Combine(localizationFolder, GameOnFrench)) ||
                           Directory.Exists(Path.Combine(localizationFolder, French));
            _haveGerman = Directory.Exists(Path.Combine(localizationFolder, GameOnGerman)) ||
                           Directory.Exists(Path.Combine(localizationFolder, German));
            _haveDutch = Directory.Exists(Path.Combine(localizationFolder, GameOnDutch)) ||
                           Directory.Exists(Path.Combine(localizationFolder, Dutch));
            _haveCzech = Directory.Exists(Path.Combine(localizationFolder, GameOnCzech)) ||
                         Directory.Exists(Path.Combine(localizationFolder, Czech));
            _havePolish = Directory.Exists(Path.Combine(localizationFolder, GameOnPolish)) ||
                         Directory.Exists(Path.Combine(localizationFolder, Polish));
            
            
        }
        
        private void CheckOtherLanguagesToymanAndArgunov()
        {
            _haveEnglish = Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanEnglish));
            _haveRussian = Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanRussian));
            _haveFrench = Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanFrench));
            _haveGerman = Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanGerman));
            _haveDutch = Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanDutch));
            _haveCzech = Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanCzech)) || 
                         Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanCzechlternative));
            _havePolish = Directory.Exists(Path.Combine(Application.streamingAssetsPath, ToymanPolish));
        }

#if BIG_CITY_LAB
        private void CheckLanguagesBigCityLab()
        {
            var data = Resources.Load(Path.Combine(DefaultLocalizationFolderName, BigCityLabRuntimeDataFile)) as RuntimeData;

            if (data != null)
            {
                foreach (var language in data.languages)
                {
                    switch (language)
                    {
                        case English:
                            _haveEnglish = true;
                            break;
                        case French:
                            _haveFrench = true;
                            break;
                        case German:
                            _haveGerman = true;
                            break;
                        case Dutch:
                            _haveDutch = true;
                            break;
                        case Russian:
                            _haveRussian = true;
                            break;
                        case Czech:
                            _haveCzech = true;
                            break;
                        case Polish:
                            _havePolish = true;
                            break;
                        default:
                            Debug.LogError("project contain undefined language! Please add this locale manually!");
                            break;
                    }
                }
            }
            else
            {
                Debug.LogError("Dont find file named as RuntimeData");
            }
        }
#endif
        
        public void SetLocale(Locale locale, string language, SystemLanguage systemLanguage)
        {
            LocalizationSettings.Instance.SetSelectedLocale(locale);
            ActiveLocale = locale;
            ActiveLanguage = language;
            ActiveSystemLanguage = systemLanguage;
#if TOYMAN
            LocalizationManager.Instance.LoadLocalization(systemLanguage);
#elif ARGUNOV
            PlayerPrefs.SetString(ProjectSettings.SavedLanguageKey, language);
            PlayerPrefs.Save();
            LocalizationManager.Instance.CurrentLocale = language;
#elif GAME_ON
            StartCoroutine(Strategop.Common.Localization.LocalizationManager.LoadLocale(systemLanguage));
#elif BIG_CITY_LAB
            var settings = ProfileManager.GetProfileData<CommonSettings>();
            if (ProfileManager.HasProfile)
            {
                settings.language = locale.LocaleName;
                LocalizeObserver.SetLanguage(settings.language);    
                EM.Trigger(EventTypes.LanguageChanged);
                ProfileManager.Save();
            }
            else
            {
                LocalizeObserver.SetLanguage(settings.language);
            }
#endif
            
            OnChangeLocale?.Invoke();
        }

        private void Start()
        {
#if !UNITY_ANDROID && !UNITY_IOS
        
            FindDeveloperLocalization();
#endif
            
            if (!_haveCzech)
            {
                RemoveFromLocalesDictionary(SystemLanguage.Czech);
            }

            if (!_haveDutch)
            {
                RemoveFromLocalesDictionary(SystemLanguage.Dutch);   
            }

            if (!_haveRussian)
            {
                RemoveFromLocalesDictionary(SystemLanguage.Russian);
            }

            if (!_haveFrench)
            {
                RemoveFromLocalesDictionary(SystemLanguage.French);
            }

            if (!_haveGerman)
            {
                RemoveFromLocalesDictionary(SystemLanguage.German);
            }

            if (!_havePolish)
            {
                RemoveFromLocalesDictionary(SystemLanguage.Polish);
            }

            if (!_haveEnglish)
            {
                RemoveFromLocalesDictionary(SystemLanguage.English);
            }
            
            if (PlayerPrefs.HasKey(SavedLocaleKey))
            {
                SetLocale(ActiveLocale, ActiveLanguage, ActiveSystemLanguage);
            }
            else
            {
                if (Enumerable.All(_localizationGroups,x=>x.SystemLanguage!=Application.systemLanguage))
                {
                    if (_localizationGroups.Count > 0)
                    {

                        switch (_localizationGroups.First().SystemLanguage)
                        {
                            case SystemLanguage.Russian:
                                ActiveLocale = _russianLocale;
                                ActiveSystemLanguage = SystemLanguage.Russian;
                                break;
                            case SystemLanguage.Dutch:
                                ActiveLocale = _dutchLocale;
                                ActiveSystemLanguage = SystemLanguage.Dutch;
                                break;
                            case SystemLanguage.French:
                                ActiveLocale = _frenchLocale;
                                ActiveSystemLanguage = SystemLanguage.French;
                                break;
                            case SystemLanguage.German:
                                ActiveLocale = _germanLocale;
                                ActiveSystemLanguage = SystemLanguage.German;
                                break;
                            case SystemLanguage.Czech:
                                ActiveLocale = _czechLocale;
                                ActiveSystemLanguage = SystemLanguage.Czech;
                                break;
                            case SystemLanguage.Polish:
                                ActiveLocale = _polishLocale;
                                ActiveSystemLanguage = SystemLanguage.Polish;
                                break;
                            default:
                                ActiveLocale = _englishLocale;
                                ActiveSystemLanguage = SystemLanguage.English;
                                break;
                        }
                        ActiveLanguage = ActiveLocale.Identifier.Code;
                        Debug.Log(ActiveLanguage);
                        SetLocale(ActiveLocale,ActiveLanguage,ActiveSystemLanguage);
                        return;
                    }
                }
                
                SetLocale(ActiveLocale, ActiveLanguage, Application.systemLanguage);
            }
        }
       
    }

    [Serializable]
    public class LocalizationGroup
    {
        [SerializeField] private Locale _locale;
        
        [SerializeField] private LocalizationChangeButton _localizationChangeButton;
        public LocalizationChangeButton LocalizationChangeButton => _localizationChangeButton;

        [SerializeField] private SystemLanguage _systemLanguage;
        public SystemLanguage SystemLanguage => _systemLanguage;

        public LocalizationGroup(Locale locale, LocalizationChangeButton localizationChangeButton, SystemLanguage systemLanguage)
        {
            _locale = locale;
            _localizationChangeButton = localizationChangeButton;
            _systemLanguage = systemLanguage;
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(EditionsLocalizationManager))]
    public class EditionsLocalizationManagerEditor : Editor
    {
        private EditionsLocalizationManager _editionsLocalizationManager;
        private int _timeDelay;
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (Application.isPlaying)
            {
                return;
            }
            
            _editionsLocalizationManager = (EditionsLocalizationManager)target;
            _timeDelay++;

            if (_timeDelay <= 20)
            {
                return;
            }

            _editionsLocalizationManager.OnValidate();
            _timeDelay = 0;
        }
    }
#endif
    
}