using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Creobit.Localization
{
    public class SheetParserWindow : OdinEditorWindow
    {
        #region Constants
        private const string SheetIDKey = "Creobit_SheetParser_SheetId";
        private const string SheetNameKey = "Creobit_SheetParser_SheetName";
        private const string ProjectNameKey = "Creobit_SheetParser_ProjectName";
        private const string LanguageColumnPattern = "^[a-z]{2}-[A-Z]{2}$";
        private const string IdColumnName = "ID";


        [Serializable]
        private class SheetNamePathPair
        {
            public string SheetName;
            public string SheetPath;
        }
        
        #endregion

        #region Serialized Fields
        [Required]
        [SerializeField] 
        private ProjectType _projectType;
        
        [Required] [SerializeField] 
        [ShowIf("@_projectType == Creobit.Localization.ProjectType.Modules")]
        [OnValueChanged(nameof(SaveSheetId))]
        private string _sheetId;

        [Required] [SerializeField]
        [ShowIf("@_projectType == Creobit.Localization.ProjectType.Modules")]
        [OnValueChanged(nameof(SaveSheetName))] 
        private string _sheetName;

        [Required] [SerializeField]
        [OnValueChanged(nameof(SaveProjectName))] 
        private string _projectName;
        
        [Required] [SerializeField]
        [ShowIf("@_projectType == Creobit.Localization.ProjectType.Toyman")]
        private List<SheetNamePathPair> _sheetPaths;

        [ReadOnly] [ListDrawerSettings(ShowIndexLabels = true)] [SerializeField]
        [ShowIf("@_projectType == Creobit.Localization.ProjectType.Modules")]
        private List<string> _detectedLanguages = new();
        #endregion

        #region Editor Window Methods
        [MenuItem("Tools/Creobit/Localization/Sheet Parser")]
        private static void OpenWindow()
        {
            var window = GetWindow<SheetParserWindow>();
            window.LoadSavedValues();
            window.Show();
        }

        private void LoadSavedValues()
        {
            _sheetId = EditorPrefs.GetString(SheetIDKey, "");
            _sheetName = EditorPrefs.GetString(SheetNameKey, "");
            _projectName = EditorPrefs.GetString(ProjectNameKey, "");
        }

        [OnInspectorGUI]
        private void DrawInstructions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                _projectType switch
                {
                    ProjectType.Modules => "1. Make sure your Google Sheet has:\n" +
                                             $"   - An '{IdColumnName}' column\n" +
                                             "   - Language columns with format 'xx-XX' (e.g., 'en-US')\n" +
                                             "2. Click 'Detect Languages' to verify columns\n" +
                                             "3. Click 'Parse and Save' to generate JSON files for each language",
                    ProjectType.Toyman => "TODO: fill helpBox",
                    _ => throw new ArgumentOutOfRangeException()
                },
                MessageType.Info);
        }

        #endregion

        #region Preference Save Methods
        private void SaveSheetId() => EditorPrefs.SetString(SheetIDKey, _sheetId);
        private void SaveSheetName() => EditorPrefs.SetString(SheetNameKey, _sheetName);
        private void SaveProjectName() => EditorPrefs.SetString(ProjectNameKey, _projectName);
        #endregion

        #region Sheet Processing Methods
        [Button("Detect Languages")]
        [ShowIf("@_projectType == Creobit.Localization.ProjectType.Modules")]
        private async UniTaskVoid DetectLanguages()
        {
            if (!ValidateBasicInput())
                return;

            var sheetData = await FetchSheetData();
            if (sheetData == null)
                return;

            _detectedLanguages = sheetData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0]
                .Split('\t')
                .Where(h => Regex.IsMatch(h, LanguageColumnPattern))
                .ToList();

            if (_detectedLanguages.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "No language columns detected! Expected format: 'xx-XX'", "OK");
            }
        }

        [Button("Parse and Save")]
        private async UniTaskVoid ParseAndSave()
        {
            if (!ValidateFullInput())
                return;

            var localizations = _projectType switch
            {
                ProjectType.Modules => await ParseGoogleTable(),
                ProjectType.Toyman => await ParseToyman(),
                _ => throw new ArgumentOutOfRangeException()
            };

            await SaveLocalizationFiles(localizations);
        }

        private async UniTask<Dictionary<string, LocalizationJson>> ParseGoogleTable()
        {
            var sheetData = await FetchSheetData();
            
            if (sheetData == null)
                return null;

            var parser = new SheetParser(sheetData);
            var localizations = parser.ParseLocalizations();

            return localizations;
        }

        private async UniTask<Dictionary<string, LocalizationJson>> ParseToyman()
        {
            var localizations = new Dictionary<string, LocalizationJson>();
            foreach (var sheet in _sheetPaths)
            {
                var localizationStrings = 
                    File.ReadAllText(Path.Combine(Application.streamingAssetsPath, sheet.SheetPath));
                string stringKey;
                string stringValue;
                int index;
                var localization = new Dictionary<string, string>();

                while (localizationStrings.Length > 0)
                {
                    index = localizationStrings.IndexOf("\n", StringComparison.Ordinal);

                    // get strings pair {key,value}
                    if (index != -1)
                    {
                        stringValue = localizationStrings.Substring(0, index);
                        localizationStrings = localizationStrings.Remove(0, index + 1);
                    }
                    else
                    {
                        stringValue = localizationStrings;
                        localizationStrings = localizationStrings.Remove(0);
                    }

                    // parse strings pair
                    index = stringValue.IndexOf(",", StringComparison.Ordinal);

                    if (index != -1 && index != 0)
                    {
                        stringKey = stringValue[..index];
                        stringKey = stringKey.Replace("@", string.Empty);

                        stringValue = stringValue.Remove(0, index + 1);

                        // delete symbol \r
                        index = stringValue.IndexOf("\r", StringComparison.Ordinal);

                        if (index != -1)
                        {
                            stringValue = stringValue.Remove(index, 1);
                        }

                        // delete symbol \"
                        index = stringValue.IndexOf("\"", StringComparison.Ordinal);

                        if (index != -1)
                        {
                            stringValue = stringValue.Remove(0, index + 1);
                            stringValue = stringValue.Remove(stringValue.Length - 1);
                        }

                        // replace \"\" to \"
                        stringValue = stringValue.Replace("\"\"", "\"");

                        // replace <br> to \n
                        stringValue = stringValue.Replace("<br>", "\n");

                        if (stringValue.StartsWith("@"))
                        {
                            if (stringValue.EndsWith("@"))
                            {
                                stringValue = stringValue.Replace("@", string.Empty);
                            }
                            else
                            {
                                index = localizationStrings.IndexOf("@", StringComparison.Ordinal);
                                if (index != -1)
                                {
                                    stringValue += "\n" + localizationStrings.Substring(0, index);
                                }

                                stringValue = stringValue.Replace("@", string.Empty);
                            }
                        }

                        //16042020
                        // add gradient
                        var gradientCount = 0;
                        var stringBuffer = stringValue;

                        index = stringValue.IndexOf("<gradient>", StringComparison.Ordinal);

                        if (index != -1)
                        {
                            gradientCount = 1;
                            stringValue = stringValue.Remove(index, 10);

                            // calc amount of lines
                            stringBuffer = stringValue;
                            while ((index = stringBuffer.IndexOf("\n", StringComparison.Ordinal)) != -1)
                            {
                                ++gradientCount;
                                stringBuffer = stringBuffer.Remove(0, index + 1);
                            }

                            // add ^N
                            int gradientIndex = gradientCount;
                            if (gradientCount > 2)
                                gradientIndex = 4;

                            stringBuffer = stringValue;
                            stringValue = "";
                            while ((index = stringBuffer.IndexOf("\n", StringComparison.Ordinal)) != -1)
                            {
                                stringValue += "^" + gradientIndex;
                                stringValue += stringBuffer.Substring(0, index + 1);
                                stringBuffer = stringBuffer.Remove(0, index + 1);
                                ++gradientIndex;
                            }

                            stringValue += "^" + gradientIndex + stringBuffer;
                        }


                        // replace <colorN> to ^N
                        stringValue = stringValue.Replace("<color0>", "^0");
                        stringValue = stringValue.Replace("<color1>", "^1");
                        stringValue = stringValue.Replace("<color2>", "^2");
                        stringValue = stringValue.Replace("<color3>", "^3");
                        stringValue = stringValue.Replace("<color4>", "^4");
                        stringValue = stringValue.Replace("<color5>", "^5");
                        stringValue = stringValue.Replace("<color6>", "^6");
                        stringValue = stringValue.Replace("<color7>", "^7");
                        stringValue = stringValue.Replace("<color8>", "^8");


                        // add {key, value} to dictionary
                        localization.TryAdd(stringKey, stringValue);
                    }
                }
                
                localizations[sheet.SheetName] = new LocalizationJson
                {
                    Data = localization,
                };
            }

            return localizations;
        }

        private bool ValidateBasicInput()
        {
            if ((string.IsNullOrEmpty(_sheetId) || string.IsNullOrEmpty(_sheetName)))
            {
                EditorUtility.DisplayDialog("Error", "Please fill in Sheet ID and Sheet Name!", "OK");
                return false;
            }
            return true;
        }

        private bool ValidateFullInput()
        {
            if (_projectType == ProjectType.Modules 
                && (!ValidateBasicInput() || string.IsNullOrEmpty(_projectName)))
            {
                EditorUtility.DisplayDialog("Error", "Please fill in all required fields!", "OK");
                return false;
            }
            return true;
        }

        private async Task<string> FetchSheetData()
        {
            IGoogleSheetService googleSheetService = new GoogleSheetUnityWebRequestService();
            string tsvData = await googleSheetService.Get(_sheetId, _sheetName);

            if (string.IsNullOrEmpty(tsvData))
            {
                EditorUtility.DisplayDialog("Error", "Failed to fetch sheet data!", "OK");
                return null;
            }

            return tsvData;
        }

        private async Task SaveLocalizationFiles(Dictionary<string, LocalizationJson> localizations)
        {
            string directoryPath = Path.Combine(Application.streamingAssetsPath, _projectName);
            Directory.CreateDirectory(directoryPath);

            try
            {
                foreach (var localization in localizations)
                {
                    string filePath = Path.Combine(directoryPath, $"{_sheetName}_{localization.Key}.json");
                    string jsonContent = JsonConvert.SerializeObject(localization.Value, Formatting.Indented);
                    await File.WriteAllTextAsync(filePath, jsonContent).AsUniTask();
                    Log.Bootstrap.Info($"Successfully saved {localization.Key} to: {filePath}");
                }

                EditorUtility.DisplayDialog("Success", 
                    $"Saved {localizations.Count} language files to {directoryPath}", "OK");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Log.Bootstrap.Error($"Error saving files: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to save files: {ex.Message}", "OK");
            }
        }
        #endregion
    }
}