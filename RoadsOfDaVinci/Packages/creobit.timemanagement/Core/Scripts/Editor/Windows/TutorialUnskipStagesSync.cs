using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using UnityEditor;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Windows
{
    public class TutorialUnskipStagesSync : AssetPostprocessor
    {
        private static Dictionary<string, List<TutorialViewSo>> _byPanelGuid;

        [MenuItem("Tools/Creobit/TMN/Обновить непропускаемые стадии туториалов")]
        private static void SyncAll()
        {
            _byPanelGuid = null;

            var updated = 0;

            foreach (var tutorial in LoadAllTutorials())
            {
                if (SyncTutorial(tutorial))
                {
                    updated++;
                }
            }

            if (updated > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[Туториалы] Непропускаемые стадии обновлены у {updated} окон.");
        }

        public static bool SyncTutorial(TutorialViewSo tutorial)
        {
            var panel = LoadPanel(tutorial);

            if (panel == null)
            {
                return false;
            }

            var stages = panel.CollectUnskipStages();

            if (SameStages(tutorial.unskipStages, stages))
            {
                return false;
            }

            tutorial.unskipStages = stages;
            EditorUtility.SetDirty(tutorial);

            return true;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (HasTutorialAsset(importedAssets) || HasTutorialAsset(deletedAssets)
                || HasTutorialAsset(movedAssets))
            {
                _byPanelGuid = null;
            }

            var updated = false;

            foreach (var path in importedAssets)
            {
                if (!path.EndsWith(".prefab"))
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(path);

                if (string.IsNullOrEmpty(guid) || !ByPanelGuid.TryGetValue(guid, out var tutorials))
                {
                    continue;
                }

                foreach (var tutorial in tutorials)
                {
                    updated |= SyncTutorial(tutorial);
                }
            }

            if (updated)
            {
                EditorApplication.delayCall += AssetDatabase.SaveAssets;
            }
        }

        private static Dictionary<string, List<TutorialViewSo>> ByPanelGuid => _byPanelGuid ??= BuildMap();

        private static Dictionary<string, List<TutorialViewSo>> BuildMap()
        {
            var map = new Dictionary<string, List<TutorialViewSo>>();

            foreach (var tutorial in LoadAllTutorials())
            {
                var guid = GetPanelGuid(tutorial);

                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                if (!map.TryGetValue(guid, out var tutorials))
                {
                    tutorials = new List<TutorialViewSo>();
                    map[guid] = tutorials;
                }

                tutorials.Add(tutorial);
            }

            return map;
        }

        private static IEnumerable<TutorialViewSo> LoadAllTutorials()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:TutorialViewSo"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tutorial = AssetDatabase.LoadAssetAtPath<TutorialViewSo>(path);

                if (tutorial != null)
                {
                    yield return tutorial;
                }
            }
        }

        private static TutorialView LoadPanel(TutorialViewSo tutorial)
        {
            var guid = GetPanelGuid(tutorial);

            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);

            return prefab != null ? prefab.GetComponentInChildren<TutorialView>(true) : null;
        }

        private static string GetPanelGuid(TutorialViewSo tutorial)
        {
            if (tutorial == null || tutorial.panelReference == null
                || tutorial.panelReference.UIPanelReference == null)
            {
                return null;
            }

            return tutorial.panelReference.UIPanelReference.AssetGUID;
        }

        private static bool HasTutorialAsset(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (path.EndsWith(".asset"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SameStages(List<int> current, List<int> collected)
        {
            if (current == null)
            {
                return collected.Count == 0;
            }

            if (current.Count != collected.Count)
            {
                return false;
            }

            for (var index = 0; index < current.Count; index++)
            {
                if (current[index] != collected[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
