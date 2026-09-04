using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Creobit.LA8.EditorTools.Localization
{
    internal enum La8LocalizationIssueKind
    {
        MissingKey,
        PartialKey,
        EmptyTranslation,
        SuspectKey,
        EmptyField,
        DuplicateKey
    }

    internal sealed class La8LocalizationUsage
    {
        public string AssetPath;
        public string ObjectPath;
        public string FieldName;
        public string TypeName;

        public string Location => string.IsNullOrEmpty(ObjectPath) ? TypeName : $"{ObjectPath} ({TypeName})";
    }

    internal sealed class La8LocalizationIssue
    {
        public string Key;
        public La8LocalizationIssueKind Kind;
        public readonly List<string> Languages = new List<string>();
        public readonly List<La8LocalizationUsage> Usages = new List<La8LocalizationUsage>();

        public string FirstField => Usages.Count > 0 ? Usages[0].FieldName : string.Empty;
        public string LanguagesText => string.Join(", ", Languages);
    }

    internal sealed class La8LocalizationAuditSettings
    {
        public List<string> Roots = new List<string>();
        public string Game = string.Empty;
        public bool FollowDependencies = true;
        public bool ExactSourcesOnly;
        public bool SkipLegacyFolders;
        public List<string> IgnoredKeys = new List<string>();
    }

    internal sealed class La8LocalizationAuditResult
    {
        public readonly List<La8LocalizationIssue> Issues = new List<La8LocalizationIssue>();
        public readonly List<string> Languages = new List<string>();
        public readonly List<string> SkippedAssets = new List<string>();
        public int ScannedAssets;
        public int TotalKeys;
        public int CheckedValues;
        public bool Cancelled;
        public string Error;

        public int Count(La8LocalizationIssueKind kind) => Issues.Count(i => i.Kind == kind);
    }

    internal sealed class La8LocalizationAuditEngine
    {
        const string GameTextPrefix = "GameText_";
        const string JsonExtension = ".json";
        const int MaxValueLength = 120;

        static readonly Regex ArrayElementRegex = new Regex(@"\.Array\.data\[\d+\]", RegexOptions.Compiled);
        static readonly Regex KeyShapeRegex = new Regex(@"^[A-Za-z0-9_\-\.\[\]]+$", RegexOptions.Compiled);
        static readonly Regex DigitsRegex = new Regex(@"^\d+$", RegexOptions.Compiled);

        static readonly string[] ExactSuffixes =
        {
            "localizationkey", "localizationkeys", "localizekey", "lockey", "textkey", "textkeys"
        };

        static readonly HashSet<string> ExactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "taskname", "levelname", "leveldescription", "locationname",
            "tooltipobjectname", "tooltipobjectnamesuffix", "tooltipobjectdescription",
            "tooltipinputtext", "tooltipoutputtext", "gametextid"
        };

        static readonly string[] SoftTokens =
        {
            "key", "name", "description", "text", "title", "hint", "tooltip",
            "label", "caption", "header", "subtitle", "task", "dialog", "phrase", "message"
        };

        static readonly HashSet<string> NoiseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "membername", "methodname", "targetassemblytypename", "objectargumentassemblytypename",
            "assemblytypename", "layername", "lightname", "familyname", "stylename", "shadername",
            "scenename", "filename", "sortinglayername", "materialname", "animatorname", "prefabname",
            "sceneassetname", "typename", "eventname", "parametername", "boolname", "statename",
            "tagname", "poolname", "spritename", "atlasname", "clipname"
        };

        static readonly string[] NoisePrefixes = { "colorlabel", "vectorlabel", "floatlabel", "keywordlabel" };

        static readonly string[] NoiseSuffixes = { "parts", "filter", "mask", "pattern", "format", "regex" };

        static readonly string[] RequiredSuffixes = { "localizationkey", "localizekey", "lockey", "textkey" };

        static readonly HashSet<string> RequiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "taskname", "levelname"
        };

        static readonly string[] LegacyFolders =
        {
            "Assets/Shared_Common/LA7", "Assets/GnomesGarden10", "Assets/LA7", "Assets/_ProjectTemplate", "Packages"
        };

        public static List<string> FindGames()
        {
            var games = new List<string>();
            var root = Application.streamingAssetsPath;

            if (!Directory.Exists(root))
                return games;

            foreach (var directory in Directory.GetDirectories(root))
            {
                if (Directory.GetFiles(directory, GameTextPrefix + "*" + JsonExtension).Length > 0)
                    games.Add(Path.GetFileName(directory));
            }

            return games;
        }

        public La8LocalizationAuditResult Run(La8LocalizationAuditSettings settings)
        {
            var result = new La8LocalizationAuditResult();

            var perLanguage = new Dictionary<string, Dictionary<string, string>>();
            var duplicates = new Dictionary<string, List<string>>();

            if (!TryLoadLocalization(settings.Game, perLanguage, duplicates, out var loadError))
            {
                result.Error = loadError;
                return result;
            }

            result.Languages.AddRange(perLanguage.Keys.OrderBy(language => language));

            var allKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var language in perLanguage.Values)
                allKeys.UnionWith(language.Keys);

            result.TotalKeys = allKeys.Count;

            var ignored = new HashSet<string>(settings.IgnoredKeys, StringComparer.Ordinal);
            var issues = new Dictionary<string, La8LocalizationIssue>(StringComparer.Ordinal);

            AddDuplicateIssues(duplicates, ignored, issues);

            var assets = CollectAssets(settings, result);

            try
            {
                for (int i = 0; i < assets.Count; i++)
                {
                    var path = assets[i];

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Проверка локализации",
                            $"{i + 1}/{assets.Count}   {path}",
                            (float)i / Mathf.Max(1, assets.Count)))
                    {
                        result.Cancelled = true;
                        break;
                    }

                    ScanAsset(path, settings, perLanguage, allKeys, ignored, issues, result);
                    result.ScannedAssets++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            result.Issues.AddRange(issues.Values
                .OrderBy(issue => (int)issue.Kind)
                .ThenBy(issue => issue.Key, StringComparer.OrdinalIgnoreCase));

            return result;
        }

        bool TryLoadLocalization(
            string game,
            Dictionary<string, Dictionary<string, string>> perLanguage,
            Dictionary<string, List<string>> duplicates,
            out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(game))
            {
                error = "Не выбран проект локализации (папка внутри StreamingAssets).";
                return false;
            }

            var directory = Path.Combine(Application.streamingAssetsPath, game);

            if (!Directory.Exists(directory))
            {
                error = $"Папка локализации не найдена: {directory}";
                return false;
            }

            var files = Directory.GetFiles(directory, GameTextPrefix + "*" + JsonExtension);

            if (files.Length == 0)
            {
                error = $"В {directory} нет файлов {GameTextPrefix}*{JsonExtension}";
                return false;
            }

            foreach (var file in files)
            {
                var language = Path.GetFileNameWithoutExtension(file).Substring(GameTextPrefix.Length);
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                var duplicated = new List<string>();

                try
                {
                    var root = JObject.Parse(File.ReadAllText(file, Encoding.UTF8));

                    if (!(root["Data"] is JObject data))
                    {
                        error = $"В {Path.GetFileName(file)} нет секции Data.";
                        return false;
                    }

                    foreach (var property in data.Properties())
                    {
                        if (map.ContainsKey(property.Name))
                            duplicated.Add(property.Name);

                        map[property.Name] = property.Value?.ToString() ?? string.Empty;
                    }
                }
                catch (Exception exception)
                {
                    error = $"Не разобрал {Path.GetFileName(file)}: {exception.Message}";
                    return false;
                }

                perLanguage[language] = map;

                if (duplicated.Count > 0)
                    duplicates[language] = duplicated;
            }

            return true;
        }

        static void AddDuplicateIssues(
            Dictionary<string, List<string>> duplicates,
            HashSet<string> ignored,
            Dictionary<string, La8LocalizationIssue> issues)
        {
            foreach (var pair in duplicates)
            {
                foreach (var key in pair.Value.Distinct())
                {
                    if (ignored.Contains(key))
                        continue;

                    var id = $"{(int)La8LocalizationIssueKind.DuplicateKey}|{key}";

                    if (!issues.TryGetValue(id, out var issue))
                    {
                        issue = new La8LocalizationIssue { Key = key, Kind = La8LocalizationIssueKind.DuplicateKey };
                        issues[id] = issue;
                    }

                    if (!issue.Languages.Contains(pair.Key))
                        issue.Languages.Add(pair.Key);
                }
            }
        }

        static List<string> CollectAssets(La8LocalizationAuditSettings settings, La8LocalizationAuditResult result)
        {
            var roots = settings.Roots
                .Where(root => !string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root))
                .Distinct()
                .ToArray();

            if (roots.Length == 0)
                return new List<string>();

            var direct = AssetDatabase.FindAssets(string.Empty, roots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                .Distinct()
                .ToArray();

            IEnumerable<string> candidates = direct;

            if (settings.FollowDependencies && direct.Length > 0)
                candidates = direct.Concat(AssetDatabase.GetDependencies(direct, true)).Distinct();

            var scannable = new List<string>();

            foreach (var path in candidates)
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();

                if (extension == ".unity")
                {
                    result.SkippedAssets.Add(path);
                    continue;
                }

                if (extension != ".prefab" && extension != ".asset")
                    continue;

                if (settings.SkipLegacyFolders && IsLegacy(path))
                    continue;

                scannable.Add(path);
            }

            scannable.Sort(StringComparer.OrdinalIgnoreCase);
            return scannable;
        }

        static bool IsLegacy(string path)
        {
            foreach (var folder in LegacyFolders)
            {
                if (path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        void ScanAsset(
            string path,
            La8LocalizationAuditSettings settings,
            Dictionary<string, Dictionary<string, string>> perLanguage,
            HashSet<string> allKeys,
            HashSet<string> ignored,
            Dictionary<string, La8LocalizationIssue> issues,
            La8LocalizationAuditResult result)
        {
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    return;

                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform)
                        continue;

                    ScanObject(component, path, BuildObjectPath(prefab.transform, component.transform),
                        settings, perLanguage, allKeys, ignored, issues, result);
                }

                return;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset == null || asset is GameObject || asset is Transform || asset is MonoScript)
                    continue;

                ScanObject(asset, path, string.Empty, settings, perLanguage, allKeys, ignored, issues, result);
            }
        }

        internal static string BuildObjectPath(Transform root, Transform target)
        {
            if (target == root)
                return target.name;

            var parts = new List<string>();
            var current = target;

            while (current != null)
            {
                parts.Add(current.name);

                if (current == root)
                    break;

                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        void ScanObject(
            UnityEngine.Object target,
            string assetPath,
            string objectPath,
            La8LocalizationAuditSettings settings,
            Dictionary<string, Dictionary<string, string>> perLanguage,
            HashSet<string> allKeys,
            HashSet<string> ignored,
            Dictionary<string, La8LocalizationIssue> issues,
            La8LocalizationAuditResult result)
        {
            var typeName = target.GetType().Name;
            var isResource = InheritsFromResourceBase(target.GetType());

            using (var serialized = new SerializedObject(target))
            {
                var iterator = serialized.GetIterator();
                bool enterChildren = true;

                while (iterator.Next(enterChildren))
                {
                    enterChildren = iterator.propertyType != SerializedPropertyType.String;

                    if (iterator.propertyType != SerializedPropertyType.String)
                        continue;

                    var rawName = LastPathElement(iterator.propertyPath);
                    bool isTmpText = string.Equals(rawName, "m_text", StringComparison.OrdinalIgnoreCase);

                    if (!isTmpText && rawName.StartsWith("m_", StringComparison.Ordinal))
                        continue;

                    var fieldName = NormalizeFieldName(rawName);

                    if (string.IsNullOrEmpty(fieldName))
                        continue;

                    bool exact = isTmpText || IsExactField(fieldName, isResource);
                    bool soft = !exact && IsSoftField(fieldName);

                    if (!exact && (!soft || settings.ExactSourcesOnly))
                        continue;

                    var value = iterator.stringValue;

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (IsRequiredField(fieldName))
                            Add(issues, ignored, string.Empty, La8LocalizationIssueKind.EmptyField, null,
                                assetPath, objectPath, fieldName, typeName);

                        continue;
                    }

                    value = value.Trim();
                    result.CheckedValues++;

                    if (!allKeys.Contains(value) && !LooksLikeKey(value))
                        continue;

                    var missing = new List<string>();
                    var empty = new List<string>();

                    foreach (var pair in perLanguage)
                    {
                        if (!pair.Value.TryGetValue(value, out var translated))
                            missing.Add(pair.Key);
                        else if (string.IsNullOrWhiteSpace(translated))
                            empty.Add(pair.Key);
                    }

                    if (missing.Count == perLanguage.Count)
                    {
                        Add(issues, ignored, value,
                            exact ? La8LocalizationIssueKind.MissingKey : La8LocalizationIssueKind.SuspectKey,
                            null, assetPath, objectPath, fieldName, typeName);

                        continue;
                    }

                    if (missing.Count > 0)
                    {
                        Add(issues, ignored, value, La8LocalizationIssueKind.PartialKey, missing,
                            assetPath, objectPath, fieldName, typeName);

                        continue;
                    }

                    if (empty.Count > 0)
                        Add(issues, ignored, value, La8LocalizationIssueKind.EmptyTranslation, empty,
                            assetPath, objectPath, fieldName, typeName);
                }
            }
        }

        static void Add(
            Dictionary<string, La8LocalizationIssue> issues,
            HashSet<string> ignored,
            string key,
            La8LocalizationIssueKind kind,
            List<string> languages,
            string assetPath,
            string objectPath,
            string fieldName,
            string typeName)
        {
            if (!string.IsNullOrEmpty(key) && ignored.Contains(key))
                return;

            var id = kind == La8LocalizationIssueKind.EmptyField
                ? $"{(int)kind}|{assetPath}|{objectPath}|{fieldName}"
                : $"{(int)kind}|{key}";

            if (!issues.TryGetValue(id, out var issue))
            {
                issue = new La8LocalizationIssue { Key = key, Kind = kind };
                issues[id] = issue;
            }

            if (languages != null)
            {
                foreach (var language in languages)
                {
                    if (!issue.Languages.Contains(language))
                        issue.Languages.Add(language);
                }
            }

            bool known = issue.Usages.Any(usage =>
                usage.AssetPath == assetPath && usage.ObjectPath == objectPath && usage.FieldName == fieldName);

            if (!known)
            {
                issue.Usages.Add(new La8LocalizationUsage
                {
                    AssetPath = assetPath,
                    ObjectPath = objectPath,
                    FieldName = fieldName,
                    TypeName = typeName
                });
            }
        }

        static bool InheritsFromResourceBase(Type type)
        {
            while (type != null)
            {
                if (type.Name == "ResourceBaseSO")
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        static string LastPathElement(string propertyPath)
        {
            var cleaned = ArrayElementRegex.Replace(propertyPath, string.Empty);
            var index = cleaned.LastIndexOf('.');
            return index < 0 ? cleaned : cleaned.Substring(index + 1);
        }

        static string NormalizeFieldName(string rawName)
        {
            var name = rawName;

            if (name.StartsWith("<", StringComparison.Ordinal))
            {
                var end = name.IndexOf('>');

                if (end > 1)
                    name = name.Substring(1, end - 1);
            }

            return name.Replace("k__BackingField", string.Empty).TrimStart('_');
        }

        static bool IsExactField(string fieldName, bool isResource)
        {
            if (ExactNames.Contains(fieldName))
                return true;

            var lower = fieldName.ToLowerInvariant();

            foreach (var suffix in ExactSuffixes)
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal))
                    return true;
            }

            return isResource && (lower == "name" || lower == "description");
        }

        static bool IsRequiredField(string fieldName)
        {
            if (RequiredNames.Contains(fieldName))
                return true;

            var lower = fieldName.ToLowerInvariant();

            foreach (var suffix in RequiredSuffixes)
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static bool IsSoftField(string fieldName)
        {
            var lower = fieldName.ToLowerInvariant();

            if (NoiseNames.Contains(lower))
                return false;

            foreach (var prefix in NoisePrefixes)
            {
                if (lower.StartsWith(prefix, StringComparison.Ordinal))
                    return false;
            }

            foreach (var suffix in NoiseSuffixes)
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal))
                    return false;
            }

            foreach (var token in SoftTokens)
            {
                if (lower.Contains(token))
                    return true;
            }

            return false;
        }

        static bool LooksLikeKey(string value)
        {
            if (value.Length < 3 || value.Length > MaxValueLength)
                return false;

            return !DigitsRegex.IsMatch(value) && KeyShapeRegex.IsMatch(value);
        }
    }
}
