using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Directory = System.IO.Directory;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Windows
{
    public class LocalizationToolsWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Localization Manager")]
        private static void OpenWindow()
        {
            var window = GetWindow<LocalizationToolsWindow>();
            
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 240);
        }
        
        public LocalizationTools LocalizationTools;
    }
    
    [HideLabel]
    [Serializable]
    public class LocalizationTools
    {
        [field: SerializeField]
        [field: FolderPath]
        [field: OnValueChanged(nameof(LoadStringTables))]
        [field: Required]
        public string StringTablesFolder { get; private set; }
        
        [field: SerializeField]
        [field: Sirenix.OdinInspector.FilePath(Extensions = "csv")]
        [field: Required]
        public string LocalizationFile { get; private set; }
        
        [field: SerializeField]
        [field: Sirenix.OdinInspector.FilePath(Extensions = "asset")]
        [field: ValidateInput(nameof(ValidateStringTableCollectionPath))]
        public string StringTableCollectionsFile { get; private set; }
        
        [field: Tooltip("This symbol is used to separate rows into form:\n\n" +
                        "[ID]|[Language_1]|...|[Language_N]\n\n" +
                        "For example: Greetings|en-US:Hello|ru-RU:Привет")]
        [field: SerializeField]
        public string LocalizationSeparator { get; private set; } = "|";

        [field: Tooltip("This symbol is used to determine where to put localized string\n\n" +
                        "For example: en-US~Hello =>" +
                        "\n=> [en-US] and [Hello]\n=> [Hello] goes to [en-US] localization table")]
        [field: SerializeField] 
        public string LanguageSeparator { get; private set; } = "~";
        
        [SerializeField]
        [ReadOnly]
        [ShowIf(nameof(StringTablesIsNotEmpty))]
        private List<StringTable> _stringTables;

        [SerializeField]
        [ShowIf(nameof(StringTablesFolder))]
        private bool _addAllParsedEntriesToStringTables;

        private StringTableCollection _stringTableCollection;
        
        [Button]
        [ShowIf(nameof(StringTablesIsNotEmpty))]
        private void ClearStringTables()
        {
            Debug.Log("Clearing");
            _stringTableCollection.ClearAllEntries();
        }
        
        [ShowIf(nameof(AllDataSet))]
        [Button]
        public void UpdateLocalizationTables()
        {
            var localizationData = File.ReadAllText(LocalizationFile);

            var localizationEntries = localizationData.Split("\r\n");

            foreach (var entry in localizationEntries)
            {
                var splitEntries = entry.Split(LocalizationSeparator);

                var entryId = splitEntries[0];
                
                for (var i = 1; i < splitEntries.Length; i++)
                {
                    var localizedEntry = splitEntries[i].Split(LanguageSeparator);

                    var entryLanguage = localizedEntry[0];

                    var entryText = localizedEntry[1];

                    var stringTable = GetStringTable(entryLanguage);

                    if (!stringTable || !_addAllParsedEntriesToStringTables)
                    {
                        continue;
                    }

                    stringTable.AddEntry(entryId, entryText);
                }
            }
        }

        private StringTable GetStringTable(string language)
        {
            foreach (var stringTable in _stringTables)
            {
                if (stringTable.name.Split("_")[1] == language)
                {
                    return stringTable;
                }
            }

            return null;
        }

        private void LoadStringTables()
        {
            if (string.IsNullOrEmpty(StringTablesFolder))
            {
                _stringTables.Clear();
                
                return;
            }
            
            _stringTables = new List<StringTable>();

            var stringTables = Directory.GetFiles(StringTablesFolder);

            foreach (var stringTable in stringTables)
            {
                var loadedTable = AssetDatabase.LoadAssetAtPath<StringTable>(stringTable);

                if (!loadedTable)
                {
                    continue;
                }
                
                _stringTables.Add(loadedTable);
            }
        }
        
        private bool ValidateStringTableCollectionPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;  
            }
            
            _stringTableCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);

            return _stringTableCollection != null;
        }

        private bool StringTablesIsNotEmpty()
        {
            return _stringTables is { Count: > 0 };
        }

        private bool AllDataSet()
        {
            return StringTablesIsNotEmpty() && !string.IsNullOrEmpty(LocalizationFile);
        }
    }
}