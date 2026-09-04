using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Creobit.LA8.EditorTools.Localization
{
    public class La8LocalizationCsvImporterWindow : EditorWindow
    {
        const string PrefsFilePath = "Creobit.LA8.LocalizationCsv.FilePath";
        const string DefaultProjectName = "RoadsOfDaVinci";
        const string DefaultOutputPrefix = "GameText";

        string _filePath;
        string _projectName = DefaultProjectName;
        string _outputPrefix = DefaultOutputPrefix;
        readonly List<string> _detectedLanguages = new List<string>();
        Vector2 _scroll;
        string _status;

        [MenuItem("Tools/LA8/Localization CSV Importer")]
        public static void Open()
        {
            var window = GetWindow<La8LocalizationCsvImporterWindow>("LA8 Localization CSV");
            window.minSize = new Vector2(560, 360);
            window.Show();
        }

        void OnEnable()
        {
            _filePath = EditorPrefs.GetString(PrefsFilePath, string.Empty);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Импорт локализации из CSV/TSV", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Файл как Google Sheet: колонка ID + языки xx-XX (en-US, ru-RU...).\n" +
                $"Пишет в StreamingAssets/{DefaultProjectName}/{DefaultOutputPrefix}_{{lang}}.json",
                MessageType.Info);

            EditorGUILayout.Space(8);
            _projectName = EditorGUILayout.TextField("Project Name", _projectName);
            _outputPrefix = EditorGUILayout.TextField("Output Prefix", _outputPrefix);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("CSV/TSV File", _filePath);
            if (GUILayout.Button("...", GUILayout.Width(32)))
                ChooseFile();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Detect Languages", GUILayout.Height(28)))
                DetectLanguages();

            if (GUILayout.Button("Parse and Save", GUILayout.Height(28)))
                ParseAndSave();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Detected languages", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120));
            if (_detectedLanguages.Count == 0)
            {
                EditorGUILayout.LabelField("—");
            }
            else
            {
                foreach (string language in _detectedLanguages)
                    EditorGUILayout.LabelField(language);
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        void ChooseFile()
        {
            string startDir = string.IsNullOrEmpty(_filePath)
                ? Application.dataPath
                : Path.GetDirectoryName(_filePath);

            string path = EditorUtility.OpenFilePanel("Select localization CSV/TSV", startDir, "csv,tsv,txt");
            if (string.IsNullOrEmpty(path))
                return;

            _filePath = path;
            EditorPrefs.SetString(PrefsFilePath, _filePath);
            _detectedLanguages.Clear();
            _status = string.Empty;
        }

        void DetectLanguages()
        {
            if (!TryReadSheet(out string sheetData, out bool isCsv))
                return;

            _detectedLanguages.Clear();
            _detectedLanguages.AddRange(La8LocalizationSheetParser.DetectLanguages(sheetData, isCsv));
            _status = _detectedLanguages.Count == 0
                ? "Языковые колонки не найдены. Нужен формат xx-XX."
                : $"Найдено языков: {_detectedLanguages.Count}";
        }

        void ParseAndSave()
        {
            if (string.IsNullOrWhiteSpace(_projectName) || string.IsNullOrWhiteSpace(_outputPrefix))
            {
                EditorUtility.DisplayDialog("Error", "Заполни Project Name и Output Prefix.", "OK");
                return;
            }

            if (!TryReadSheet(out string sheetData, out bool isCsv))
                return;

            var localizations = La8LocalizationSheetParser.Parse(sheetData, isCsv);
            if (localizations == null || localizations.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Не удалось распарсить файл. Проверь колонку ID и языки xx-XX.", "OK");
                return;
            }

            string directoryPath = Path.Combine(Application.streamingAssetsPath, _projectName.Trim());
            Directory.CreateDirectory(directoryPath);

            foreach (var localization in localizations)
            {
                string filePath = Path.Combine(directoryPath, $"{_outputPrefix.Trim()}_{localization.Key}.json");
                La8LocalizationJsonWriter.Write(filePath, localization.Value);
            }

            AssetDatabase.Refresh();
            _detectedLanguages.Clear();
            _detectedLanguages.AddRange(localizations.Keys);
            _status = $"Сохранено {localizations.Count} файлов в {directoryPath}";
            EditorUtility.DisplayDialog("Success", _status, "OK");
        }

        bool TryReadSheet(out string sheetData, out bool isCsv)
        {
            sheetData = null;
            isCsv = true;

            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                EditorUtility.DisplayDialog("Error", "Выбери существующий CSV/TSV файл.", "OK");
                return false;
            }

            sheetData = File.ReadAllText(_filePath);
            if (!string.IsNullOrEmpty(sheetData) && sheetData[0] == '\uFEFF')
                sheetData = sheetData[1..];

            isCsv = La8LocalizationSheetParser.DetectIsCsv(_filePath);
            return true;
        }
    }
}
