using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Creobit.Localization
{
    public class SheetParser
    {
        private readonly string _tsvData;
        private const string LanguageColumnPattern = @"^[a-z]{2}-[A-Z]{2}$";
        private const string IdColumnName = "ID";

        public SheetParser(string tsvData)
        {
            _tsvData = tsvData;
        }

        public Dictionary<string, LocalizationJson> ParseLocalizations()
        {
            var localizations = new Dictionary<string, LocalizationJson>();
            string[] rows = _tsvData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] headers = rows[0].Split('\t');

            int idIndex = FindIdColumnIndex(headers);
            if (idIndex == -1)
            {
                EditorUtility.DisplayDialog("Error", $"No '{IdColumnName}' column found in the sheet!", "OK");
                return null;
            }

            var languageIndices = GetLanguageColumnIndices(headers, localizations);
            ProcessRows(rows, idIndex, languageIndices, localizations);

            return localizations;
        }

        private int FindIdColumnIndex(string[] headers)
        {
            return Array.FindIndex(headers, h => h.Equals(IdColumnName, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, int> GetLanguageColumnIndices(
            string[] headers,
            Dictionary<string, LocalizationJson> localizations)
        {
            var languageIndices = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                if (Regex.IsMatch(headers[i], LanguageColumnPattern))
                {
                    languageIndices[headers[i]] = i;
                    localizations[headers[i]] = new LocalizationJson { Data = new Dictionary<string, string>() };
                }
            }

            return languageIndices;
        }

        private void ProcessRows(
            string[] rows,
            int idIndex,
            Dictionary<string, int> languageIndices,
            Dictionary<string, LocalizationJson> localizations)
        {
            for (int rowIndex = 1; rowIndex < rows.Length; rowIndex++)
            {
                if (string.IsNullOrEmpty(rows[rowIndex]))
                    continue;

                string[] columns = rows[rowIndex].Split('\t');
                if (columns.Length <= idIndex)
                    continue;

                string id = columns[idIndex].Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                foreach (var langPair in languageIndices)
                {
                    if (columns.Length > langPair.Value)
                    {
                        localizations[langPair.Key].Data[id] = columns[langPair.Value];
                    }
                }
            }
        }
    }
}