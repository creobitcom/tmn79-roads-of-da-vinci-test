using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using EditionsUpgrade.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Creobit.LA8.Build
{
    public static class BuildTool
    {
        private const string OutputRoot = "Builds";
        private const int MaxWindowsPathLength = 255;

        private static readonly string[] DevOnlyDefines =
        {
            "CREOBIT_DEBUG",
            "CREOBIT_DEVELOP"
        };

        private static readonly string[] DontShipPatterns =
        {
            "*_BurstDebugInformation_DoNotShip",
            "*_BackUpThisFolder_ButDontShipItWithYourGame"
        };

        private static readonly string[] AdSdkMarkers =
        {
            "Assets/Yodo1/MAS/Scripts",
            "Assets/Yodo1/MAS/Editor/Dependencies/Yodo1MasAndroidDependencies.xml",
            "Assets/Yodo1/MAS/Editor/Dependencies/Yodo1MasiOSDependencies.xml",
            "Assets/Plugins/iOS/Yodo1MasUnityBridge"
        };

        private sealed class EditionProfile
        {
            public string Edition;
            public BuildTarget Target = BuildTarget.StandaloneWindows;
            public string StagingFolder;
            public string ReleaseToken;
            public string ProductNameOverride;
            public string BundleIdOverride;
            public bool FlatZip;
            public bool AppBundle;
            public bool ValidateOffers;
            public string[] RequiredDefines;
            public string[] ForbiddenDefines;
        }

        private static readonly EditionProfile SteamProfile = new EditionProfile
        {
            Edition = "steam_windows_premium",
            StagingFolder = "steam",
            ReleaseToken = "steam",
            ProductNameOverride = "Roads of Da Vinci",
            FlatZip = true,
            RequiredDefines = new[] { "PREMIUM", "COLLECTOR", "STEAM_BUILD" },
            ForbiddenDefines = new[] { "HAS_AD", "GOOGLE_PLAY", "MAC_APPSTORE", "HUAWEI", "UDP" }
        };

        private static readonly EditionProfile CeProfile = new EditionProfile
        {
            Edition = "8floor_windows_premium_ce",
            StagingFolder = "8floor_ce",
            ReleaseToken = "8floor_win_ce",
            ProductNameOverride = "Roads of Da Vinci Collectors Edition",
            FlatZip = false,
            RequiredDefines = new[] { "PREMIUM", "COLLECTOR" },
            ForbiddenDefines = new[] { "HAS_AD", "GOOGLE_PLAY", "MAC_APPSTORE", "HUAWEI", "UDP", "STEAM_BUILD", "UPGRADE" }
        };

        private static readonly EditionProfile SeProfile = new EditionProfile
        {
            Edition = "8floor_windows_premium_se",
            StagingFolder = "8floor_se",
            ReleaseToken = "8floor_win_se",
            ProductNameOverride = "Roads of Da Vinci",
            FlatZip = false,
            RequiredDefines = new[] { "PREMIUM" },
            ForbiddenDefines = new[] { "COLLECTOR", "HAS_AD", "GOOGLE_PLAY", "MAC_APPSTORE", "HUAWEI", "UDP", "STEAM_BUILD", "UPGRADE" }
        };

        private static readonly EditionProfile GooglePlayPremiumProfile = new EditionProfile
        {
            Edition = "googleplay_android_premium",
            Target = BuildTarget.Android,
            StagingFolder = "googleplay_premium",
            ReleaseToken = "googleplay_premium",
            ProductNameOverride = "Roads of Da Vinci",
            BundleIdOverride = "com.eighthfloor.roadsofdavinci1.premium",
            RequiredDefines = new[] { "PREMIUM", "GOOGLE_PLAY", "COLLECTOR", "UNITY_ANALYTICS", "UPGRADE" },
            ForbiddenDefines = new[] { "HAS_AD", "MAC_APPSTORE", "HUAWEI", "UDP", "STEAM_BUILD" }
        };

        private static readonly EditionProfile GooglePlayF2pProfile = new EditionProfile
        {
            Edition = "googleplay_android_f2p",
            Target = BuildTarget.Android,
            StagingFolder = "googleplay_f2p",
            ReleaseToken = "googleplay_f2p",
            ProductNameOverride = "Roads of Da Vinci Chapter 1",
            BundleIdOverride = "com.eighthfloor.roadsofdavinci1.f2p",
            ValidateOffers = true,
            RequiredDefines = new[] { "GOOGLE_PLAY", "HAS_AD", "COLLECTOR", "UNITY_ANALYTICS", "UPGRADE" },
            ForbiddenDefines = new[] { "PREMIUM", "MAC_APPSTORE", "HUAWEI", "UDP", "STEAM_BUILD" }
        };

        [MenuItem("Tools/LA8/Build Addressables")]
        public static void BuildAddressables()
        {
            Debug.Log("[Addressables] Refreshing AssetDatabase & Settings...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[Addressables] Starting BuildPlayerContent...");
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"[Addressables] Build FAILED: {result.Error}");
            }
            else
            {
                Debug.Log($"[Addressables] Build SUCCESS! Duration: {result.Duration:F2}s, Output: {result.OutputPath}");
            }
        }

        [MenuItem("8floor/Build/Steam Windows (x32)")]
        private static void BuildSteamWindowsFromMenu()
        {
            RunFromMenu(SteamProfile);
        }

        [MenuItem("8floor/Build/8floor Windows CE (x32)")]
        private static void Build8FloorWindowsCeFromMenu()
        {
            RunFromMenu(CeProfile);
        }

        [MenuItem("8floor/Build/8floor Windows SE (x32)")]
        private static void Build8FloorWindowsSeFromMenu()
        {
            RunFromMenu(SeProfile);
        }

        [MenuItem("8floor/Build/Google Play Android Premium (APK)")]
        private static void BuildGooglePlayAndroidPremiumFromMenu()
        {
            RunFromMenu(GooglePlayPremiumProfile);
        }

        [MenuItem("8floor/Build/Google Play Android F2P (APK)")]
        private static void BuildGooglePlayAndroidF2pFromMenu()
        {
            RunFromMenu(GooglePlayF2pProfile);
        }

        private static void RunFromMenu(EditionProfile profile)
        {
            var targetGroup = BuildPipeline.GetBuildTargetGroup(profile.Target);

            if (EditorUserBuildSettings.activeBuildTarget != profile.Target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, profile.Target))
            {
                Debug.LogError($"[BuildTool] Не удалось переключиться на {profile.Target}.");
                return;
            }

            try
            {
                Run(profile);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void BuildSteamWindows()
        {
            RunBatch(SteamProfile);
        }

        public static void Build8FloorWindowsCe()
        {
            RunBatch(CeProfile);
        }

        public static void Build8FloorWindowsSe()
        {
            RunBatch(SeProfile);
        }

        public static void BuildGooglePlayAndroidPremium()
        {
            RunBatch(GooglePlayPremiumProfile);
        }

        public static void BuildGooglePlayAndroidF2p()
        {
            RunBatch(GooglePlayF2pProfile);
        }

        private static void RunBatch(EditionProfile profile)
        {
            try
            {
                Run(profile);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                throw;
            }
        }

        private static void Run(EditionProfile profile)
        {
            if (EditorUserBuildSettings.activeBuildTarget != profile.Target)
            {
                throw new InvalidOperationException(
                    $"Активный target = {EditorUserBuildSettings.activeBuildTarget}, ожидался {profile.Target}. " +
                    $"Запускай Unity с -buildTarget {ResolveBuildTargetArgument(profile.Target)}.");
            }

            var namedTarget = EditionArtifacts.ToNamedBuildTarget(profile.Target);
            var defines = ResolveEditionDefines(profile);
            var definesLine = string.Join(";", defines);
            var scenes = ResolveScenes();

            AssertAdSdkMatchesEdition(profile, defines);

            EditionArtifacts.Apply(
                namedTarget,
                EditionArtifacts.UsesCollectorsLogo(definesLine),
                EditionArtifacts.UsesCollectorsIcon(definesLine));

            var previousProductName = PlayerSettings.productName;
            var previousBundleId = PlayerSettings.GetApplicationIdentifier(namedTarget);

            if (!string.IsNullOrEmpty(profile.ProductNameOverride))
                PlayerSettings.productName = profile.ProductNameOverride;

            if (!string.IsNullOrEmpty(profile.BundleIdOverride))
                PlayerSettings.SetApplicationIdentifier(namedTarget, profile.BundleIdOverride);

            try
            {
                if (profile.ValidateOffers)
                    Creobit.CI.F2POfferBuildValidator.Validate(profile.Edition, PlayerSettings.GetApplicationIdentifier(namedTarget));

                if (profile.Target == BuildTarget.Android)
                    RunAndroidBuild(profile, defines, scenes);
                else
                    RunBuild(profile, defines, scenes);
            }
            finally
            {
                PlayerSettings.productName = previousProductName;
                PlayerSettings.SetApplicationIdentifier(namedTarget, previousBundleId);
            }
        }

        private static string ResolveBuildTargetArgument(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.StandaloneOSX:
                    return "OSXUniversal";
                default:
                    return "Win";
            }
        }

        private static void AssertAdSdkMatchesEdition(EditionProfile profile, string[] defines)
        {
            if (profile.Target != BuildTarget.Android && profile.Target != BuildTarget.iOS)
                return;

            if (defines.Contains("HAS_AD"))
                return;

            var found = AdSdkMarkers
                .Where(marker => File.Exists(marker) || Directory.Exists(marker))
                .ToArray();

            if (found.Length == 0)
                return;

            throw new InvalidOperationException(
                $"Издание {profile.Edition} собирается без HAS_AD, но в проекте лежит рекламный SDK: " +
                $"{string.Join(", ", found)}. Собирай премиум-издания из ветки develop, f2p — из mobile_F2P.");
        }

        private static void RunBuild(EditionProfile profile, string[] defines, string[] scenes)
        {
            var buildFolder = Path.Combine(OutputRoot, profile.StagingFolder);
            var outputPath = Path.Combine(buildFolder, $"{SanitizeFileName(PlayerSettings.productName)}.exe");

            ClearStagingFolder(buildFolder);
            Directory.CreateDirectory(buildFolder);

            Debug.Log($"[BuildTool] Издание: {profile.Edition}");
            Debug.Log($"[BuildTool] Target: {EditorUserBuildSettings.activeBuildTarget}, backend: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)}");
            Debug.Log($"[BuildTool] Product: {PlayerSettings.productName}, version {PlayerSettings.bundleVersion}");
            Debug.Log($"[BuildTool] Defines: {string.Join(";", defines)}");
            Debug.Log($"[BuildTool] Scenes: {string.Join(", ", scenes)}");

            var previousRunInBackground = PlayerSettings.runInBackground;
            PlayerSettings.runInBackground = false;

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                    extraScriptingDefines = defines
                });
            }
            finally
            {
                PlayerSettings.runInBackground = previousRunInBackground;
            }

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Сборка не удалась: {summary.result}. Ошибок: {summary.totalErrors}, предупреждений: {summary.totalWarnings}");
            }

            Debug.Log($"[BuildTool] Плеер собран за {summary.totalTime}");

            var freed = RemoveDontShipFolders(buildFolder);
            Debug.Log($"[BuildTool] Служебные папки удалены, освобождено {FormatSize(freed)}");

            AssertPathsFitWindowsLimit(buildFolder);

            var shippedSize = DirectorySize(buildFolder);
            Debug.Log($"[BuildTool] Папка раздачи: {buildFolder} ({FormatSize(shippedSize)})");

            var releaseName = BuildReleaseName(profile.ReleaseToken);
            var zipPath = CreateZip(buildFolder, releaseName, profile.FlatZip);
            Debug.Log($"[BuildTool] Архив: {zipPath} ({FormatSize(new FileInfo(zipPath).Length)})");

            WriteManifest(profile, defines, releaseName, zipPath, shippedSize);
            Debug.Log("[BuildTool] Готово.");
        }

        private static void RunAndroidBuild(EditionProfile profile, string[] defines, string[] scenes)
        {
            var buildFolder = Path.Combine(OutputRoot, profile.StagingFolder);

            ClearStagingFolder(buildFolder);
            Directory.CreateDirectory(buildFolder);

            var releaseName = BuildReleaseName(profile.ReleaseToken);
            var outputPath = Path.Combine(buildFolder, releaseName + (profile.AppBundle ? ".aab" : ".apk"));

            Debug.Log($"[BuildTool] Издание: {profile.Edition}");
            Debug.Log($"[BuildTool] Target: {EditorUserBuildSettings.activeBuildTarget}, backend: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}, архитектуры: {PlayerSettings.Android.targetArchitectures}");
            Debug.Log($"[BuildTool] Product: {PlayerSettings.productName}, bundle {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}, version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
            Debug.Log($"[BuildTool] Defines: {string.Join(";", defines)}");
            Debug.Log($"[BuildTool] Scenes: {string.Join(", ", scenes)}");

            if (!PlayerSettings.Android.useCustomKeystore)
                Debug.LogWarning("[BuildTool] Custom keystore выключен — артефакт подпишется отладочным ключом Unity. В стор такой не уедет, это сборка для проверки на устройстве.");

            var previousAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = profile.AppBundle;

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                    extraScriptingDefines = defines
                });
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
            }

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Сборка не удалась: {summary.result}. Ошибок: {summary.totalErrors}, предупреждений: {summary.totalWarnings}");
            }

            var artifactSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0L;
            Debug.Log($"[BuildTool] Плеер собран за {summary.totalTime}: {outputPath} ({FormatSize(artifactSize)})");

            WriteManifest(profile, defines, releaseName, outputPath, artifactSize);
            Debug.Log("[BuildTool] Готово.");
        }

        private static string[] ResolveEditionDefines(EditionProfile profile)
        {
            EditionsUpgradeEditor.WriteDefinesToPlayerSettings = false;
            EditionsUpgradeEditor.ApplyEdition(profile.Edition);

            var computed = EditionsUpgradeEditor.ComputedDefines
                .Split(';')
                .Select(define => define.Trim())
                .Where(define => !string.IsNullOrEmpty(define))
                .Distinct()
                .ToArray();

            EditionsUpgradeEditor.WriteDefinesToPlayerSettings = true;

            var stripped = computed.Where(define => DevOnlyDefines.Contains(define)).ToArray();
            var defines = computed.Where(define => !DevOnlyDefines.Contains(define)).ToArray();

            if (stripped.Length > 0)
                Debug.Log($"[BuildTool] Отладочные дефайны сняты со сборки: {string.Join(";", stripped)}");

            foreach (var required in profile.RequiredDefines)
            {
                if (!defines.Contains(required))
                    throw new InvalidOperationException($"Издание {profile.Edition} не выставило дефайн {required}.");
            }

            foreach (var forbidden in profile.ForbiddenDefines)
            {
                if (defines.Contains(forbidden))
                    throw new InvalidOperationException($"Издание {profile.Edition} не должно нести дефайн {forbidden}.");
            }

            return defines;
        }

        private static string[] ResolveScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("В EditorBuildSettings нет включённых сцен.");

            var missing = scenes.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Сцены не найдены: {string.Join(", ", missing)}");

            return scenes;
        }

        private static long RemoveDontShipFolders(string root)
        {
            var freed = 0L;
            foreach (var pattern in DontShipPatterns)
            {
                foreach (var directory in Directory.GetDirectories(root, pattern, SearchOption.AllDirectories))
                {
                    if (!Directory.Exists(directory))
                        continue;

                    freed += DirectorySize(directory);
                    Directory.Delete(directory, true);
                }
            }

            return freed;
        }

        private static string BuildReleaseName(string releaseToken)
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return $"{ResolveArtifactBaseName()}_{releaseToken}_{ResolveLanguageSuffix()}_{date}";
        }

        private static string ResolveArtifactBaseName()
        {
            var baseName = string.IsNullOrWhiteSpace(PlayerSettings.productName) ? "Build" : PlayerSettings.productName;

            foreach (var suffix in new[] { " Collector's Edition", " Collectors Edition" })
            {
                if (baseName.EndsWith(suffix, StringComparison.Ordinal))
                    baseName = baseName.Substring(0, baseName.Length - suffix.Length);
            }

            return baseName.Replace(' ', '_');
        }

        private static string ResolveLanguageSuffix()
        {
            var codes = EditionArtifacts.ResolveShipLocales();
            return codes.Length == 0 ? "en" : string.Join("_", codes);
        }

        private static void ClearStagingFolder(string buildFolder)
        {
            if (!Directory.Exists(buildFolder))
                return;

            var running = System.Diagnostics.Process.GetProcesses()
                .Where(process =>
                {
                    try
                    {
                        return process.MainModule != null &&
                               process.MainModule.FileName.StartsWith(Path.GetFullPath(buildFolder),
                                   StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                })
                .Select(process => process.ProcessName)
                .Distinct()
                .ToArray();

            if (running.Length > 0)
            {
                throw new InvalidOperationException(
                    $"В папке сборки запущены процессы: {string.Join(", ", running)}. " +
                    "Закрой игру перед сборкой — иначе её файлы будут удалены прямо под ней.");
            }

            try
            {
                Directory.Delete(buildFolder, true);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Не удалось очистить {buildFolder}: {exception.Message}. " +
                    "Скорее всего файлы заняты — закрой игру и повтори.", exception);
            }
        }

        private static void AssertPathsFitWindowsLimit(string buildFolder)
        {
            var tooLong = Directory.GetFiles(buildFolder, "*", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(path => path.Length > MaxWindowsPathLength)
                .ToArray();

            if (tooLong.Length == 0)
                return;

            var sample = string.Join(Environment.NewLine, tooLong.Take(5));
            throw new InvalidOperationException(
                $"Путей длиннее {MaxWindowsPathLength} символов: {tooLong.Length}. Windows их не откроет " +
                $"ни при архивации, ни у игрока. Перенеси проект ближе к корню диска.{Environment.NewLine}{sample}");
        }

        private static string CreateZip(string releaseFolder, string releaseName, bool flat)
        {
            var parent = Path.GetDirectoryName(releaseFolder);
            var zipPath = string.IsNullOrEmpty(parent)
                ? releaseName + ".zip"
                : Path.Combine(parent, releaseName + ".zip");

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var entryPrefix = flat ? string.Empty : releaseName + "/";

            using (var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(releaseFolder, "*", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(releaseFolder.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/');

                    var entry = archive.CreateEntry(entryPrefix + relative, System.IO.Compression.CompressionLevel.Optimal);
                    using (var entryStream = entry.Open())
                    using (var fileStream = File.OpenRead(file))
                        fileStream.CopyTo(entryStream);
                }
            }

            return zipPath;
        }

        private static void WriteManifest(EditionProfile profile, string[] defines, string releaseName, string zipPath, long shippedSize)
        {
            var architecture = profile.Target == BuildTarget.Android
                ? PlayerSettings.Android.targetArchitectures.ToString()
                : "x86";

            var lines = new List<string>
            {
                "{",
                "  \"schema\": 1,",
                $"  \"edition\": \"{profile.Edition}\",",
                $"  \"target\": \"{profile.Target}\",",
                $"  \"architecture\": \"{Escape(architecture)}\",",
                $"  \"productName\": \"{Escape(PlayerSettings.productName)}\",",
                $"  \"bundleId\": \"{Escape(PlayerSettings.GetApplicationIdentifier(EditionArtifacts.ToNamedBuildTarget(profile.Target)))}\",",
                $"  \"version\": \"{Escape(PlayerSettings.bundleVersion)}\",",
                $"  \"releaseName\": \"{Escape(releaseName)}\",",
                $"  \"defines\": \"{Escape(string.Join(";", defines))}\",",
                $"  \"cheats\": {(defines.Contains("CREOBIT") ? "true" : "false")},",
                $"  \"content\": \"{ResolveContentCut()}\",",
                $"  \"scenes\": {EditorBuildSettings.scenes.Count(scene => scene.enabled)},",
                $"  \"shippedBytes\": {shippedSize},",
                $"  \"artifact\": \"{Escape(Path.GetFileName(zipPath))}\",",
                $"  \"unity\": \"{Escape(Application.unityVersion)}\",",
                $"  \"builtAt\": \"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\"",
                "}"
            };

            var manifestPath = Path.Combine(OutputRoot, "build-manifest.json");
            File.WriteAllLines(manifestPath, lines);
            Debug.Log($"[BuildTool] Паспорт: {manifestPath}");
        }

        private static string ResolveContentCut()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var group = settings == null ? null : settings.FindGroup(DemoContentTool.DemoGroupName);

            return group != null && DemoContentTool.IsExcluded(group) ? "demo (packs 2-5 excluded)" : "full";
        }

        private static long DirectorySize(string path)
        {
            return Directory.Exists(path)
                ? Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
                : 0L;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
                return $"{bytes / (1024d * 1024d * 1024d):0.00} ГБ";

            return $"{bytes / (1024d * 1024d):0.0} МБ";
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Build";

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidChar, '_');

            return value.Trim();
        }
    }
}
