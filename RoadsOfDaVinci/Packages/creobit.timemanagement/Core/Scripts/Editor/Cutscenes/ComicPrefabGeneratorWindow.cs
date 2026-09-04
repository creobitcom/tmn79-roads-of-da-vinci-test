#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.UI;
using UnityEditor;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Cutscenes
{
    /// <summary>
    /// Батч-генератор префабов комикс-сцен из CutsceneSequenceSO.
    /// Перетаскивай SO или папки в окно, собери список и жми Generate.
    /// </summary>
    public class ComicPrefabGeneratorWindow : EditorWindow
    {
        private GameObject _template;
        private bool _skipIfPrefabSet = true;
        private readonly List<CutsceneSequenceSO> _targets = new();
        private Vector2 _scroll;

        [MenuItem("Tools/Cutscenes/Comic Prefab Generator")]
        private static void Open()
        {
            GetWindow<ComicPrefabGeneratorWindow>("Comic Prefab Generator").minSize = new Vector2(420, 460);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Генерирует lvlNN_comic.prefab из SO (Frames -> ComicPhoto) и прописывает SO.ComicPrefab.\n" +
                "1) Укажи шаблон (префаб с ComicScene).\n" +
                "2) Перетащи SO или ПАПКИ в область ниже (папки сканируются рекурсивно).\n" +
                "3) Жми Generate. Frames в SO остаются как fallback.",
                MessageType.Info);

            _template = (GameObject)EditorGUILayout.ObjectField("Template (ComicScene)", _template, typeof(GameObject), false);
            if (_template != null && _template.GetComponent<ComicScene>() == null)
                EditorGUILayout.HelpBox("У шаблона нет компонента ComicScene.", MessageType.Error);

            _skipIfPrefabSet = EditorGUILayout.Toggle(
                new GUIContent("Пропускать, если ComicPrefab задан", "Не трогать SO, у которых ComicPrefab уже заполнен."),
                _skipIfPrefabSet);

            EditorGUILayout.Space(6);
            DrawDropArea();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Добавить все в проекте")) AddAllInProject();
            if (GUILayout.Button("Добавить выбранные")) AddSelection();
            using (new EditorGUI.DisabledScope(_targets.Count == 0))
                if (GUILayout.Button("Очистить")) _targets.Clear();
            EditorGUILayout.EndHorizontal();

            DrawTargetList();

            EditorGUILayout.Space(6);
            bool canRun = _template != null && _template.GetComponent<ComicScene>() != null && _targets.Count > 0;
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button($"Generate ({_targets.Count})", GUILayout.Height(34)))
                    Generate();
            }
        }

        private void DrawDropArea()
        {
            var rect = GUILayoutUtility.GetRect(0, 54, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Перетащи сюда SO или папки", EditorStyles.helpBox);

            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                        AddFromObject(obj);
                }
                evt.Use();
            }
        }

        private void DrawTargetList()
        {
            EditorGUILayout.LabelField($"Целей: {_targets.Count}", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120));
            int removeAt = -1;
            for (int i = 0; i < _targets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(_targets[i], typeof(CutsceneSequenceSO), false);
                bool already = _targets[i] != null && _targets[i].ComicPrefab != null;
                GUILayout.Label(already ? "есть prefab" : "", GUILayout.Width(80));
                if (GUILayout.Button("x", GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) _targets.RemoveAt(removeAt);
            EditorGUILayout.EndScrollView();
        }

        private void AddFromObject(Object obj)
        {
            if (obj == null) return;

            if (obj is CutsceneSequenceSO so)
            {
                AddTarget(so);
                return;
            }

            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                AddFromFolder(path);
        }

        private void AddFromFolder(string folderPath)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CutsceneSequenceSO", new[] { folderPath }))
                AddTarget(AssetDatabase.LoadAssetAtPath<CutsceneSequenceSO>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private void AddAllInProject()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CutsceneSequenceSO"))
                AddTarget(AssetDatabase.LoadAssetAtPath<CutsceneSequenceSO>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private void AddSelection()
        {
            foreach (var obj in Selection.objects)
                AddFromObject(obj);
        }

        private void AddTarget(CutsceneSequenceSO so)
        {
            if (so != null && !_targets.Contains(so))
                _targets.Add(so);
        }

        private void Generate()
        {
            int done = 0, skipped = 0, failed = 0;

            foreach (var so in _targets)
            {
                if (so == null) { skipped++; continue; }
                if (_skipIfPrefabSet && so.ComicPrefab != null) { skipped++; continue; }
                if (so.Frames == null || so.Frames.Count == 0) { skipped++; continue; }

                try
                {
                    if (GenerateFor(so)) done++;
                    else failed++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    Debug.LogError($"[ComicPrefabGenerator] Ошибка на {AssetDatabase.GetAssetPath(so)}: {e}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ComicPrefabGenerator] Готово. Создано: {done}, пропущено: {skipped}, ошибок: {failed}.");
            EditorUtility.DisplayDialog("Comic Prefab Generator",
                $"Создано: {done}\nПропущено: {skipped}\nОшибок: {failed}", "OK");
        }

        private bool GenerateFor(CutsceneSequenceSO so)
        {
            string templatePath = AssetDatabase.GetAssetPath(_template);
            string soPath = AssetDatabase.GetAssetPath(so);
            string dir = Path.GetDirectoryName(soPath).Replace("\\", "/");
            string baseName = Path.GetFileNameWithoutExtension(soPath)
                .Replace("_cutsene", "").Replace("_cutscene", "").TrimEnd('_', ' ');
            string savePath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{baseName}_comic.prefab");

            if (!AssetDatabase.CopyAsset(templatePath, savePath))
                return false;

            var root = PrefabUtility.LoadPrefabContents(savePath);
            try
            {
                var scene = root.GetComponent<ComicScene>();
                if (scene == null)
                    return false;

                var photos = scene.Photos;
                int used = Mathf.Min(so.Frames.Count, photos.Count);

                var extras = new List<GameObject>();
                for (int i = used; i < photos.Count; i++)
                    if (photos[i] != null)
                        extras.Add(photos[i].gameObject);

                for (int i = 0; i < used; i++)
                {
                    var p = photos[i];
                    if (p == null) continue;

                    var frame = so.Frames[i];
                    if (p.Image != null)
                        p.Image.sprite = frame.FrameSprite;

                    p.FadeDuration = frame.FadeDuration;
                    p.PopDuration = frame.PopDuration > 0f ? frame.PopDuration : 0.3f;
                    p.DurationSeconds = frame.DurationSeconds;
                }

                var sceneSo = new SerializedObject(scene);
                var arr = sceneSo.FindProperty("_photos");
                if (arr != null && used < arr.arraySize)
                {
                    arr.arraySize = used;
                    sceneSo.ApplyModifiedProperties();
                }

                foreach (var go in extras)
                    Object.DestroyImmediate(go);

                PrefabUtility.SaveAsPrefabAsset(root, savePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(savePath);
            if (saved == null) return false;

            so.ComicPrefab = saved;
            EditorUtility.SetDirty(so);
            return true;
        }
    }
}
#endif
