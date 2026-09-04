using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif

namespace Creobit.Localization.Utils
{
    public static class StreamingAssetsHelper
    {
        /// <summary>
        /// Determines whether a file exists in the StreamingAssets directory.
        /// </summary>
        /// <param name="fileName">The name of the file to check for existence.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public static bool FileExists(string fileName)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID
            // Android requires special handling as files are in the APK
            return true; // You'll need to actually try loading the file to know
#else
            return File.Exists(fullPath);
#endif
        }

        public static string GetFilePath(string fileName)
        {
            return Path.Combine(Application.streamingAssetsPath, fileName);
        }

        // For synchronous file loading (not recommended for Android)
        /// <summary>
        /// Loads the content of a text file from the StreamingAssets directory synchronously.
        /// </summary>
        /// <param name="fileName">The name of the file to be loaded.</param>
        /// <returns>The content of the file as a string, or null if the file does not exist.</returns>
        public static string LoadTextFile(string fileName)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            UnityWebRequest www = UnityWebRequest.Get(fullPath);
            www.SendWebRequest();
            // Warning: This will freeze the game until the file is loaded
            while (!www.isDone) { }
            return www.downloadHandler.text.TrimStart((char)0xFEFF);
#else
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            return null;
#endif
        }

        // Recommended way to load files (async)
        /// <summary>
        /// Asynchronously loads the content of a text file from the StreamingAssets directory.
        /// </summary>
        /// <param name="fileName">The name of the file to be loaded.</param>
        /// <returns>A UniTask representing the asynchronous operation. The result contains the file's content as a string if successfully loaded, or null if the file is not found or an error occurs.</returns>
        public static async UniTask<string> LoadTextFileAsync(string fileName)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID
            using (UnityWebRequest www = UnityWebRequest.Get(fullPath))
            {
                await www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    return www.downloadHandler.text.TrimStart((char)0xFEFF);
                }
                Debug.LogError($"Error loading file: {www.error}");
                return null;
            }
#else
            if (File.Exists(fullPath))
            {
                return await File.ReadAllTextAsync(fullPath);
            }

            return null;
#endif
        }
    }
}