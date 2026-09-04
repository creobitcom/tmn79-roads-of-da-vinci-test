#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Creobit.UI.Utility;
using UnityEditor;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Editor
{
    [InitializeOnLoad]
    public static class MetaUiPreloadValidator
    {
        private const string BootstrapScopePrefabPath =
            "Assets/_ProjectTemplate/Common/Bootstrap/Prefabs/BootstrapScope.prefab";

        private const string MetaScenePath =
            "Assets/_ProjectTemplate/Common/Bootstrap/Scenes/Meta.unity";

        private const string MenuPath = "Tools/Creobit/Bootstrap/Validate Meta UI Preload";

        private static readonly Regex PanelListPropertyRegex = new(
            @"propertyPath:\s+(_loadedPanels|_starterPanels|_preservedPanels)\.Array\.data\[\d+\]\.<Reference>k__BackingField",
            RegexOptions.Compiled);

        private static readonly Regex ObjectReferenceGuidRegex = new(
            @"objectReference:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})",
            RegexOptions.Compiled);

        private static readonly Regex AssetGuidRegex = new(
            @"m_AssetGUID:\s*([0-9a-f]{32})",
            RegexOptions.Compiled);

        static MetaUiPreloadValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        private static void ValidateFromMenu()
        {
            var missing = FindPanelsMissingFromPreload();

            if (missing.Count == 0)
            {
                Debug.Log("Meta UI preload: все панели UILoader есть в MetaUiPreloadAssets у BootstrapScope.prefab.");
                return;
            }

            LogMissing(missing);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            var missing = FindPanelsMissingFromPreload();

            if (missing.Count > 0)
            {
                LogMissing(missing);
            }
        }

        private static void LogMissing(List<string> missing)
        {
            Debug.LogWarning(
                "Meta UI preload: этих панелей UILoader нет в MetaUiPreloadAssets у BootstrapScope.prefab, "
                + "они будут грузиться дольше при первом показе:\n- "
                + string.Join("\n- ", missing));
        }

        private static List<string> FindPanelsMissingFromPreload()
        {
            var missing = new List<string>();

            if (!File.Exists(BootstrapScopePrefabPath) || !File.Exists(MetaScenePath))
            {
                return missing;
            }

            var preloadedPanelGuids = ReadPreloadedPanelGuids();

            foreach (var panelReferenceGuid in ReadUiLoaderPanelReferenceGuids())
            {
                var path = AssetDatabase.GUIDToAssetPath(panelReferenceGuid);

                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var panelReference = AssetDatabase.LoadAssetAtPath<PanelReference>(path);

                if (panelReference == null || panelReference.UIPanelReference == null)
                {
                    continue;
                }

                var panelAssetGuid = panelReference.UIPanelReference.AssetGUID;

                if (string.IsNullOrEmpty(panelAssetGuid) || preloadedPanelGuids.Contains(panelAssetGuid))
                {
                    continue;
                }

                missing.Add($"{panelReference.name} ({path})");
            }

            return missing;
        }

        private static HashSet<string> ReadPreloadedPanelGuids()
        {
            var guids = new HashSet<string>();
            var insideArray = false;

            foreach (var line in File.ReadLines(BootstrapScopePrefabPath))
            {
                if (line.Contains("<MetaUiPreloadAssets>k__BackingField:"))
                {
                    insideArray = true;
                    continue;
                }

                if (!insideArray)
                {
                    continue;
                }

                if (line.Contains("k__BackingField"))
                {
                    break;
                }

                var match = AssetGuidRegex.Match(line);

                if (match.Success)
                {
                    guids.Add(match.Groups[1].Value);
                }
            }

            return guids;
        }

        private static HashSet<string> ReadUiLoaderPanelReferenceGuids()
        {
            var guids = new HashSet<string>();
            var pendingReference = false;

            foreach (var line in File.ReadLines(MetaScenePath))
            {
                if (PanelListPropertyRegex.IsMatch(line))
                {
                    pendingReference = true;
                    continue;
                }

                if (!pendingReference)
                {
                    continue;
                }

                var match = ObjectReferenceGuidRegex.Match(line);

                if (match.Success)
                {
                    guids.Add(match.Groups[1].Value);
                    pendingReference = false;
                }
                else if (line.Contains("propertyPath:"))
                {
                    pendingReference = false;
                }
            }

            return guids;
        }
    }
}
#endif
