using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Creobit.LA8.Build
{
    public static class EditionArtifacts
    {
        private const string GameStreamingFolder = "Assets/StreamingAssets/RoadsOfDaVinci";
        private const string LogoJsonPath = GameStreamingFolder + "/logo.json";
        private const string IconsFolder = "Assets/AppIcons";
        private const int MinIconSize = 1024;

        public static void ApplyForTarget(NamedBuildTarget namedTarget)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            var collectors = UsesCollectorsIdentity(defines, namedTarget);
            Apply(namedTarget, collectors, collectors);
        }

        public static void Apply(bool collectorsLogo, bool collectorsIcon)
        {
            Apply(ToNamedBuildTarget(EditorUserBuildSettings.activeBuildTarget), collectorsLogo, collectorsIcon);
        }

        public static void Apply(NamedBuildTarget namedTarget, bool collectorsLogo, bool collectorsIcon)
        {
            ApplyLogo(collectorsLogo);
            ApplyIcon(namedTarget, collectorsIcon);
        }

        public static bool UsesCollectorsIdentity(string defines, NamedBuildTarget namedTarget)
        {
            return HasDefine(defines, "COLLECTOR")
                   && HasDefine(defines, "PREMIUM")
                   && !HasDefine(defines, "MAC_APPSTORE")
                   && namedTarget != NamedBuildTarget.iOS;
        }

        public static bool UsesCollectorsLogo(string defines)
        {
            return UsesCollectorsIdentity(defines, ToNamedBuildTarget(EditorUserBuildSettings.activeBuildTarget));
        }

        public static bool UsesCollectorsIcon(string defines)
        {
            return UsesCollectorsLogo(defines);
        }

        public static NamedBuildTarget ToNamedBuildTarget(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return NamedBuildTarget.Android;
                case BuildTarget.iOS:
                    return NamedBuildTarget.iOS;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                    return NamedBuildTarget.Standalone;
                default:
                    return NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            }
        }

        private static bool HasDefine(string defines, string define)
        {
            return !string.IsNullOrEmpty(defines)
                   && defines.Split(';').Any(entry => string.Equals(entry.Trim(), define, StringComparison.Ordinal));
        }

        private static void ApplyLogo(bool isCollector)
        {
            if (!File.Exists(LogoJsonPath))
                throw new FileNotFoundException($"Не найден {LogoJsonPath} — логотип главного меню не определится.", LogoJsonPath);

            var edition = isCollector ? "ce" : "se";
            var json = File.ReadAllText(LogoJsonPath);
            var patched = Regex.Replace(json, "(\"currentEdition\"\\s*:\\s*\")[^\"]*(\")", "${1}" + edition + "$2");

            if (!patched.Contains($"\"currentEdition\": \"{edition}\"") && !patched.Contains($"\"currentEdition\":\"{edition}\""))
                throw new InvalidOperationException($"Не удалось выставить currentEdition={edition} в {LogoJsonPath}. Содержимое: {json}");

            var missing = ResolveShipLocales()
                .Select(locale => $"{GameStreamingFolder}/Logo_{locale}_{edition}.png")
                .Where(path => !File.Exists(path))
                .ToArray();

            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Для издания '{edition}' нет логотипов: {string.Join(", ", missing)}. " +
                    "Главное меню покажет пустое место на этих языках.");
            }

            if (patched != json)
            {
                File.WriteAllText(LogoJsonPath, patched);
                AssetDatabase.ImportAsset(LogoJsonPath);
            }

            Debug.Log($"[EditionArtifacts] logo.json: currentEdition={edition}");
        }

        private static void ApplyIcon(NamedBuildTarget namedTarget, bool isCollector)
        {
            var edition = isCollector ? "ce" : "se";
            var iconPath = $"{IconsFolder}/icon_{edition}.png";
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);

            if (icon == null)
            {
                throw new FileNotFoundException(
                    $"Не найдена иконка издания: {iconPath}. Положи квадратный PNG от {MinIconSize}x{MinIconSize} в {IconsFolder}.",
                    iconPath);
            }

            if (icon.width != icon.height || icon.width < MinIconSize)
            {
                throw new InvalidOperationException(
                    $"Иконка {iconPath} должна быть квадратной и не меньше {MinIconSize}x{MinIconSize}, сейчас {icon.width}x{icon.height}.");
            }

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            ApplyPlatformIcons(namedTarget, icon);
            Debug.Log($"[EditionArtifacts] Иконка издания: {iconPath}");
        }

        private static void ApplyPlatformIcons(NamedBuildTarget namedTarget, Texture2D icon)
        {
            if (namedTarget != NamedBuildTarget.iOS)
                return;

            var kinds = PlayerSettings.GetSupportedIconKinds(namedTarget);

            if (kinds == null || kinds.Length == 0)
                return;

            var assigned = 0;

            foreach (var kind in kinds)
            {
                var icons = PlayerSettings.GetPlatformIcons(namedTarget, kind);

                if (icons == null || icons.Length == 0)
                    continue;

                foreach (var platformIcon in icons)
                {
                    platformIcon.SetTexture(icon, 0);
                    assigned++;
                }

                PlayerSettings.SetPlatformIcons(namedTarget, kind, icons);
            }

            Debug.Log($"[EditionArtifacts] Иконки {namedTarget.TargetName}: заполнено слотов {assigned}");
        }

        public static string[] ResolveShipLocales()
        {
            if (!Directory.Exists(GameStreamingFolder))
                return new[] { "en" };

            return Directory.GetFiles(GameStreamingFolder, "GameText_*.json")
                .Select(path => Regex.Match(Path.GetFileNameWithoutExtension(path), @"^GameText_(.+)$"))
                .Where(match => match.Success)
                .Select(match => ToShortLocale(match.Groups[1].Value))
                .Distinct()
                .OrderBy(code => code == "en" ? 0 : 1)
                .ThenBy(code => code, StringComparer.Ordinal)
                .ToArray();
        }

        private static string ToShortLocale(string language)
        {
            if (string.Equals(language, "pt-BR", StringComparison.OrdinalIgnoreCase))
                return "br";

            var dashIndex = language.IndexOf('-');
            var code = dashIndex > 0 ? language.Substring(0, dashIndex) : language;
            return code.ToLowerInvariant();
        }
    }

    public class EditionArtifactsBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            EditionArtifacts.ApplyForTarget(EditionArtifacts.ToNamedBuildTarget(report.summary.platform));
        }
    }
}
