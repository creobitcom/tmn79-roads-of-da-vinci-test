using System.Collections.Generic;
using System.IO;
using Creobit.Localization.Utils;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuideContentLoader
    {
        private const string ContentFileName = "Level.txt";
        private const string LevelFolderLabel = "Level";

        private static readonly string[] PictureExtensions = { ".jpg", ".png" };

        private readonly string _rootFolder;
        private readonly Dictionary<int, GuideLevelContent> _contentCache = new();
        private readonly Dictionary<string, Sprite> _pictureCache = new();

        public GuideContentLoader(string rootFolder)
        {
            _rootFolder = rootFolder;
        }

        public async UniTask<GuideLevelContent> LoadLevel(int levelNumber)
        {
            if (_contentCache.TryGetValue(levelNumber, out var cached))
            {
                return cached;
            }

            var relativePath = Path.Combine(_rootFolder, LevelFolderName(levelNumber), ContentFileName);
            var raw = await StreamingAssetsHelper.LoadTextFileAsync(relativePath);

            if (string.IsNullOrEmpty(raw))
            {
                Log.Meta.Warning($"Guide content not found: {relativePath}");
                return null;
            }

            GuideLevelContent content;

            try
            {
                content = JsonConvert.DeserializeObject<GuideLevelContent>(raw);
            }
            catch (JsonException exception)
            {
                Log.Meta.Error($"Guide content is broken: {relativePath}. {exception.Message}");
                return null;
            }

            _contentCache[levelNumber] = content;
            return content;
        }

        public async UniTask<Sprite> LoadPicture(int levelNumber, string pictureName)
        {
            if (string.IsNullOrEmpty(pictureName))
            {
                return null;
            }

            var cacheKey = $"{levelNumber:00}/{pictureName}";

            if (_pictureCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            foreach (var extension in PictureExtensions)
            {
                var fullPath = Path.Combine(Application.streamingAssetsPath, _rootFolder,
                    LevelFolderName(levelNumber), pictureName + extension);

                var texture = await LoadTexture(fullPath);

                if (texture == null)
                {
                    continue;
                }

                var sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));

                _pictureCache[cacheKey] = sprite;
                return sprite;
            }

            Log.Meta.Warning($"Guide picture not found: {_rootFolder}/{LevelFolderName(levelNumber)}/{pictureName}");
            return null;
        }

        public void ClearPictures()
        {
            foreach (var sprite in _pictureCache.Values)
            {
                if (sprite == null)
                {
                    continue;
                }

                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }

            _pictureCache.Clear();
        }

        private static async UniTask<Texture2D> LoadTexture(string fullPath)
        {
            using var request = UnityWebRequestTexture.GetTexture(ToUri(fullPath));

            await request.SendWebRequest().ToUniTask(cancelImmediately: false);

            return request.result != UnityWebRequest.Result.Success
                ? null
                : DownloadHandlerTexture.GetContent(request);
        }

        private static string ToUri(string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');

            return normalized.Contains("://") ? normalized : "file:///" + normalized.TrimStart('/');
        }

        private static string LevelFolderName(int levelNumber) => $"{LevelFolderLabel}{levelNumber:00}";
    }
}
