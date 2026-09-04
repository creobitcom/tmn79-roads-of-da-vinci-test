using System;
using System.IO;
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

namespace Creobit.UI.Editor
{
    public class UIToolPanel : OdinEditorWindow
    {
        [SerializeField] [FolderPath]
        private string _pathToSavePanelPrefabs = RuntimeConstants.Paths.PathToSavePanelPrefabs;

        [SerializeField] [ReadOnly] private SerializedDictionary<string, AssetReference> _assetReferences = new();

        [SerializeField] private string _panelName;

        [SerializeField] private PanelAnimationData[] _panelAnimations;

        private void OnBecameVisible()
        {
            GetExistingPanelPrefabs();
        }

        [MenuItem(RuntimeConstants.MenuItems.UiToolPanel)]
        private static void OpenWindow()
        {
            var window = GetWindow<UIToolPanel>();

            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 250);
        }

        [Button]
        [HideIf(nameof(IsPathEmpty))]
        [HideIf(nameof(IsPanelNameEmpty))]
        [ShowIf(nameof(ValidatePanelName))]
        private void CreatePanelPrefab()
        {
            var panelObject = CreateEmptyUIObject(_panelName, null, typeof(RectTransform), 
                typeof(PanelData));

            var panelAnimations = CreateEmptyUIObject("Animations", panelObject.transform);

            CreateEmptyUIObject("Bridges", panelObject.transform);

            var panelData = panelObject.GetComponent<PanelData>();

            SetAnimationPrefab(_panelAnimations, panelData, panelAnimations.transform);

            if (!Directory.Exists(_pathToSavePanelPrefabs))
            {
                Directory.CreateDirectory(_pathToSavePanelPrefabs);

                AssetDatabase.Refresh();
            }

            var prefabFolder = Path.Combine(_pathToSavePanelPrefabs, _panelName);

            if (!Directory.Exists(prefabFolder))
            {
                Directory.CreateDirectory(prefabFolder);

                AssetDatabase.Refresh();
            }

            var prefabPath = Path.Combine(prefabFolder, $"{panelObject.name}.prefab");

            PrefabUtility.SaveAsPrefabAsset(panelObject, prefabPath);

            DestroyImmediate(panelObject);

            var addressableAssetSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            var addressableAssetGroup = addressableAssetSettings.FindGroup(RuntimeConstants.AddressablesGroupName) ??
                                        addressableAssetSettings.CreateGroup(RuntimeConstants.AddressablesGroupName,
                                            false, false, true, null, typeof(ContentUpdateGroupSchema),
                                            typeof(BundledAssetGroupSchema));

            var assetGuid = AssetDatabase.AssetPathToGUID(prefabPath);

            CreatePanelReference(_panelName, assetGuid);

            var addressableAssetEntry = addressableAssetSettings.FindAssetEntry(assetGuid) ??
                                        addressableAssetSettings.CreateOrMoveEntry(assetGuid, addressableAssetGroup);

            addressableAssetSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved,
                addressableAssetEntry, true);

            AssetDatabase.SaveAssets();

            GetExistingPanelPrefabs();
        }

        private static void SetAnimationPrefab(PanelAnimationData[] animationData, PanelData panelData,
            Transform parent)
        {
            foreach (var panelAnimationData in animationData)
            {
                if (panelAnimationData.AnimationPrefab == null)
                {
                    continue;
                }

                PrefabUtility.InstantiatePrefab(panelAnimationData.AnimationPrefab, parent);
            }

            panelData.PanelAnimations = animationData;
        }

        [Button]
        private void CreateBridgePrefab()
        {
            var uiBridge = CreateEmptyUIObject("UI Bridge");

            uiBridge.AddComponent<UIBridge>();
        }

        private static void CreatePanelReference(string panelPrefabName, string guid)
        {
            var panelReference = CreateInstance<PanelReference>();

            panelReference.UIPanelReference = new AssetReference(guid);

            var panelReferencePath = Path.Combine(RuntimeConstants.Paths.PathToSavePanelPrefabs,
                $"{panelPrefabName}/{panelPrefabName}Reference.asset");

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(panelReferencePath);

            AssetDatabase.CreateAsset(panelReference, assetPath);
            AssetDatabase.SaveAssets();
        }

        private void GetExistingPanelPrefabs()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            var group = settings.FindGroup(RuntimeConstants.AddressablesGroupName);

            _assetReferences = new SerializedDictionary<string, AssetReference>();

            foreach (var entry in group.entries)
            {
                var assetReference = new AssetReference(entry.guid);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(entry.guid));

                _assetReferences.Add(prefab.name, assetReference);
            }
        }

        private static GameObject CreateEmptyUIObject(string objectName,
            Transform parent = null, params Type[] components)
        {
            var emptyObject = new GameObject(objectName);

            foreach (var component in components) emptyObject.AddComponent(component);

            emptyObject.transform.SetParent(parent);

            return emptyObject;
        }

        private bool ValidatePanelName()
        {
            return !string.IsNullOrEmpty(_panelName) &&
                   _assetReferences != null &&
                   !_assetReferences.ContainsKey(_panelName);
        }

        private bool IsPanelNameEmpty()
        {
            return string.IsNullOrEmpty(_panelName);
        }

        private bool IsPathEmpty()
        {
            return _pathToSavePanelPrefabs.Length == 0;
        }
    }
}