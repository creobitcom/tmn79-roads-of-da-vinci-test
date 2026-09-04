using System;
using System.IO;
using Creobit.Loading;
using Creobit.Localization;
using Creobit.Localization.Utils;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using R3;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LogoController
{
    public static class LogoConstants
    {
        public const string LogoJsonFile = "logo.json";
    }

    public class LogoController : ILogoController
    {
        public string GameFolder;
        private readonly ReactiveProperty<Sprite> _logo = new();
        
        public ReadOnlyReactiveProperty<Sprite> Logo => _logo;
#if ALL_IN_ONE
        [Inject] private GameSwitcher _gameSwitcher;
#endif
        
        public UniTask Load()
        {
#if ALL_IN_ONE
            GameFolder = _gameSwitcher.CurrentGame.Value.GameName;
#endif
            LocalizationService.Instance.OnLanguageChanged += OnLanguageChanged;
            
            return UpdateLogo();
        }
        
        public void Dispose()
        {
            LocalizationService.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(string language)
        {
            UpdateLogo().Forget();
        }

        private async UniTask UpdateLogo()
        {
            try
            {
                var logoData = await LoadLogoData();

                if (logoData == null)
                {
                    return;
                }
                
                Log.Meta.Info("LOGO DATA EDITION IS "+logoData.CurrentEdition);
                var editionPath = CombineStreamingPath(Application.streamingAssetsPath, GameFolder);
                var currentLocale = ResolveLocaleCode(LocalizationService.Instance.CurrentLanguage);
                var logoName = $"Logo_{currentLocale}_{logoData.CurrentEdition}.png";
                var fallbackName = $"Logo_en_{logoData.CurrentEdition}.png";

                await LoadLogo(editionPath, logoName, fallbackName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error in LogoController: {ex}");
            }
        }

        private static string ResolveLocaleCode(string language)
        {
            if (string.IsNullOrEmpty(language))
                return "en";

            var separator = language.IndexOfAny(new[] { '-', '_' });
            var code = separator > 0 ? language.Substring(0, separator) : language;

            return code.ToLowerInvariant();
        }

        private async UniTask<LogoDataJson> LoadLogoData()
        {
            string jsonPath = Path.Combine(GameFolder, LogoConstants.LogoJsonFile);
            Log.Bootstrap.Info($"Attempting to load JSON from: {jsonPath}");

            string jsonContent = await StreamingAssetsHelper.LoadTextFileAsync(jsonPath);
            if (string.IsNullOrEmpty(jsonContent))
            {
                Debug.LogError($"Failed to load {jsonPath}. File not found or empty.");
                return null;
            }

            try
            {
                var logoData = JsonConvert.DeserializeObject<LogoDataJson>(jsonContent);
                if (logoData == null || string.IsNullOrEmpty(logoData.CurrentEdition))
                {
                    Debug.LogError("Logo data or edition is null");
                    return null;
                }
                Log.Bootstrap.Info($"Found edition: {logoData.CurrentEdition}");
                return logoData;
            }
            catch (JsonException ex)
            {
                Log.Bootstrap.Error($"JSON parsing error: {ex.Message}");
                return null;
            }
        }
        
        private async UniTask LoadLogo(string editionPath, string logoName, string fallbackName)
        {
            var texture = await LoadTexture(BuildStreamingUrl(editionPath, logoName));

            if (texture == null && !string.Equals(logoName, fallbackName, StringComparison.OrdinalIgnoreCase))
            {
                Log.Meta.Info($"No logo '{logoName}', falling back to '{fallbackName}'");
                texture = await LoadTexture(BuildStreamingUrl(editionPath, fallbackName));
            }

            if (texture == null)
            {
                Debug.LogError($"LogoController: не удалось загрузить логотип '{logoName}' в {editionPath}");
                return;
            }

            _logo.Value = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new(0.5f, 0.5f));
        }

        private static async UniTask<Texture2D> LoadTexture(string url)
        {
            using var request = UnityWebRequestTexture.GetTexture(url);

            try
            {
                await request.SendWebRequest();
            }
            catch (Exception exception)
            {
                Log.Meta.Info($"Logo request failed for {url}: {exception.Message}");
                return null;
            }

            return request.result == UnityWebRequest.Result.Success
                ? DownloadHandlerTexture.GetContent(request)
                : null;
        }

        private static string CombineStreamingPath(string root, string folder)
        {
            return string.IsNullOrEmpty(folder) ? root : root.TrimEnd('/', '\\') + "/" + folder;
        }

        private static string BuildStreamingUrl(string editionPath, string fileName)
        {
            var path = CombineStreamingPath(editionPath, fileName).Replace('\\', '/');

            return path.Contains("://") ? path : "file://" + path;
        }
    }
    public interface ILogoController : ILoadUnit, IDisposable
    {
        public ReadOnlyReactiveProperty<Sprite> Logo { get; }
    }
}