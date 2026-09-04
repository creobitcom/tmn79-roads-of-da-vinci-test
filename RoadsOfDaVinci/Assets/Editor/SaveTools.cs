using System;
using System.IO;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using UnityEditor;
using UnityEngine;

namespace Creobit.LA8.EditorTools
{
    [InitializeOnLoad]
    public static class SaveTools
    {
        private const string ClearMenuPath = "8floor/Saves/Clear All Saves (file + PlayerPrefs)";
        private const string RevealMenuPath = "8floor/Saves/Show Save Folder";
        private const string OrphanSuffix = ".orphan";
        private const double OrphanCheckIntervalSeconds = 1d;

        private static double _nextOrphanCheck;

        static SaveTools()
        {
            EditorApplication.update += CheckOrphanedSaveFile;
        }

        private static void CheckOrphanedSaveFile()
        {
            if (EditorApplication.timeSinceStartup < _nextOrphanCheck)
                return;

            _nextOrphanCheck = EditorApplication.timeSinceStartup + OrphanCheckIntervalSeconds;

            if (PlayerPrefs.HasKey(FileSaveProvider.LinkedPrefsKey) || !File.Exists(FileSaveProvider.SaveFilePath))
                return;

            var orphanPath = FileSaveProvider.SaveFilePath + OrphanSuffix;

            try
            {
                if (File.Exists(orphanPath))
                    File.Delete(orphanPath);

                File.Move(FileSaveProvider.SaveFilePath, orphanPath);

                Debug.LogWarning(
                    "[SaveTools] PlayerPrefs очищены, поэтому файл сейва больше не считается активным и отложен в " +
                    $"{orphanPath}. Вернуть его можно, переименовав обратно в save.json.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveTools] Не удалось отложить {FileSaveProvider.SaveFilePath}: {exception.Message}");
            }
        }

        [MenuItem(ClearMenuPath)]
        private static void ClearAllSaves()
        {
            var message =
                $"Будут удалены:\n\n{FileSaveProvider.SaveFilePath}\n\nи все PlayerPrefs (реестр).\n\nОтменить нельзя.";

            if (!EditorUtility.DisplayDialog("Очистить сейвы", message, "Удалить", "Отмена"))
                return;

            PerformClear();
        }

        [MenuItem(RevealMenuPath)]
        private static void ShowSaveFolder()
        {
            EditorUtility.RevealInFinder(FileSaveProvider.SaveFilePath);
        }

        public static string PerformClear()
        {
            var removed = 0;

            foreach (var path in new[] { FileSaveProvider.SaveFilePath, FileSaveProvider.TempFilePath })
            {
                try
                {
                    if (!File.Exists(path))
                        continue;

                    File.Delete(path);
                    removed++;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[SaveTools] Не удалось удалить {path}: {exception.Message}");
                }
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            var result = $"[SaveTools] Сейвы очищены: файлов удалено {removed}, PlayerPrefs очищены.";
            Debug.Log(result);

            return result;
        }
    }
}
