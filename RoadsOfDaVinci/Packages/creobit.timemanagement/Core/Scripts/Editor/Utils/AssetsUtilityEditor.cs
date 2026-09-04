using UnityEditor;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Utils
{
    public class AssetsUtilityEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Force Save All Assets")]
        public static void ForceSaveAllAssets()
        {
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets/8floor", "Assets/GodsAndRoads1" });

            Debug.Log($"Forcefully re-saving {guids.Length} assets.");

            for (int i = 0, length = guids.Length; i < length; i++)
            {
                if (string.IsNullOrEmpty(guids[i]))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guids[i]));
                
                if (asset == null)
                {
                    continue;
                }

                if ((asset.hideFlags & (HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor)) != 0)
                {
                    continue;
                }

                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
        }
    }
}