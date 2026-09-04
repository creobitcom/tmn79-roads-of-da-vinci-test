#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialPrefabSaveButton))]
public class TutorialPrefabSaveButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TutorialPrefabSaveButton script = (TutorialPrefabSaveButton)target;

        GUILayout.Space(10);

        if (GUILayout.Button("💾 SAVE TUTORIAL PREFAB"))
        {
            script.Save();
        }
    }
}
#endif