using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    public class FileSaveProvider : IPlayerPrefsSaveProvider
    {
        public const string LinkedPrefsKey = "Creobit.FileSave.Linked";

        private const string SaveFileName = "save.json";
        private const string TempFileName = "save.json.tmp";

        private readonly Dictionary<string, string> _values = new();

        private bool _loaded;

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static string TempFilePath => Path.Combine(Application.persistentDataPath, TempFileName);

        public UniTask Load()
        {
            EnsureLoaded();

            return UniTask.CompletedTask;
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            try
            {
                var path = File.Exists(SaveFilePath)
                    ? SaveFilePath
                    : File.Exists(TempFilePath)
                        ? TempFilePath
                        : null;

                if (path != null)
                {
                    var json = File.ReadAllText(path);
                    var stored = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                    if (stored != null)
                    {
                        foreach (var pair in stored)
                        {
                            _values[pair.Key] = pair.Value;
                        }
                    }

                    if (path == TempFilePath && stored != null)
                    {
                        File.Move(TempFilePath, SaveFilePath);
                        Debug.LogWarning($"FileSaveProvider: {SaveFilePath} отсутствовал, данные восстановлены из {TempFilePath}");
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"FileSaveProvider: не удалось прочитать {SaveFilePath}: {exception.Message}");
            }
        }

        public void Save(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            EnsureLoaded();

            _values[key] = value;
            Flush();
        }

        public string TryGetValue(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            EnsureLoaded();

            if (_values.TryGetValue(key, out var stored) && !string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            var legacy = PlayerPrefs.GetString(key, string.Empty);

            if (string.IsNullOrEmpty(legacy))
            {
                return defaultValue;
            }

            _values[key] = legacy;
            Flush();
            Debug.Log($"FileSaveProvider: ключ '{key}' перенесён из PlayerPrefs в {SaveFilePath}");

            return legacy;
        }

        public void DeleteAll()
        {
            _loaded = true;

            _values.Clear();

            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }

                if (File.Exists(TempFilePath))
                {
                    File.Delete(TempFilePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"FileSaveProvider: не удалось удалить {SaveFilePath}: {exception.Message}");
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        private void Flush()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_values, Formatting.Indented);

                File.WriteAllText(TempFilePath, json);

                if (File.Exists(SaveFilePath))
                {
                    File.Replace(TempFilePath, SaveFilePath, null);
                }
                else
                {
                    File.Move(TempFilePath, SaveFilePath);
                }

                PlayerPrefs.SetInt(LinkedPrefsKey, 1);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogError($"FileSaveProvider: не удалось записать {SaveFilePath}: {exception.Message}");
            }
        }
    }
}
