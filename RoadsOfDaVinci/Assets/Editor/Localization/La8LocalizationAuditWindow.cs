using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Creobit.LA8.EditorTools.Localization
{
    public class La8LocalizationAuditWindow : EditorWindow
    {
        const string PrefsRoots = "Creobit.LA8.LocalizationAudit.Roots";
        const string PrefsGame = "Creobit.LA8.LocalizationAudit.Game";
        const string PrefsFollowDependencies = "Creobit.LA8.LocalizationAudit.FollowDependencies";
        const string PrefsExactOnly = "Creobit.LA8.LocalizationAudit.ExactOnly";
        const string PrefsSkipLegacy = "Creobit.LA8.LocalizationAudit.SkipLegacy";
        const string PrefsIgnored = "Creobit.LA8.LocalizationAudit.Ignored";

        const string DefaultRoot = "Assets/LA8_slv/Levels";

        readonly La8LocalizationAuditEngine _engine = new La8LocalizationAuditEngine();
        readonly La8LocalizationAuditSettings _settings = new La8LocalizationAuditSettings();
        readonly HashSet<string> _expanded = new HashSet<string>();
        readonly HashSet<La8LocalizationIssueKind> _visibleKinds = new HashSet<La8LocalizationIssueKind>();

        List<string> _games = new List<string>();
        La8LocalizationAuditResult _result;
        List<La8LocalizationIssue> _filtered = new List<La8LocalizationIssue>();
        string _search = string.Empty;
        Vector2 _scroll;
        bool _optionsExpanded = true;
        bool _filterDirty = true;
        GUIStyle _tagStyle;
        GUIStyle _keyStyle;
        GUIStyle _pathStyle;

        [MenuItem("Tools/LA8/Проверка локализации уровней")]
        public static void Open()
        {
            var window = GetWindow<La8LocalizationAuditWindow>("Локализация");
            window.minSize = new Vector2(820, 520);
            window.Show();
        }

        void OnEnable()
        {
            _games = La8LocalizationAuditEngine.FindGames();

            _settings.Roots = Split(EditorPrefs.GetString(PrefsRoots, DefaultRoot));
            _settings.Game = EditorPrefs.GetString(PrefsGame, _games.FirstOrDefault() ?? string.Empty);
            _settings.FollowDependencies = EditorPrefs.GetBool(PrefsFollowDependencies, true);
            _settings.ExactSourcesOnly = EditorPrefs.GetBool(PrefsExactOnly, false);
            _settings.SkipLegacyFolders = EditorPrefs.GetBool(PrefsSkipLegacy, false);
            _settings.IgnoredKeys = Split(EditorPrefs.GetString(PrefsIgnored, string.Empty));

            if (_settings.Roots.Count == 0)
                _settings.Roots.Add(DefaultRoot);

            if (!_games.Contains(_settings.Game))
                _settings.Game = _games.FirstOrDefault() ?? string.Empty;

            foreach (La8LocalizationIssueKind kind in Enum.GetValues(typeof(La8LocalizationIssueKind)))
                _visibleKinds.Add(kind);
        }

        void SaveSettings()
        {
            EditorPrefs.SetString(PrefsRoots, Join(_settings.Roots));
            EditorPrefs.SetString(PrefsGame, _settings.Game);
            EditorPrefs.SetBool(PrefsFollowDependencies, _settings.FollowDependencies);
            EditorPrefs.SetBool(PrefsExactOnly, _settings.ExactSourcesOnly);
            EditorPrefs.SetBool(PrefsSkipLegacy, _settings.SkipLegacyFolders);
            EditorPrefs.SetString(PrefsIgnored, Join(_settings.IgnoredKeys));
        }

        static List<string> Split(string value) => string.IsNullOrEmpty(value)
            ? new List<string>()
            : value.Split(';').Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()).ToList();

        static string Join(List<string> values) => string.Join(";", values);

        void EnsureStyles()
        {
            if (_tagStyle != null)
                return;

            _tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _keyStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };

            _pathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = false
            };
        }

        void OnGUI()
        {
            EnsureStyles();

            DrawOptions();
            DrawRunBar();

            if (_result == null)
            {
                EditorGUILayout.HelpBox(
                    "Укажи папку уровней и нажми «Проверить».\n" +
                    "Тулза соберёт все ассеты папки, пройдёт по их зависимостям (префабы, SO, тултипы, ресурсы) " +
                    "и сверит каждый ключ с GameText_*.json из StreamingAssets.",
                    MessageType.Info);

                return;
            }

            if (!string.IsNullOrEmpty(_result.Error))
            {
                EditorGUILayout.HelpBox(_result.Error, MessageType.Error);
                return;
            }

            DrawSummary();
            DrawFilters();
            DrawResults();
            DrawFooter();
        }

        void DrawOptions()
        {
            _optionsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_optionsExpanded, "Что проверяем");

            if (_optionsExpanded)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawGameSelector();
                DrawRoots();

                _settings.FollowDependencies = EditorGUILayout.ToggleLeft(
                    "Идти по зависимостям (префабы уровня, тултипы, ресурсы вне папки)",
                    _settings.FollowDependencies);

                _settings.ExactSourcesOnly = EditorGUILayout.ToggleLeft(
                    "Только точные источники ключей (без эвристики)",
                    _settings.ExactSourcesOnly);

                _settings.SkipLegacyFolders = EditorGUILayout.ToggleLeft(
                    "Пропускать чужое (LA7 / GnomesGarden10 / _ProjectTemplate / Packages)",
                    _settings.SkipLegacyFolders);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawGameSelector()
        {
            if (_games.Count == 0)
            {
                EditorGUILayout.HelpBox("В StreamingAssets не найдено ни одной папки с GameText_*.json", MessageType.Error);
                return;
            }

            int index = Mathf.Max(0, _games.IndexOf(_settings.Game));
            int picked = EditorGUILayout.Popup("Локализация", index, _games.ToArray());
            _settings.Game = _games[picked];
        }

        void DrawRoots()
        {
            EditorGUILayout.LabelField("Папки", EditorStyles.boldLabel);

            for (int i = 0; i < _settings.Roots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_settings.Roots[i]);
                var picked = EditorGUILayout.ObjectField(folder, typeof(DefaultAsset), false);

                if (picked != folder)
                {
                    var path = AssetDatabase.GetAssetPath(picked);

                    if (AssetDatabase.IsValidFolder(path))
                        _settings.Roots[i] = path;
                }

                EditorGUILayout.LabelField(_settings.Roots[i], _pathStyle);

                if (GUILayout.Button("−", GUILayout.Width(24)) && _settings.Roots.Count > 1)
                {
                    _settings.Roots.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Добавить папку", GUILayout.Width(140)))
            {
                var path = EditorUtility.OpenFolderPanel("Папка уровней", Application.dataPath, string.Empty);
                var relative = ToAssetPath(path);

                if (!string.IsNullOrEmpty(relative) && !_settings.Roots.Contains(relative))
                    _settings.Roots.Add(relative);
            }

            if (GUILayout.Button("Сбросить на " + DefaultRoot, GUILayout.Width(220)))
            {
                _settings.Roots.Clear();
                _settings.Roots.Add(DefaultRoot);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawDropArea();
        }

        void DrawDropArea()
        {
            var rect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Перетащи сюда папку из Project", EditorStyles.helpBox);

            var current = Event.current;

            if (!rect.Contains(current.mousePosition))
                return;

            if (current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                current.Use();
                return;
            }

            if (current.type != EventType.DragPerform)
                return;

            DragAndDrop.AcceptDrag();

            foreach (var dragged in DragAndDrop.objectReferences)
            {
                var path = AssetDatabase.GetAssetPath(dragged);

                if (AssetDatabase.IsValidFolder(path) && !_settings.Roots.Contains(path))
                    _settings.Roots.Add(path);
            }

            current.Use();
        }

        static string ToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return string.Empty;

            absolutePath = absolutePath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');

            return absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + absolutePath.Substring(dataPath.Length)
                : string.Empty;
        }

        void DrawRunBar()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_games.Count == 0))
            {
                if (GUILayout.Button("Проверить", GUILayout.Height(28)))
                    Scan();
            }

            using (new EditorGUI.DisabledScope(_settings.IgnoredKeys.Count == 0))
            {
                if (GUILayout.Button($"Очистить игнор ({_settings.IgnoredKeys.Count})", GUILayout.Height(28), GUILayout.Width(180)))
                {
                    _settings.IgnoredKeys.Clear();
                    SaveSettings();
                    Scan();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        void Scan()
        {
            SaveSettings();
            _result = _engine.Run(_settings);
            _expanded.Clear();
            _filterDirty = true;
        }

        void DrawSummary()
        {
            var summary = new StringBuilder();
            summary.Append($"Ассетов просканировано: {_result.ScannedAssets}");
            summary.Append($"   |   строк проверено: {_result.CheckedValues}");
            summary.Append($"   |   языков: {_result.Languages.Count}");
            summary.Append($"   |   ключей в json: {_result.TotalKeys}");

            if (_result.Cancelled)
                summary.Append("   |   ПРЕРВАНО");

            EditorGUILayout.LabelField(summary.ToString(), EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            DrawCounter("Нет ключа", La8LocalizationIssueKind.MissingKey);
            DrawCounter("Не во всех языках", La8LocalizationIssueKind.PartialKey);
            DrawCounter("Пустой перевод", La8LocalizationIssueKind.EmptyTranslation);
            DrawCounter("Дубль в json", La8LocalizationIssueKind.DuplicateKey);
            DrawCounter("Подозрение", La8LocalizationIssueKind.SuspectKey);
            DrawCounter("Пустое поле", La8LocalizationIssueKind.EmptyField);
            EditorGUILayout.EndHorizontal();

            if (_result.SkippedAssets.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Сцены не сканируются, пропущено: {_result.SkippedAssets.Count}. " +
                    "Тексты в сценах проверь отдельно.",
                    MessageType.None);
            }
        }

        void DrawCounter(string label, La8LocalizationIssueKind kind)
        {
            int count = _result.Count(kind);
            var previous = GUI.backgroundColor;

            if (count > 0)
                GUI.backgroundColor = ColorFor(kind);

            bool visible = _visibleKinds.Contains(kind);
            bool toggled = GUILayout.Toggle(visible, $"{label}: {count}", EditorStyles.miniButton);

            GUI.backgroundColor = previous;

            if (toggled == visible)
                return;

            if (toggled)
                _visibleKinds.Add(kind);
            else
                _visibleKinds.Remove(kind);

            _filterDirty = true;
        }

        void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();

            var search = EditorGUILayout.TextField("Поиск", _search);

            if (search != _search)
            {
                _search = search;
                _filterDirty = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        void ApplyFilter()
        {
            if (!_filterDirty)
                return;

            _filterDirty = false;

            IEnumerable<La8LocalizationIssue> query = _result.Issues.Where(issue => _visibleKinds.Contains(issue.Kind));

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var needle = _search.Trim();

                query = query.Where(issue =>
                    issue.Key.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    issue.Usages.Any(usage =>
                        usage.AssetPath.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        usage.FieldName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            _filtered = query.ToList();
        }

        void DrawResults()
        {
            ApplyFilter();

            if (_filtered.Count == 0)
            {
                EditorGUILayout.HelpBox("Ничего не найдено — по выбранным фильтрам всё локализовано.", MessageType.Info);
                GUILayout.FlexibleSpace();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var issue in _filtered)
                DrawIssue(issue);

            EditorGUILayout.EndScrollView();
        }

        void DrawIssue(La8LocalizationIssue issue)
        {
            var id = $"{(int)issue.Kind}|{issue.Key}|{issue.Usages.FirstOrDefault()?.AssetPath}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var previous = GUI.backgroundColor;
            GUI.backgroundColor = ColorFor(issue.Kind);
            GUILayout.Box(TagFor(issue.Kind), _tagStyle, GUILayout.Width(150), GUILayout.Height(18));
            GUI.backgroundColor = previous;

            bool expanded = _expanded.Contains(id);
            var title = issue.Kind == La8LocalizationIssueKind.EmptyField ? "(поле пустое)" : issue.Key;

            if (GUILayout.Button(title, _keyStyle, GUILayout.MinWidth(220)))
            {
                if (expanded)
                    _expanded.Remove(id);
                else
                    _expanded.Add(id);
            }

            GUILayout.Label(issue.FirstField, EditorStyles.miniLabel, GUILayout.Width(190));
            GUILayout.Label($"×{issue.Usages.Count}", EditorStyles.miniLabel, GUILayout.Width(34));

            if (issue.Languages.Count > 0)
                GUILayout.Label(issue.LanguagesText, EditorStyles.miniLabel, GUILayout.Width(190));

            GUILayout.FlexibleSpace();

            var firstUsage = issue.Usages.FirstOrDefault();

            using (new EditorGUI.DisabledScope(firstUsage == null))
            {
                if (GUILayout.Button(new GUIContent("Перейти", "Открыть префаб и выделить объект с этим ключом"),
                        EditorStyles.miniButton, GUILayout.Width(64)))
                    GoTo(firstUsage);
            }

            if (!string.IsNullOrEmpty(issue.Key) && GUILayout.Button("Ключ", EditorStyles.miniButton, GUILayout.Width(50)))
                EditorGUIUtility.systemCopyBuffer = issue.Key;

            if (!string.IsNullOrEmpty(issue.Key) && GUILayout.Button("Игнор", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                if (!_settings.IgnoredKeys.Contains(issue.Key))
                    _settings.IgnoredKeys.Add(issue.Key);

                SaveSettings();
                _result.Issues.RemoveAll(other => other.Key == issue.Key);
                _filterDirty = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            if (_expanded.Contains(id))
            {
                EditorGUI.indentLevel++;

                foreach (var usage in issue.Usages)
                {
                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button(usage.AssetPath, _pathStyle))
                        Ping(usage);

                    GUILayout.Label(usage.Location, EditorStyles.miniLabel, GUILayout.Width(260));
                    GUILayout.Label(usage.FieldName, EditorStyles.miniLabel, GUILayout.Width(170));

                    if (GUILayout.Button(new GUIContent("Перейти", "Открыть префаб и выделить объект"),
                            EditorStyles.miniButton, GUILayout.Width(64)))
                        GoTo(usage);

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        static void Ping(La8LocalizationUsage usage)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(usage.AssetPath);

            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        static void GoTo(La8LocalizationUsage usage)
        {
            if (usage == null)
                return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(usage.AssetPath);

            if (asset == null)
                return;

            if (asset is GameObject && !string.IsNullOrEmpty(usage.ObjectPath))
            {
                var stage = PrefabStageUtility.OpenPrefab(usage.AssetPath);
                var target = stage == null ? null : FindByObjectPath(stage.prefabContentsRoot, usage.ObjectPath);

                if (target != null)
                {
                    Selection.activeGameObject = target;
                    EditorGUIUtility.PingObject(target);
                    SceneView.FrameLastActiveSceneView();
                    return;
                }
            }

            Ping(usage);
        }

        static GameObject FindByObjectPath(GameObject root, string objectPath)
        {
            if (root == null)
                return null;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (La8LocalizationAuditEngine.BuildObjectPath(root.transform, transform) == objectPath)
                    return transform.gameObject;
            }

            var tail = objectPath.Substring(objectPath.IndexOf('/') + 1);
            var found = root.transform.Find(tail);

            return found == null ? root : found.gameObject;
        }

        void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button($"Скопировать ключи ({_filtered.Count})"))
            {
                var keys = _filtered
                    .Where(issue => !string.IsNullOrEmpty(issue.Key))
                    .Select(issue => issue.Key)
                    .Distinct();

                EditorGUIUtility.systemCopyBuffer = string.Join(Environment.NewLine, keys);
            }

            if (GUILayout.Button("Выгрузить CSV"))
                ExportCsv();

            EditorGUILayout.EndHorizontal();
        }

        void ExportCsv()
        {
            var path = EditorUtility.SaveFilePanel("Отчёт по локализации", string.Empty, "localization_audit.csv", "csv");

            if (string.IsNullOrEmpty(path))
                return;

            var builder = new StringBuilder();
            builder.AppendLine("key;problem;field;type;object;asset;languages");

            foreach (var issue in _filtered)
            {
                foreach (var usage in issue.Usages)
                {
                    builder.Append(Escape(issue.Key)).Append(';');
                    builder.Append(Escape(TagFor(issue.Kind))).Append(';');
                    builder.Append(Escape(usage.FieldName)).Append(';');
                    builder.Append(Escape(usage.TypeName)).Append(';');
                    builder.Append(Escape(usage.ObjectPath)).Append(';');
                    builder.Append(Escape(usage.AssetPath)).Append(';');
                    builder.AppendLine(Escape(issue.LanguagesText));
                }
            }

            System.IO.File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
            EditorUtility.RevealInFinder(path);
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Contains(";") || value.Contains("\"")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }

        static string TagFor(La8LocalizationIssueKind kind)
        {
            switch (kind)
            {
                case La8LocalizationIssueKind.MissingKey: return "НЕТ КЛЮЧА";
                case La8LocalizationIssueKind.PartialKey: return "НЕ ВО ВСЕХ ЯЗЫКАХ";
                case La8LocalizationIssueKind.EmptyTranslation: return "ПУСТОЙ ПЕРЕВОД";
                case La8LocalizationIssueKind.DuplicateKey: return "ДУБЛЬ В JSON";
                case La8LocalizationIssueKind.SuspectKey: return "ПОДОЗРЕНИЕ";
                default: return "ПУСТОЕ ПОЛЕ";
            }
        }

        static Color ColorFor(La8LocalizationIssueKind kind)
        {
            switch (kind)
            {
                case La8LocalizationIssueKind.MissingKey: return new Color(1f, 0.42f, 0.38f);
                case La8LocalizationIssueKind.PartialKey: return new Color(1f, 0.68f, 0.3f);
                case La8LocalizationIssueKind.EmptyTranslation: return new Color(1f, 0.78f, 0.35f);
                case La8LocalizationIssueKind.DuplicateKey: return new Color(0.98f, 0.6f, 0.85f);
                case La8LocalizationIssueKind.SuspectKey: return new Color(0.95f, 0.9f, 0.45f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }
    }
}
