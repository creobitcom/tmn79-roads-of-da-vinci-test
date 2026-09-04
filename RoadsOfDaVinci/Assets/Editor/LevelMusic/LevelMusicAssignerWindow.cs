using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Creobit.LA8.EditorTools
{
    /// <summary>
    /// Простановка музыки во все SO уровней внутри папки (пака).
    /// Указываешь папку пака и клип — тулза находит все ассеты уровней (у которых есть поле _music)
    /// и подменяет клип разом. Ничего кроме поля _music не трогает.
    /// </summary>
    public class LevelMusicAssignerWindow : EditorWindow
    {
        const string MusicField = "_music";
        const string LevelNumberField = "_levelNumber";
        const string LevelNameField = "_levelName";

        const string PrefsFolder = "Creobit.LA8.LevelMusic.Folder";
        const string PrefsClip = "Creobit.LA8.LevelMusic.Clip";

        [MenuItem("Tools/LA8/Музыка уровней (простановка по паку)")]
        public static void Open()
        {
            var w = GetWindow<LevelMusicAssignerWindow>("Музыка уровней");
            w.minSize = new Vector2(700, 420);
            w.Show();
        }

        class Row
        {
            public ScriptableObject Asset;
            public string Path;
            public string Label;
            public AudioClip Current;
            public bool Selected = true;
        }

        DefaultAsset _folder;
        AudioClip _clip;
        readonly List<Row> _rows = new List<Row>();
        bool _scanned;
        Vector2 _scroll;
        string _status = "";

        GUIStyle _sHeader, _sSub, _sMono;
        bool _stylesReady;

        void OnEnable()
        {
            var folderPath = EditorPrefs.GetString(PrefsFolder, "");
            if (!string.IsNullOrEmpty(folderPath))
            {
                _folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            }

            var clipGuid = EditorPrefs.GetString(PrefsClip, "");
            if (!string.IsNullOrEmpty(clipGuid))
            {
                var clipPath = AssetDatabase.GUIDToAssetPath(clipGuid);
                if (!string.IsNullOrEmpty(clipPath))
                {
                    _clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                }
            }
        }

        // ============================================================ GUI

        void OnGUI()
        {
            EnsureStyles();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Простановка музыки в SO уровней", _sHeader);
            EditorGUILayout.LabelField(
                "Клип пишется в поле Music каждого SimpleLevelSO внутри папки (рекурсивно). " +
                "Уровень читает его при загрузке: LevelController.SetInitialLevelState() → AudioService.PlayMusic().",
                _sSub);
            EditorGUILayout.Space(8);

            DrawInputs();
            EditorGUILayout.Space(6);
            DrawActions();
            EditorGUILayout.Space(6);
            DrawRows();
            DrawFooter();
        }

        void DrawInputs()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _folder = (DefaultAsset)EditorGUILayout.ObjectField(
                    new GUIContent("Папка пака", "Перетащи сюда папку, например Assets/LA8_slv/Levels/Pack02"),
                    _folder, typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _scanned = false;
                    _rows.Clear();
                    EditorPrefs.SetString(PrefsFolder, FolderPath ?? "");
                }

                if (_folder != null && FolderPath == null)
                {
                    EditorGUILayout.HelpBox("Это не папка. Нужна именно папка проекта.", MessageType.Error);
                }

                EditorGUI.BeginChangeCheck();
                _clip = (AudioClip)EditorGUILayout.ObjectField("Музыка пака", _clip, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    var path = AssetDatabase.GetAssetPath(_clip);
                    EditorPrefs.SetString(PrefsClip,
                        string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
                }

                if (_clip == null)
                {
                    EditorGUILayout.HelpBox(
                        "Клип не выбран. Применение с пустым полем сотрёт музыку у уровней " +
                        "(PlayMusic вызывается всё равно и заглушит трек).", MessageType.Warning);
                }
            }
        }

        void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(FolderPath == null))
                {
                    if (GUILayout.Button("Найти уровни", GUILayout.Height(24), GUILayout.Width(140)))
                    {
                        Scan();
                    }
                }

                var selected = _rows.Count(r => r.Selected);
                using (new EditorGUI.DisabledScope(selected == 0))
                {
                    if (GUILayout.Button($"Проставить в {selected} SO", GUILayout.Height(24), GUILayout.Width(180)))
                    {
                        Apply();
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_rows.Count == 0))
                {
                    if (GUILayout.Button("Выделить всё", GUILayout.Width(110)))
                    {
                        foreach (var r in _rows) r.Selected = true;
                    }

                    if (GUILayout.Button("Снять всё", GUILayout.Width(100)))
                    {
                        foreach (var r in _rows) r.Selected = false;
                    }
                }
            }
        }

        void DrawRows()
        {
            if (!_scanned)
            {
                EditorGUILayout.HelpBox("Укажи папку и нажми «Найти уровни».", MessageType.Info);
                return;
            }

            if (_rows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"В «{FolderPath}» не найдено ни одного SO уровня (ассета с полем {MusicField}).",
                    MessageType.Warning);
                return;
            }

            var distinct = _rows.Select(r => r.Current).Distinct().ToList();
            EditorGUILayout.LabelField(
                $"Найдено SO уровней: {_rows.Count} · разных треков сейчас: {distinct.Count} " +
                $"({string.Join(", ", distinct.Select(c => c == null ? "<пусто>" : c.name))})",
                _sSub);

            using (var scope = new EditorGUILayout.ScrollViewScope(_scroll, EditorStyles.helpBox))
            {
                _scroll = scope.scrollPosition;

                foreach (var row in _rows)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(18));

                        if (GUILayout.Button(row.Label, EditorStyles.linkLabel, GUILayout.Width(230)))
                        {
                            Selection.activeObject = row.Asset;
                            EditorGUIUtility.PingObject(row.Asset);
                        }

                        var isSame = row.Current == _clip;
                        var currentName = row.Current == null ? "<пусто>" : row.Current.name;
                        var color = GUI.color;
                        if (isSame) GUI.color = new Color(0.55f, 0.55f, 0.55f);
                        EditorGUILayout.LabelField(isSame ? $"{currentName}  (уже стоит)" : currentName, _sMono);
                        GUI.color = color;
                    }
                }
            }
        }

        void DrawFooter()
        {
            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            EditorGUILayout.LabelField(
                "После смены трека: Default Local Group собирается Pack Separately, поэтому новый клип " +
                "продублируется в бандл каждого уровня. Добавь его в группу Duplicate Asset Isolation1 " +
                "или прогони Addressables → Analyze → Check Duplicate Bundle Dependencies → Fix.",
                _sSub);
        }

        // ============================================================ логика

        string FolderPath
        {
            get
            {
                if (_folder == null) return null;
                var path = AssetDatabase.GetAssetPath(_folder);
                return AssetDatabase.IsValidFolder(path) ? path : null;
            }
        }

        void Scan()
        {
            _rows.Clear();
            _status = "";
            _scanned = true;

            var folder = FolderPath;
            if (folder == null) return;

            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;

                var so = new SerializedObject(asset);
                var music = so.FindProperty(MusicField);
                if (music == null || music.propertyType != SerializedPropertyType.ObjectReference) continue;

                _rows.Add(new Row
                {
                    Asset = asset,
                    Path = path,
                    Label = BuildLabel(asset, so),
                    Current = music.objectReferenceValue as AudioClip
                });
            }

            _rows.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            _status = $"Найдено {_rows.Count} SO уровней в «{folder}».";
        }

        static string BuildLabel(ScriptableObject asset, SerializedObject so)
        {
            var number = so.FindProperty(LevelNumberField);
            var name = so.FindProperty(LevelNameField);

            var label = asset.name;
            if (number != null && number.propertyType == SerializedPropertyType.Integer)
            {
                label = $"[{number.intValue}] {label}";
            }

            if (name != null && name.propertyType == SerializedPropertyType.String &&
                !string.IsNullOrEmpty(name.stringValue))
            {
                label = $"{label} — {name.stringValue}";
            }

            return label;
        }

        void Apply()
        {
            var targets = _rows.Where(r => r.Selected).ToList();
            if (targets.Count == 0) return;

            var clipName = _clip == null ? "<пусто>" : _clip.name;
            var already = targets.Count(r => r.Current == _clip);

            var message =
                $"Проставить «{clipName}» в {targets.Count} SO уровней?\n\n" +
                $"Уже с этим треком: {already}\nБудет изменено: {targets.Count - already}\n\n" +
                "Операция откатывается через Ctrl+Z.";

            if (!EditorUtility.DisplayDialog("Простановка музыки", message, "Проставить", "Отмена"))
            {
                return;
            }

            var changed = 0;

            foreach (var row in targets)
            {
                var so = new SerializedObject(row.Asset);
                var music = so.FindProperty(MusicField);
                if (music == null) continue;

                if (music.objectReferenceValue == _clip) continue;

                // ApplyModifiedProperties сам регистрирует Undo
                music.objectReferenceValue = _clip;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(row.Asset);

                row.Current = _clip;
                changed++;
            }

            AssetDatabase.SaveAssets();

            _status = $"Готово: изменено {changed} из {targets.Count} SO (остальные уже были с «{clipName}»).";
            Debug.Log($"[LevelMusicAssigner] {_status}");
        }

        // ============================================================ стили

        void EnsureStyles()
        {
            if (_stylesReady) return;

            _sHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _sSub = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _sMono = new GUIStyle(EditorStyles.label);

            _stylesReady = true;
        }
    }
}
