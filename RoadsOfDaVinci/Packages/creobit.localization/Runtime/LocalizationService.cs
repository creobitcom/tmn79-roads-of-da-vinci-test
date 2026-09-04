using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Creobit.Localization
{
    /// <summary>
    /// Provides functionality to manage and switch between localizations in the application.
    /// </summary>
    public sealed class LocalizationService
    {
        private const string GameTextPrefix = "GameText_";
        private const string JsonExtension = ".json";
        private static readonly string[] DefaultLanguages = { "en-US", "ru-RU" };

        public static readonly LocalizationService Instance = new();

        private readonly LocalizationFileHandler _fileHandler;
        private readonly List<string> _availableLanguages;
        private LocalizationJson _localizationJson;
        private string _currentGame;
        private string _currentLocalization;
        private IReadOnlyList<string> _supportedLanguages;

        /// <summary>
        /// Gets the currently active language used for localization within the application.
        /// This property reflects the language code (e.g., "en", "fr", "de") corresponding
        /// to the localization being displayed.
        /// </summary>
        public string CurrentLanguage => _currentLocalization;

        /// <summary>
        /// Gets a read-only list of strings representing the available languages supported
        /// by the localization system.
        /// </summary>
        /// <remarks>
        /// The list of available languages is populated based on the localization files
        /// present for the application. This property can be used to determine which
        /// languages are available for the user to select.
        /// </remarks>
        /// <value>
        /// A read-only collection of language codes.
        /// </value>
        public IReadOnlyList<string> AvailableLanguages => _availableLanguages;

        /// <summary>
        /// Event triggered when the current language of the localization system is changed.
        /// The event provides the new language, allowing subscribers to react accordingly to the change.
        /// </summary>
        public event Action<string> OnLanguageChanged;

        private LocalizationService()
        {
            _availableLanguages = new List<string>();
            _fileHandler = new LocalizationFileHandler();
        }

        /// <summary>
        /// This method MUST be called before you start to work with the localization module
        /// Initializes the localization system by setting the game and default localization.
        /// Loads the available languages and applies the specified default localization.
        /// </summary>
        /// <param name="game">The name of the game for which the localization system is being initialized.</param>
        /// <param name="defaultLocalization">The default language to be loaded and applied.</param>
        /// <returns>A UniTask that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="game"/> parameter is null or empty.</exception>
        public async UniTask Init(string game, string defaultLocalization, IReadOnlyList<string> supportedLanguages = null)
        {
            if (string.IsNullOrEmpty(game))
                throw new ArgumentNullException(nameof(game));

            _currentGame = game;
            _currentLocalization = defaultLocalization;
            _supportedLanguages = supportedLanguages ?? _supportedLanguages;

            Debug.Log($"[Localization] Init game={game}, default={defaultLocalization}, supported={(supportedLanguages == null ? "null" : string.Join(",", supportedLanguages))}");

            await CheckLanguages();

            Debug.Log($"[Localization] Available: {string.Join(",", _availableLanguages)}, current={_currentLocalization}");

            await LoadAndSetLanguage(_currentLocalization);
        }

        /// <summary>
        /// Loads the specified language and applies it as the current localization.
        /// If the language is not available, an exception will be thrown.
        /// </summary>
        /// <param name="language">The language to be loaded and applied.</param>
        /// <returns>A UniTask that represents the asynchronous operation of loading and setting the language.</returns>
        /// <exception cref="LanguageNotAvailableException">
        /// Thrown when the specified <paramref name="language"/> is not in the list of available languages.
        /// </exception>
        /// <exception cref="LocalizationFileParseException">
        /// Thrown when the localization file for the specified <paramref name="language"/> cannot be parsed.
        /// </exception>
        public async UniTask LoadAndSetLanguage(string language)
        {
            if (language is null or "") return;
            
            if (!_availableLanguages.Contains(language))
                throw new LanguageNotAvailableException(language);

            var localizationFile = BuildLocalizationFilePath(language);
            var jsonContent = await _fileHandler.LoadTextFileAsync(localizationFile);

            try
            {
                _localizationJson = JsonConvert.DeserializeObject<LocalizationJson>(jsonContent);
                _currentLocalization = language;
            }
            catch (Exception e)
            {
                throw new LocalizationFileParseException(language, e);
            }
            
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            LocalizationWindowTitle.Localize();
#endif
            
            Log.Bootstrap.Info($"Localization loaded: {language}");
            
            OnLanguageChanged?.Invoke(language);
        }

        /// <summary>
        /// Retrieves the localized text for the specified key from the localization data.
        /// If the key does not exist, the key itself is returned as a fallback.
        /// </summary>
        /// <param name="key">The key for which the localized text is to be retrieved.</param>
        /// <returns>The localized text corresponding to the provided key,
        /// or the key itself if no matching localized text is found.</returns>
        public string GetText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            if (_localizationJson?.Data == null || !_localizationJson.Data.TryGetValue(key, out var text))
            {
                Log.Bootstrap.Warning($"Missing localization key: {key}");
                return key;
            }

            return text;
        }

        private async UniTask CheckLanguages()
        {
            _availableLanguages.Clear();

#if UNITY_ANDROID && !UNITY_EDITOR
            await CheckAndroidLanguages();
#else
            await CheckStandardLanguages();
#endif

            if (_availableLanguages.Count == 0)
                throw new NoLanguagesFoundException(_currentGame);
            
            if (!_availableLanguages.Contains(_currentLocalization))
            {
                _currentLocalization = _availableLanguages.First();
            }
        }

        private string BuildLocalizationFilePath(string language) =>
            Path.Combine(_currentGame, $"{GameTextPrefix}{language}{JsonExtension}");

        private async UniTask CheckAndroidLanguages()
        {
            IReadOnlyList<string> candidates = _supportedLanguages != null && _supportedLanguages.Count > 0
                ? _supportedLanguages
                : DefaultLanguages;

            foreach (var language in candidates)
            {
                var filePath = BuildLocalizationFilePath(language);
                if (await _fileHandler.FileExistsAsync(filePath))
                    _availableLanguages.Add(language);
            }
        }

        private UniTask CheckStandardLanguages()
        {
            var localizationPath = Path.Combine(Application.streamingAssetsPath, _currentGame);
            if (!Directory.Exists(localizationPath))
                throw new DirectoryNotFoundException($"Localization directory not found: {localizationPath}");

            var files = Directory.GetFiles(localizationPath, $"{GameTextPrefix}*{JsonExtension}");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith(GameTextPrefix))
                {
                    var language = fileName.Substring(GameTextPrefix.Length);
                    _availableLanguages.Add(language);
                }
            }
            
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Determines whether the localization system is configured with only one available language.
        /// </summary>
        /// <returns>True if there is exactly one available language in the build; otherwise, false.</returns>
        public bool IsOneLanguageBuild()
        {
            return _availableLanguages.Count == 1;
        }
    }
}