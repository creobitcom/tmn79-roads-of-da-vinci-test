using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Creobit.LA8.Build
{
    public static class DemoContentTool
    {
        public const string DemoGroupName = "Levels-Pack02-05";
        private const string SourceGroupName = "Default Local Group";

        private static readonly Regex ExcludedPacks =
            new Regex(@"^Assets/LA8_slv/Levels/Pack0[2345]/", RegexOptions.IgnoreCase);

        [MenuItem("8floor/Build/Демо: паки 2-5 вне сборки", false, 20)]
        public static void ExcludePacks()
        {
            var settings = ResolveSettings();
            if (settings == null)
                return;

            var demoGroup = ResolveDemoGroup(settings);
            var moved = MoveIntoDemoGroup(settings, demoGroup);
            SetIncludeInBuild(demoGroup, false);

            AssetDatabase.SaveAssets();
            Report(demoGroup, moved, "паки 2-5 ВНЕ сборки");
        }

        [MenuItem("8floor/Build/Демо: паки 2-5 в сборке", false, 21)]
        public static void IncludePacks()
        {
            var settings = ResolveSettings();
            if (settings == null)
                return;

            var demoGroup = settings.FindGroup(DemoGroupName);
            if (demoGroup == null)
            {
                Debug.Log($"[DemoContent] Группы '{DemoGroupName}' нет — паки и так в сборке.");
                return;
            }

            SetIncludeInBuild(demoGroup, true);
            AssetDatabase.SaveAssets();
            Report(demoGroup, 0, "паки 2-5 В сборке");
        }

        [MenuItem("8floor/Build/Демо: показать состояние", false, 22)]
        private static void ShowState()
        {
            var settings = ResolveSettings();
            if (settings == null)
                return;

            var demoGroup = settings.FindGroup(DemoGroupName);
            if (demoGroup == null)
            {
                Debug.Log($"[DemoContent] Группы '{DemoGroupName}' нет. Все паки идут в сборку.");
                return;
            }

            Report(demoGroup, 0, IsExcluded(demoGroup) ? "паки 2-5 ВНЕ сборки" : "паки 2-5 В сборке");
        }

        public static bool IsExcluded(AddressableAssetGroup group)
        {
            var schema = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
            return schema != null && !schema.IncludeInBuild;
        }

        private static AddressableAssetSettings ResolveSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                Debug.LogError("[DemoContent] Настройки Addressables не найдены.");

            return settings;
        }

        private static AddressableAssetGroup ResolveDemoGroup(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(DemoGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(DemoGroupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                Debug.Log($"[DemoContent] Создана группа '{DemoGroupName}'.");
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null && schema.BundleNaming != BundledAssetGroupSchema.BundleNamingStyle.FileNameHash)
            {
                schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.FileNameHash;
                Debug.Log("[DemoContent] Bundle Naming Mode выставлен в File Name Hash.");
            }

            return group;
        }

        private static int MoveIntoDemoGroup(AddressableAssetSettings settings, AddressableAssetGroup demoGroup)
        {
            var source = settings.FindGroup(SourceGroupName);
            if (source == null)
            {
                Debug.LogError($"[DemoContent] Группа '{SourceGroupName}' не найдена — переносить неоткуда.");
                return 0;
            }

            var candidates = source.entries
                .Where(entry => entry != null && !entry.IsFolder)
                .Where(entry => !string.IsNullOrEmpty(entry.AssetPath))
                .Where(entry => ExcludedPacks.IsMatch(entry.AssetPath.Replace('\\', '/')))
                .ToList();

            if (candidates.Count == 0)
                return 0;

            settings.MoveEntries(candidates, demoGroup);
            return candidates.Count;
        }

        private static void SetIncludeInBuild(AddressableAssetGroup group, bool include)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                Debug.LogError($"[DemoContent] У группы '{group.Name}' нет BundledAssetGroupSchema.");
                return;
            }

            schema.IncludeInBuild = include;
        }

        private static void Report(AddressableAssetGroup group, int moved, string state)
        {
            var byPack = new SortedDictionary<string, int>();
            foreach (var entry in group.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.AssetPath))
                    continue;

                var match = Regex.Match(entry.AssetPath.Replace('\\', '/'), @"Levels/(Pack\d+)/");
                var key = match.Success ? match.Groups[1].Value : "прочее";
                byPack.TryGetValue(key, out var count);
                byPack[key] = count + 1;
            }

            var breakdown = byPack.Count == 0
                ? "пусто"
                : string.Join(", ", byPack.Select(pair => $"{pair.Key}: {pair.Value}"));

            Debug.Log(
                $"[DemoContent] {state}. Группа '{group.Name}': {group.entries.Count} записей ({breakdown})." +
                (moved > 0 ? $" Перенесено сейчас: {moved}." : string.Empty));
        }
    }
}
