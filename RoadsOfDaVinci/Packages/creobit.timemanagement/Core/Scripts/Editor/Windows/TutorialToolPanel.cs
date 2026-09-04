using System;
using System.IO;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using Creobit.UI;
using Creobit.UI.Utility;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Windows
{
    public class TutorialToolPanel : OdinEditorWindow
    {
        [SerializeField]
        [FolderPath]
        private string pathToSavePanelPrefabs = "Assets/UI/Windows";
        
        [SerializeField]
        private string panelName = "Level0Tutorial0";
        
        [SerializeField]
        private GameObject tutorialPresetPrefab;
        
        [SerializeField] 
        [ReadOnly] 
        private SerializedDictionary<string, AssetReference> assetReferences = new();
        
        
        [MenuItem("Tools/Creobit/TMN/Tutorial tool")]
        private static void OpenWindow()
        {
            var window = GetWindow<TutorialToolPanel>();

            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 250);
        }
        
        [Button]
        private void CreateTutorialPrefab()
        {
            var panelObject = CreateTutorialObject(panelName, null, 
                typeof(TutorialView), typeof(PanelData));

            var prefabFolder = GetPrefabFolder();
            var prefabPath = Path.Combine(prefabFolder, $"{panelObject.name}.prefab");
            

            var addressableAssetSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            var addressableAssetGroup = addressableAssetSettings.FindGroup(RuntimeConstants.AddressablesGroupName) ??
                                        addressableAssetSettings.CreateGroup(RuntimeConstants.AddressablesGroupName,
                                            false, false, true, null, 
                                            typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
            
            var assetGUIDPanel = AssetDatabase.AssetPathToGUID(prefabPath);
            var panelReference = CreatePanelReference(panelName, assetGUIDPanel);
            var view = panelObject.GetComponent<TutorialView>();
            var tutorialSO = CreateTutorialReference(panelName, panelReference);
            
            PrefabUtility.SaveAsPrefabAsset(panelObject, prefabPath);
            AssetDatabase.SaveAssets();
            
            assetGUIDPanel = AssetDatabase.AssetPathToGUID(prefabPath);
            panelReference.UIPanelReference = new AssetReference(assetGUIDPanel);
            
            DestroyImmediate(panelObject);
            var addressableAssetEntry = addressableAssetSettings.FindAssetEntry(assetGUIDPanel) ?? 
                                        addressableAssetSettings.CreateOrMoveEntry(assetGUIDPanel, addressableAssetGroup);

            addressableAssetSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved,
                addressableAssetEntry, true);

            AssetDatabase.SaveAssets();

            var prefab = AssetDatabase.LoadAssetAtPath<TutorialView>(AssetDatabase.GUIDToAssetPath(assetGUIDPanel));
            GetExistingPanelPrefabs();
        }
        

        private static TutorialViewSo CreateTutorialReference(string panelPrefabName, PanelReference panelReference)
        {
            var tutorialViewSo = CreateInstance<TutorialViewSo>();

            var panelReferencePath = Path.Combine(RuntimeConstants.Paths.PathToSavePanelPrefabs,
                $"{panelPrefabName}/{panelPrefabName}SO.asset");

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(panelReferencePath);
            tutorialViewSo.panelReference = panelReference;

            AssetDatabase.CreateAsset(tutorialViewSo, assetPath);
            AssetDatabase.SaveAssets();
            return tutorialViewSo;
        }
        
        private static PanelReference CreatePanelReference(string panelPrefabName, string guid)
        {
            var panelReference = CreateInstance<PanelReference>();

            panelReference.UIPanelReference = new AssetReference(guid);

            var panelReferencePath = Path.Combine(RuntimeConstants.Paths.PathToSavePanelPrefabs,
                $"{panelPrefabName}/{panelPrefabName}Reference.asset");

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(panelReferencePath);

            AssetDatabase.CreateAsset(panelReference, assetPath);
            AssetDatabase.SaveAssets();
            return panelReference;
        }
        
        private GameObject CreateTutorialObject(string objectName,
            Transform parent = null, params Type[] components)
        {
            var emptyObject = new GameObject(objectName);
            var preset = Instantiate(tutorialPresetPrefab, emptyObject.transform);
            var panelPreset = preset.GetComponent<PanelData>();
            var tutorialPreset = preset.GetComponent<TutorialView>();

            foreach (var component in components)
            {
                emptyObject.AddComponent(component);
            }

            emptyObject.GetComponent<TutorialView>().buttonToClose = tutorialPreset.buttonToClose;
            var panelData = emptyObject.GetComponent<PanelData>();
            panelData.PanelAnimations = panelPreset.PanelAnimations;
            DestroyImmediate(panelPreset);
            DestroyImmediate(tutorialPreset);

            emptyObject.transform.SetParent(parent);

            return emptyObject;
        }

        private string GetPrefabFolder()
        {
            if (!Directory.Exists(pathToSavePanelPrefabs))
            {
                Directory.CreateDirectory(pathToSavePanelPrefabs);

                AssetDatabase.Refresh();
            }

            var prefabFolder = Path.Combine(pathToSavePanelPrefabs, panelName);

            if (!Directory.Exists(prefabFolder))
            {
                Directory.CreateDirectory(prefabFolder);

                AssetDatabase.Refresh();
            }

            return prefabFolder;
        }
        
        private void GetExistingPanelPrefabs()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            var group = settings.FindGroup(RuntimeConstants.AddressablesGroupName);

            assetReferences = new SerializedDictionary<string, AssetReference>();

            foreach (var entry in group.entries)
            {
                var assetReference = new AssetReference(entry.guid);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(entry.guid));

                assetReferences.Add(prefab.name, assetReference);
            }
        }
    }
}