using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

namespace Creobit.EditionsUpgrade
{
    public class JsonService
    {
        public static void Create<T>(string path, T data) where T : class
        {
            if (!typeof(T).IsDefined(typeof(SerializableAttribute), false))
            {
                throw new InvalidOperationException($"The class {typeof(T).Name} must be marked as [Serializable].");
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "Data cannot be null.");
            }

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            File.WriteAllText(path, json);
        }

        public static async UniTask<T> LoadAsync<T>(string path, Action onFail = null) where T : class
        {
            try
            {
                if (!typeof(T).IsDefined(typeof(SerializableAttribute), false))
                {
                    throw new InvalidOperationException($"The class {typeof(T).Name} must be marked as [Serializable].");
                }

#if UNITY_STANDALONE || UNITY_IOS
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"File not found at path: {path}");
                }

                string json = await File.ReadAllTextAsync(path);
                var result = JsonConvert.DeserializeObject<T>(json);

                if (result == null)
                {
                    throw new InvalidDataException("The JSON data does not match the expected class structure.");
                }

                return result;

#elif UNITY_ANDROID || (UNITY_EDITOR && UNITY_ANDROID)
                return await LoadFromAndroidAsync<T>(path);
#else
        throw new PlatformNotSupportedException("Unsupported platform");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading JSON: {ex.Message}");
                onFail?.Invoke();
                return null;
            }
        }

        private static async UniTask<T> LoadFromAndroidAsync<T>(string path) where T : class
        {
#if UNITY_EDITOR
            path = "file://" + path;
#endif

            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load file from path: {path}, error: {request.error}");
                    return null;
                }

                try
                {
                    var result = JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
                    if (result == null)
                    {
                        throw new InvalidDataException("The JSON data does not match the expected class structure.");
                    }

                    return result;
                }
                catch (JsonException ex)
                {
                    Debug.LogError($"Failed to deserialize JSON: {ex.Message}");
                    return null;
                }
            }
        }
    }
}