using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Creobit.LA8.Build
{
    public class BuildIntegrityGate : IPreprocessBuildWithReport
    {
        private const string PackagesFolder = "Assets/Packages";

        private static readonly string[] DevOnlyDefines = { "CREOBIT_DEBUG", "CREOBIT_DEVELOP" };

        private static readonly string[] EditorOnlyPluginPrefixes = { "Google.Apis" };

        private static readonly string[] ExpectedScenes = { "Bootstrap", "Meta", "Gameplay" };

        private static readonly string[] ExpectedLocales = { "en", "br", "de", "es", "fr", "it", "nl", "pl", "ru" };

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            EnforceRunInBackground(target);
            var problems = Validate(target);

            if (problems.Count == 0)
            {
                Debug.Log($"[BuildIntegrityGate] Проверки пройдены (target {target}).");
                return;
            }

            throw new BuildFailedException(Describe(target, problems));
        }

        public static List<string> Validate(BuildTarget target)
        {
            var namedTarget = EditionArtifacts.ToNamedBuildTarget(target);
            var problems = new List<string>();

            CheckDevDefines(namedTarget, problems);
            CheckScenes(problems);
            CheckAddressableGroups(problems);
            CheckShipLocales(problems);
            CheckCampaignBounds(namedTarget, problems);
            CheckEditorOnlyPlugins(target, problems);
            CheckDuplicateManagedPlugins(problems);

            return problems;
        }

        public static string Describe(BuildTarget target, List<string> problems)
        {
            if (problems.Count == 0)
            {
                return $"Проверки проекта пройдены (target {target}).";
            }

            return $"Проверки проекта не пройдены (target {target}):" + Environment.NewLine +
                   string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem));
        }

        [MenuItem("8floor/Build/Проверить проект перед сборкой", false, 40)]
        private static void ValidateFromMenu()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var problems = Validate(target);
            var message = Describe(target, problems);

            Debug.Log($"[BuildIntegrityGate] {message}");
            EditorUtility.DisplayDialog("Проверка проекта", message, "OK");
        }

        private static void CheckDevDefines(NamedBuildTarget namedTarget, List<string> problems)
        {
            var defines = ResolveDefines(namedTarget);
            var found = DevOnlyDefines.Where(define => defines.Contains(define, StringComparer.Ordinal)).ToArray();

            if (found.Length > 0)
            {
                problems.Add($"отладочные дефайны уедут в релиз: {string.Join(", ", found)}");
            }
        }

        private static void EnforceRunInBackground(BuildTarget target)
        {
            if (!IsStandalone(target) || !PlayerSettings.runInBackground)
            {
                return;
            }

            PlayerSettings.runInBackground = false;
            Debug.Log("[BuildIntegrityGate] runInBackground выключен для десктопной сборки.");
        }

        private static void CheckScenes(List<string> problems)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();

            var missing = scenes.Where(scene => !File.Exists(scene.path)).Select(scene => scene.path).ToArray();
            if (missing.Length > 0)
            {
                problems.Add($"сцены не найдены на диске: {string.Join(", ", missing)}");
            }

            var names = scenes.Select(scene => Path.GetFileNameWithoutExtension(scene.path)).ToArray();
            if (!names.SequenceEqual(ExpectedScenes, StringComparer.Ordinal))
            {
                problems.Add(
                    $"состав сцен изменился: ожидалось {string.Join(", ", ExpectedScenes)}, " +
                    $"в сборке {(names.Length == 0 ? "пусто" : string.Join(", ", names))}");
            }
        }

        private static void CheckAddressableGroups(List<string> problems)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                problems.Add("настройки Addressables не найдены — контент в сборку не попадёт");
                return;
            }

            var excluded = settings.groups
                .Where(group => group != null)
                .Where(group =>
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    return schema != null && !schema.IncludeInBuild;
                })
                .Select(group => group.Name)
                .ToArray();

            if (excluded.Length > 0)
            {
                problems.Add(
                    $"группы Addressables исключены из сборки: {string.Join(", ", excluded)}. " +
                    "Вернуть через 8floor/Build/Демо: паки 2-5 в сборке");
            }
        }

        private static void CheckShipLocales(List<string> problems)
        {
            var locales = EditionArtifacts.ResolveShipLocales();
            var found = locales.OrderBy(code => code, StringComparer.Ordinal).ToArray();
            var expected = ExpectedLocales.OrderBy(code => code, StringComparer.Ordinal).ToArray();

            if (!found.SequenceEqual(expected, StringComparer.Ordinal))
            {
                problems.Add(
                    $"набор языков изменился: ожидалось {string.Join(", ", ExpectedLocales)}, " +
                    $"найдено {string.Join(", ", locales)}. Обновить languages в конфиге фабрики и логотипы издания");
            }
        }

        private static void CheckCampaignBounds(NamedBuildTarget namedTarget, List<string> problems)
        {
            var guids = AssetDatabase.FindAssets("t:AllLevelsSO");
            if (guids.Length != 1)
            {
                problems.Add($"ожидался ровно один AllLevelsSO, найдено {guids.Length}");
                return;
            }

            var allLevels = AssetDatabase.LoadAssetAtPath<AllLevelsSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (allLevels == null)
            {
                problems.Add("AllLevelsSO не загрузился");
                return;
            }

            var total = allLevels.AllLevels == null ? 0 : allLevels.AllLevels.Count;
            if (total == 0)
            {
                problems.Add("в AllLevelsSO нет уровней");
                return;
            }

            var main = allLevels.MainCampaignLevels;
            if (main <= 0 || main >= total)
            {
                problems.Add(
                    $"MainCampaignLevels={main} вне диапазона 1..{total - 1} при {total} уровнях — " +
                    "SE и CE перестанут различаться");
                return;
            }

            var cap = allLevels.LimitLevels ? Mathf.Min(total, allLevels.MaxLevel) : total;
            var isCollector = ResolveDefines(namedTarget).Contains("COLLECTOR", StringComparer.Ordinal);
            var expected = isCollector ? cap : Mathf.Min(cap, main);

            if (allLevels.MaxCampaignLevel != expected)
            {
                problems.Add(
                    $"MaxCampaignLevel={allLevels.MaxCampaignLevel}, ожидалось {expected} " +
                    $"(COLLECTOR {(isCollector ? "есть" : "нет")}, уровней {total}, основная кампания {main}). " +
                    "Редактор скомпилирован не под это издание");
            }
        }

        private static void CheckEditorOnlyPlugins(BuildTarget target, List<string> problems)
        {
            if (!Directory.Exists(PackagesFolder))
            {
                return;
            }

            var leaking = new List<string>();
            foreach (var prefix in EditorOnlyPluginPrefixes)
            {
                foreach (var path in Directory.GetFiles(PackagesFolder, prefix + "*.dll", SearchOption.AllDirectories))
                {
                    var assetPath = path.Replace(Path.DirectorySeparatorChar, '/');
                    if (!(AssetImporter.GetAtPath(assetPath) is PluginImporter importer))
                    {
                        continue;
                    }

                    if (importer.GetCompatibleWithAnyPlatform() || importer.GetCompatibleWithPlatform(target))
                    {
                        leaking.Add(assetPath);
                    }
                }
            }

            if (leaking.Count > 0)
            {
                problems.Add(
                    $"editor-only библиотеки помечены совместимыми с плеером: {string.Join(", ", leaking)}. " +
                    "Снять Any Platform и оставить только Editor");
            }
        }

        private static void CheckDuplicateManagedPlugins(List<string> problems)
        {
            if (!Directory.Exists(PackagesFolder))
            {
                return;
            }

            var duplicates = Directory.GetFiles(PackagesFolder, "*.dll", SearchOption.AllDirectories)
                .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} ({group.Count()} шт.)")
                .ToArray();

            if (duplicates.Length > 0)
            {
                problems.Add(
                    $"в {PackagesFolder} одноимённые сборки разных версий: {string.Join(", ", duplicates)}. " +
                    "IL2CPP на этом падает");
            }
        }

        private static string[] ResolveDefines(NamedBuildTarget namedTarget)
        {
            return PlayerSettings.GetScriptingDefineSymbols(namedTarget)
                .Split(';')
                .Select(define => define.Trim())
                .Where(define => define.Length > 0)
                .ToArray();
        }

        private static bool IsStandalone(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows
                   || target == BuildTarget.StandaloneWindows64
                   || target == BuildTarget.StandaloneOSX
                   || target == BuildTarget.StandaloneLinux64;
        }
    }
}
