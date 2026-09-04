using System.Collections.Generic;
using System.Text;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using UnityEditor;
using UnityEngine;

namespace Creobit.LA8.EditorTools
{
    public static class CollectiblesCampaignMapper
    {
        private const string FillMenuPath = "8floor/Collectibles/Bind Collectibles To Levels";
        private const string ReportMenuPath = "8floor/Collectibles/Check Collectibles Binding";

        [MenuItem(FillMenuPath)]
        private static void FillMenu()
        {
            EditorUtility.DisplayDialog("Collectibles binding", Fill(), "OK");
        }

        [MenuItem(ReportMenuPath)]
        private static void ReportMenu()
        {
            EditorUtility.DisplayDialog("Collectibles binding", Report(), "OK");
        }

        public static string Fill()
        {
            var changes = new List<string>();

            foreach (var allLevels in LoadAssets<AllLevelsSO>())
            {
                if (allLevels.AllLevels == null)
                {
                    continue;
                }

                var levels = allLevels.AllLevels;
                var changed = false;

                for (var i = 0; i < levels.Count; i++)
                {
                    var level = levels[i];
                    var inPrefab = ReadLevelPrefabCollectibles(level);

                    if (inPrefab.Artifact != level.MapArtifact)
                    {
                        changes.Add($"{allLevels.name} level {level.levelNum}: artifact " +
                                    $"{NameOf(level.MapArtifact)} -> {NameOf(inPrefab.Artifact)}");
                        level.MapArtifact = inPrefab.Artifact;
                        changed = true;
                    }

                    if (inPrefab.Trophy != level.MapTrophy)
                    {
                        changes.Add($"{allLevels.name} level {level.levelNum}: trophy " +
                                    $"{NameOf(level.MapTrophy)} -> {NameOf(inPrefab.Trophy)}");
                        level.MapTrophy = inPrefab.Trophy;
                        changed = true;
                    }

                    levels[i] = level;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(allLevels);
                }
            }

            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            report.AppendLine($"Bindings changed: {changes.Count}");

            foreach (var change in changes)
            {
                report.AppendLine($"  {change}");
            }

            report.Append(Report());

            var result = report.ToString();
            Debug.Log($"[CollectiblesCampaignMapper] {result}");

            return result;
        }

        public static string Report()
        {
            var issues = GetIssues();
            var report = new StringBuilder();
            report.AppendLine($"Binding issues: {issues.Count}");

            foreach (var issue in issues)
            {
                report.AppendLine($"  {issue}");
            }

            return report.ToString();
        }

        public static IReadOnlyList<string> GetIssues()
        {
            var issues = new List<string>();
            var bound = new HashSet<CollectibleItemSO>();

            foreach (var allLevels in LoadAssets<AllLevelsSO>())
            {
                if (allLevels.AllLevels == null)
                {
                    continue;
                }

                foreach (var level in allLevels.AllLevels)
                {
                    var inPrefab = ReadLevelPrefabCollectibles(level);

                    if (inPrefab.Extra != null)
                    {
                        issues.Add($"{allLevels.name} level {level.levelNum}: prefab holds more than one " +
                                   $"collectible of the same type ({inPrefab.Extra.name})");
                    }

                    if (inPrefab.Artifact != level.MapArtifact)
                    {
                        issues.Add($"{allLevels.name} level {level.levelNum}: MapArtifact is " +
                                   $"{NameOf(level.MapArtifact)}, prefab gives {NameOf(inPrefab.Artifact)}");
                    }

                    if (inPrefab.Trophy != level.MapTrophy)
                    {
                        issues.Add($"{allLevels.name} level {level.levelNum}: MapTrophy is " +
                                   $"{NameOf(level.MapTrophy)}, prefab gives {NameOf(inPrefab.Trophy)}");
                    }

                    if (level.MapArtifact != null)
                    {
                        bound.Add(level.MapArtifact);
                    }

                    if (level.MapTrophy != null)
                    {
                        bound.Add(level.MapTrophy);
                    }
                }
            }

            foreach (var database in LoadAssets<CollectiblesDatabaseSO>())
            {
                if (database.Groups == null)
                {
                    continue;
                }

                foreach (var group in database.Groups)
                {
                    if (group == null || group.Items == null)
                    {
                        continue;
                    }

                    foreach (var item in group.Items)
                    {
                        if (item == null || bound.Contains(item))
                        {
                            continue;
                        }

                        issues.Add($"{item.name} is in {database.name} but no level gives it");
                    }
                }
            }

            return issues;
        }

        private struct LevelCollectibles
        {
            public CollectibleItemSO Artifact;
            public CollectibleItemSO Trophy;
            public CollectibleItemSO Extra;
        }

        private static LevelCollectibles ReadLevelPrefabCollectibles(LevelReferenceByNum level)
        {
            var result = new LevelCollectibles();

            if (level.level == null || string.IsNullOrEmpty(level.level.AssetGUID))
            {
                return result;
            }

            var levelPath = AssetDatabase.GUIDToAssetPath(level.level.AssetGUID);
            var levelSo = AssetDatabase.LoadAssetAtPath<LevelBaseSO>(levelPath);

            if (levelSo == null || levelSo.LevelPrefab == null)
            {
                return result;
            }

            var prefabPath = AssetDatabase.GetAssetPath(levelSo.LevelPrefab);

            if (string.IsNullOrEmpty(prefabPath))
            {
                return result;
            }

            foreach (var dependency in AssetDatabase.GetDependencies(prefabPath, false))
            {
                var item = AssetDatabase.LoadAssetAtPath<CollectibleItemSO>(dependency);

                if (item == null)
                {
                    continue;
                }

                if (item.Type == CollectibleType.Trophy)
                {
                    if (result.Trophy == null)
                    {
                        result.Trophy = item;
                    }
                    else
                    {
                        result.Extra = item;
                    }

                    continue;
                }

                if (result.Artifact == null)
                {
                    result.Artifact = item;
                }
                else
                {
                    result.Extra = item;
                }
            }

            return result;
        }

        private static string NameOf(CollectibleItemSO item)
        {
            return item != null ? item.name : "none";
        }

        private static List<T> LoadAssets<T>() where T : ScriptableObject
        {
            var result = new List<T>();

            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }
    }
}
