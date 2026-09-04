#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class TutorialPrefabSaveButton : MonoBehaviour
{
#if UNITY_EDITOR

    public GameObject prefabAsset; // MUST be from Project window

    public void Save()
    {
        if (prefabAsset == null)
        {
            Debug.LogError("Drag PREFAB from Project window here (NOT scene object)");
            return;
        }

        string path = UnityEditor.AssetDatabase.GetAssetPath(prefabAsset);

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Prefab path is empty. You assigned wrong object.");
            return;
        }

        PrefabUtility.SaveAsPrefabAsset(
            gameObject,
            path
        );

        Debug.Log("Prefab saved successfully");
    }

#endif
}