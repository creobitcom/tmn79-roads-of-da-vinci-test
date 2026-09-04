using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Creobit.LA8.Build
{
    public class EditionLogoCleanup : IPostprocessBuildWithReport
    {
        private const string GameStreamingFolder = "RoadsOfDaVinci";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var streamingAssets = ResolveStreamingAssets(report.summary.platform, report.summary.outputPath);
            if (string.IsNullOrEmpty(streamingAssets))
            {
                return;
            }

            var folder = Path.Combine(streamingAssets, GameStreamingFolder);
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning($"[EditionLogoCleanup] Папка логотипов не найдена: {folder}");
                return;
            }

            var namedTarget = EditionArtifacts.ToNamedBuildTarget(report.summary.platform);
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            var dropSuffix = EditionArtifacts.UsesCollectorsLogo(defines) ? "se" : "ce";

            var removed = 0;
            foreach (var file in Directory.GetFiles(folder, $"Logo_*_{dropSuffix}.png", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
                removed++;
            }

            Debug.Log($"[EditionLogoCleanup] Логотипы издания '{dropSuffix}' удалены из билда: {removed}");
        }

        private static string ResolveStreamingAssets(BuildTarget target, string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                return null;
            }

            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    var folder = Path.GetDirectoryName(outputPath);
                    if (string.IsNullOrEmpty(folder))
                    {
                        return null;
                    }

                    return Path.Combine(folder, Path.GetFileNameWithoutExtension(outputPath) + "_Data", "StreamingAssets");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(outputPath, "Contents", "Resources", "Data", "StreamingAssets");
                default:
                    return null;
            }
        }
    }
}
