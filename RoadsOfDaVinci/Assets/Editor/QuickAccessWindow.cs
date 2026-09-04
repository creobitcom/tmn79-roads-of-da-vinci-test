using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class QuickAccessWindow : EditorWindow
{
    private readonly List<string> sceneGuids = new List<string>();
    private readonly List<string> bookmarkGuids = new List<string>();
    private readonly List<string> recentGuids = new List<string>();

    private Vector2 scrollPos;
    private string searchQuery = string.Empty;
    private bool showScenes = true;
    private bool showBookmarks = true;
    private bool showRecent = true;
    private bool pendingHistoryRefresh;
    private bool stylesDirty = true;

    private GUIStyle clippedLabelStyle;
    private GUIStyle clippedButtonStyle;
    private GUIStyle dropZoneStyle;

    private const int MaxRecentItems = 12;
    private const string DataFileName = "QuickAccess.json";

    [MenuItem("Tools/LA8/Quick Access", false, 10)]
    public static void ShowWindow()
    {
        var window = GetWindow<QuickAccessWindow>("Quick Access");
        window.minSize = new Vector2(240f, 220f);
    }

    private void OnEnable()
    {
        stylesDirty = true;
        clippedLabelStyle = null;
        clippedButtonStyle = null;
        dropZoneStyle = null;
        LoadData();
        Selection.selectionChanged -= OnSelectionChanged;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.quitting += OnEditorQuitting;
        wantsMouseMove = true;
    }

    private void OnDisable()
    {
        SaveData();
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.quitting -= OnEditorQuitting;
    }

    private void OnEditorQuitting()
    {
        SaveData();
    }

    private void OnSelectionChanged()
    {
        pendingHistoryRefresh = true;
        Repaint();
    }

    private void EnsureStyles()
    {
        if (!stylesDirty && clippedLabelStyle != null && clippedButtonStyle != null && dropZoneStyle != null)
            return;

        clippedLabelStyle = new GUIStyle(EditorStyles.label)
        {
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft
        };

        clippedButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft
        };

        dropZoneStyle = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };

        stylesDirty = false;
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (pendingHistoryRefresh && Event.current.type == EventType.Layout)
        {
            pendingHistoryRefresh = false;
            RefreshRecentFromSelection();
        }

        DrawToolbar();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);

        DrawSceneSection();
        EditorGUILayout.Space(8);
        DrawBookmarkSection();
        EditorGUILayout.Space(8);
        DrawRecentSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Поиск", GUILayout.Width(42f));
        searchQuery = GUILayout.TextField(searchQuery ?? string.Empty, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("Очистить", EditorStyles.toolbarButton, GUILayout.Width(64f)))
            searchQuery = string.Empty;
        EditorGUILayout.EndHorizontal();
    }

    private bool PassesSearch(string name, string path)
    {
        if (string.IsNullOrEmpty(searchQuery))
            return true;

        string q = searchQuery.Trim();
        if (q.Length == 0)
            return true;

        return (!string.IsNullOrEmpty(name) && name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
               || (!string.IsNullOrEmpty(path) && path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void RefreshRecentFromSelection()
    {
        UnityEngine.Object activeObj = Selection.activeObject;
        if (activeObj == null || !AssetDatabase.Contains(activeObj))
            return;

        string path = AssetDatabase.GetAssetPath(activeObj);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid) || bookmarkGuids.Contains(guid))
            return;

        recentGuids.Remove(guid);
        recentGuids.Insert(0, guid);

        while (recentGuids.Count > MaxRecentItems)
            recentGuids.RemoveAt(recentGuids.Count - 1);

        SaveData();
    }

    private void DrawSceneSection()
    {
        showScenes = EditorGUILayout.Foldout(showScenes, "Сцены", true);
        if (!showScenes)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Добавить текущую сцену", GUILayout.Height(22f)))
            AddCurrentScene();
        EditorGUILayout.EndHorizontal();

        Rect dropZone = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
        DrawDropZone(dropZone, "Перетащите .unity сюда");
        HandleSceneDrop(dropZone);

        int visible = 0;
        for (int i = 0; i < sceneGuids.Count; i++)
        {
            string guid = sceneGuids[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!PassesSearch(name, path))
                continue;

            visible++;
            DrawSceneRow(i, guid, path);
        }

        if (sceneGuids.Count == 0)
            EditorGUILayout.LabelField("Список сцен пуст", EditorStyles.centeredGreyMiniLabel);
        else if (visible == 0)
            EditorGUILayout.LabelField("Ничего не найдено", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawSceneRow(int index, string guid, string path)
    {
        SceneAsset sceneAsset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

        if (sceneAsset == null)
        {
            GUILayout.Label("Сцена не найдена", clippedLabelStyle, GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        }
        else
        {
            Texture icon = EditorGUIUtility.IconContent("SceneAsset Icon").image;
            var content = new GUIContent(sceneAsset.name, icon, path);
            if (GUILayout.Button(content, clippedButtonStyle, GUILayout.Height(22f), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(path);
                GUIUtility.ExitGUI();
            }
        }

        if (GUILayout.Button("X", GUILayout.Width(22f), GUILayout.Height(20f)))
        {
            sceneGuids.RemoveAt(index);
            SaveData();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawBookmarkSection()
    {
        showBookmarks = EditorGUILayout.Foldout(showBookmarks, "Закладки (файлы и папки)", true);
        if (!showBookmarks)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Добавить выделенное", GUILayout.Height(22f)))
            AddCurrentSelectionToBookmarks();
        EditorGUILayout.EndHorizontal();

        Rect dropZone = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
        DrawDropZone(dropZone, "Перетащите файлы и папки сюда");
        HandleAssetDrop(dropZone, bookmarkGuids, true);

        int visible = 0;
        for (int i = 0; i < bookmarkGuids.Count; i++)
        {
            string guid = bookmarkGuids[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
            if (!PassesSearch(name, path))
                continue;

            visible++;
            DrawAssetRow(guid, true);
        }

        if (bookmarkGuids.Count == 0)
            EditorGUILayout.LabelField("Нет закладок — перетащите сюда файлы или папки", EditorStyles.centeredGreyMiniLabel);
        else if (visible == 0)
            EditorGUILayout.LabelField("Ничего не найдено", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawRecentSection()
    {
        showRecent = EditorGUILayout.Foldout(showRecent, "Недавние", true);
        if (!showRecent)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool any = false;
        for (int i = 0; i < recentGuids.Count; i++)
        {
            string guid = recentGuids[i];
            if (bookmarkGuids.Contains(guid) || sceneGuids.Contains(guid))
                continue;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
            if (!PassesSearch(name, path))
                continue;

            any = true;
            DrawAssetRow(guid, false);
        }

        if (!any)
            EditorGUILayout.LabelField("История пуста", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawAssetRow(string guid, bool isBookmark)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        UnityEngine.Object obj = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        bool isFolder = !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);

        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

        if (obj == null && !isFolder)
        {
            GUILayout.Label("Ассет не найден", clippedLabelStyle, GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        }
        else
        {
            Texture icon = GetAssetIcon(obj, isFolder);
            string label = isFolder ? Path.GetFileName(path) : obj.name;
            var content = new GUIContent(label, icon, path);
            if (GUILayout.Button(content, clippedButtonStyle, GUILayout.Height(22f), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true)))
            {
                NavigateToAsset(obj, path, isFolder);
            }
        }

        if (!isBookmark)
        {
            if (GUILayout.Button("+", GUILayout.Width(22f), GUILayout.Height(20f)))
            {
                AddBookmark(guid);
                GUIUtility.ExitGUI();
            }
        }
        else if (GUILayout.Button("X", GUILayout.Width(22f), GUILayout.Height(20f)))
        {
            bookmarkGuids.Remove(guid);
            SaveData();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
    }

    private static Texture GetAssetIcon(UnityEngine.Object obj, bool isFolder)
    {
        if (isFolder)
            return EditorGUIUtility.IconContent("Folder Icon").image;

        if (obj != null)
        {
            Texture thumb = AssetPreview.GetMiniThumbnail(obj);
            if (thumb != null)
                return thumb;
        }

        return EditorGUIUtility.IconContent("DefaultAsset Icon").image;
    }

    private static void NavigateToAsset(UnityEngine.Object obj, string path, bool isFolder)
    {
        EditorUtility.FocusProjectWindow();

        if (isFolder)
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }

            return;
        }

        if (obj == null)
            return;

        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);

        if (obj is SceneAsset)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }
    }

    private void DrawDropZone(Rect dropZone, string label)
    {
        bool hover = dropZone.Contains(Event.current.mousePosition)
                     && (Event.current.type == EventType.DragUpdated
                         || Event.current.type == EventType.DragPerform
                         || DragAndDrop.objectReferences.Length > 0 && dropZone.Contains(Event.current.mousePosition));

        Color prev = GUI.color;
        if (hover && DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
            GUI.color = new Color(0.6f, 0.85f, 1f, 1f);

        GUI.Box(dropZone, label, dropZoneStyle);
        GUI.color = prev;
    }

    private void AddCurrentScene()
    {
        string currentPath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(currentPath))
        {
            Debug.LogWarning("Сначала сохраните сцену.");
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(currentPath);
        if (string.IsNullOrEmpty(guid) || sceneGuids.Contains(guid))
            return;

        sceneGuids.Add(guid);
        SaveData();
    }

    private void AddCurrentSelectionToBookmarks()
    {
        UnityEngine.Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
            return;

        bool added = false;
        for (int i = 0; i < selected.Length; i++)
        {
            UnityEngine.Object obj = selected[i];
            if (obj == null || !AssetDatabase.Contains(obj))
                continue;

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
            added |= AddBookmark(guid, false);
        }

        if (added)
            SaveData();
    }

    private bool AddBookmark(string guid, bool save = true)
    {
        if (string.IsNullOrEmpty(guid) || bookmarkGuids.Contains(guid))
            return false;

        bookmarkGuids.Add(guid);
        recentGuids.Remove(guid);
        if (save)
            SaveData();
        return true;
    }

    private void HandleSceneDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            return;

        if (!dropArea.Contains(evt.mousePosition))
            return;

        bool hasScene = false;
        UnityEngine.Object[] refs = DragAndDrop.objectReferences;
        for (int i = 0; i < refs.Length; i++)
        {
            if (refs[i] is SceneAsset)
            {
                hasScene = true;
                break;
            }
        }

        if (!hasScene)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            for (int i = 0; i < refs.Length; i++)
            {
                if (refs[i] is not SceneAsset)
                    continue;

                string path = AssetDatabase.GetAssetPath(refs[i]);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid) && !sceneGuids.Contains(guid))
                    sceneGuids.Add(guid);
            }

            SaveData();
        }

        evt.Use();
    }

    private void HandleAssetDrop(Rect dropArea, List<string> target, bool removeFromRecent)
    {
        Event evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            return;

        if (!dropArea.Contains(evt.mousePosition))
            return;

        UnityEngine.Object[] refs = DragAndDrop.objectReferences;
        if (refs == null || refs.Length == 0)
            return;

        bool hasAsset = false;
        for (int i = 0; i < refs.Length; i++)
        {
            if (refs[i] != null && AssetDatabase.Contains(refs[i]))
            {
                hasAsset = true;
                break;
            }
        }

        if (!hasAsset)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            for (int i = 0; i < refs.Length; i++)
            {
                UnityEngine.Object obj = refs[i];
                if (obj == null || !AssetDatabase.Contains(obj))
                    continue;

                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
                if (string.IsNullOrEmpty(guid) || target.Contains(guid))
                    continue;

                target.Add(guid);
                if (removeFromRecent)
                    recentGuids.Remove(guid);
            }

            SaveData();
        }

        evt.Use();
    }

    private static string GetDataPath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string userSettings = Path.Combine(projectRoot, "UserSettings");
        if (!Directory.Exists(userSettings))
            Directory.CreateDirectory(userSettings);

        return Path.Combine(userSettings, DataFileName);
    }

    private void SaveData()
    {
        var data = new PersistData
        {
            scenes = sceneGuids.ToArray(),
            bookmarks = bookmarkGuids.ToArray(),
            recent = recentGuids.ToArray(),
            showScenes = showScenes,
            showBookmarks = showBookmarks,
            showRecent = showRecent
        };

        File.WriteAllText(GetDataPath(), JsonUtility.ToJson(data, true));
    }

    private void LoadData()
    {
        sceneGuids.Clear();
        bookmarkGuids.Clear();
        recentGuids.Clear();

        string path = GetDataPath();
        if (File.Exists(path))
        {
            var data = JsonUtility.FromJson<PersistData>(File.ReadAllText(path));
            if (data != null)
            {
                AddRangeUnique(sceneGuids, data.scenes);
                AddRangeUnique(bookmarkGuids, data.bookmarks);
                AddRangeUnique(recentGuids, data.recent);
                showScenes = data.showScenes;
                showBookmarks = data.showBookmarks;
                showRecent = data.showRecent;
                MigratePinnedIfNeeded(data);
                return;
            }
        }

        LoadLegacyEditorPrefs();
        SaveData();
    }

    private void MigratePinnedIfNeeded(PersistData data)
    {
        if (data.bookmarks != null && data.bookmarks.Length > 0)
            return;

        if (data.pinned == null || data.pinned.Length == 0)
            return;

        AddRangeUnique(bookmarkGuids, data.pinned);
        SaveData();
    }

    private void LoadLegacyEditorPrefs()
    {
        string key = Application.productName;
        string loadedScenes = EditorPrefs.GetString("QuickAccess_Scenes" + key, string.Empty);
        if (!string.IsNullOrEmpty(loadedScenes))
            AddRangeUnique(sceneGuids, loadedScenes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

        string loadedPinned = EditorPrefs.GetString("QuickAccess_Pinned" + key, string.Empty);
        if (!string.IsNullOrEmpty(loadedPinned))
            AddRangeUnique(bookmarkGuids, loadedPinned.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void AddRangeUnique(List<string> target, IEnumerable<string> source)
    {
        if (source == null)
            return;

        foreach (string item in source)
        {
            if (!string.IsNullOrEmpty(item) && !target.Contains(item))
                target.Add(item);
        }
    }

    [Serializable]
    private class PersistData
    {
        public string[] scenes;
        public string[] bookmarks;
        public string[] pinned;
        public string[] recent;
        public bool showScenes = true;
        public bool showBookmarks = true;
        public bool showRecent = true;
    }
}
