using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[Overlay(typeof(SceneView), "Level Navigator", true)]
public class LevelNavigatorOverlay : Overlay
{
    private const string PREFS_ALL_LEVELS = "LevelNavigator_AllLevels";

    private AllLevelsSO allLevels;
    private int currentIndex = -1;

    private IntegerField numberField;
    private Button prevButton;
    private Button nextButton;
    private IMGUIContainer gearButton;
    private Label statusLabel;

    public override void OnCreated()
    {
        base.OnCreated();
        string path = EditorPrefs.GetString(PREFS_ALL_LEVELS + Application.productName, "");
        if (!string.IsNullOrEmpty(path))
        {
            allLevels = AssetDatabase.LoadAssetAtPath<AllLevelsSO>(path);
        }

        PrefabStage.prefabStageOpened += OnPrefabStageOpened;
    }

    public override void OnWillBeDestroyed()
    {
        PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
        base.OnWillBeDestroyed();
    }

    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement { style = { minWidth = 130 } };

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

        gearButton = new IMGUIContainer(() =>
        {
            if (GUILayout.Button("⚙", GUILayout.Width(32), GUILayout.Height(18)))
            {
                UnityEditor.PopupWindow.Show(GUILayoutUtility.GetLastRect(), new AllLevelsPopup(this));
            }
        });
        gearButton.style.width = 34;
        gearButton.style.height = 20;
        row.Add(gearButton);

        prevButton = new Button(() => OpenEntry(currentIndex - 1)) { text = "<" };
        prevButton.style.width = 24;
        row.Add(prevButton);

        numberField = new IntegerField { isDelayed = true };
        numberField.style.width = 36;
        numberField.RegisterValueChangedCallback(evt => TryJumpToTypedLevel(evt.newValue));
        row.Add(numberField);

        var openButton = new Button(() => TryJumpToTypedLevel(numberField.value)) { text = "▶" };
        openButton.style.width = 24;
        row.Add(openButton);

        nextButton = new Button(() => OpenEntry(currentIndex + 1)) { text = ">" };
        nextButton.style.width = 24;
        row.Add(nextButton);

        root.Add(row);

        statusLabel = new Label { style = { marginTop = 2, whiteSpace = WhiteSpace.Normal } };
        root.Add(statusLabel);

        SyncFromCurrentPrefabStage();
        RefreshNavRow();

        return root;
    }

    private void SetAllLevels(AllLevelsSO value)
    {
        allLevels = value;
        EditorPrefs.SetString(PREFS_ALL_LEVELS + Application.productName,
            allLevels != null ? AssetDatabase.GetAssetPath(allLevels) : "");
        currentIndex = -1;
        SyncFromCurrentPrefabStage();
        RefreshNavRow();
    }

    private void RefreshNavRow()
    {
        if (numberField == null) return;

        var entries = GetEntries();
        bool hasAsset = allLevels != null;
        bool hasEntries = entries.Count > 0;

        prevButton.SetEnabled(hasEntries && currentIndex > 0);
        nextButton.SetEnabled(hasEntries && currentIndex >= 0 && currentIndex < entries.Count - 1);
        numberField.SetEnabled(hasEntries);

        if (currentIndex >= 0 && currentIndex < entries.Count)
        {
            numberField.SetValueWithoutNotify(entries[currentIndex].FindPropertyRelative("levelNum").intValue);
        }

        statusLabel.text = !hasAsset
            ? "Назначьте AllLevels asset."
            : !hasEntries
                ? "AllLevels пуст."
                : "";
    }

    private void TryJumpToTypedLevel(int levelNum)
    {
        var entries = GetEntries();
        int index = entries.FindIndex(e => e.FindPropertyRelative("levelNum").intValue == levelNum);
        if (index < 0)
        {
            Debug.LogWarning($"LevelNavigator: level {levelNum} not found in AllLevels.", allLevels);
            if (allLevels != null)
            {
                Selection.activeObject = allLevels;
                EditorGUIUtility.PingObject(allLevels);
            }
            RefreshNavRow();
            return;
        }

        OpenEntry(index);
    }

    private void OpenEntry(int index)
    {
        var entries = GetEntries();
        if (index < 0 || index >= entries.Count) return;

        var entry = entries[index];
        int levelNum = entry.FindPropertyRelative("levelNum").intValue;
        string path = ResolvePrefabPath(entry);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"LevelNavigator: could not resolve prefab for level {levelNum}.");
            return;
        }

        currentIndex = index;
        PrefabStageUtility.OpenPrefab(path);
        RefreshNavRow();
    }

    // level -> Level{N}SO.m_AssetGUID -> Level{N}SO._levelPrefab -> LevelPrefab{N}.prefab
    private static string ResolvePrefabPath(SerializedProperty entry)
    {
        string levelSoGuid = entry.FindPropertyRelative("level")?.FindPropertyRelative("m_AssetGUID")?.stringValue;
        if (string.IsNullOrEmpty(levelSoGuid)) return null;

        string levelSoPath = AssetDatabase.GUIDToAssetPath(levelSoGuid);
        Object levelSo = AssetDatabase.LoadAssetAtPath<Object>(levelSoPath);
        if (levelSo == null) return null;

        var levelSoSerialized = new SerializedObject(levelSo);
        SerializedProperty prefabProperty = levelSoSerialized.FindProperty("_levelPrefab");
        GameObject prefab = prefabProperty != null ? prefabProperty.objectReferenceValue as GameObject : null;
        return prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
    }

    private List<SerializedProperty> GetEntries()
    {
        var result = new List<SerializedProperty>();
        if (allLevels == null) return result;

        var serialized = new SerializedObject(allLevels);
        SerializedProperty array = serialized.FindProperty("<AllLevels>k__BackingField");
        if (array == null || !array.isArray) return result;

        for (int i = 0; i < array.arraySize; i++)
        {
            result.Add(array.GetArrayElementAtIndex(i));
        }
        return result;
    }

    private void OnPrefabStageOpened(PrefabStage stage)
    {
        SyncFromCurrentPrefabStage();
        RefreshNavRow();
    }

    private void SyncFromCurrentPrefabStage()
    {
        if (allLevels == null) return;

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null) return;

        var entries = GetEntries();
        int index = entries.FindIndex(e => ResolvePrefabPath(e) == stage.assetPath);

        if (index >= 0)
        {
            currentIndex = index;
        }
    }

    private class AllLevelsPopup : PopupWindowContent
    {
        private readonly LevelNavigatorOverlay owner;

        public AllLevelsPopup(LevelNavigatorOverlay owner)
        {
            this.owner = owner;
        }

        public override Vector2 GetWindowSize() => new Vector2(260, 40);

        public override void OnGUI(Rect rect)
        {
            EditorGUI.BeginChangeCheck();
            var newValue = (AllLevelsSO)EditorGUILayout.ObjectField("All Levels", owner.allLevels, typeof(AllLevelsSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                owner.SetAllLevels(newValue);
                editorWindow.Close();
            }
        }
    }
}
