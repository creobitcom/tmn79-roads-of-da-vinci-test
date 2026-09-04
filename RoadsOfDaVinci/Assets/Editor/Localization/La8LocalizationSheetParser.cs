using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Creobit.LA8.EditorTools.Localization
{
    internal static class La8LocalizationSheetParser
    {
        private const string LanguageColumnPattern = @"^[a-z]{2}-[A-Z]{2}$";
        private const string IdColumnName = "ID";

        internal static Dictionary<string, Dictionary<string, string>> Parse(string sheetData, bool isCsv)
        {
            if (string.IsNullOrWhiteSpace(sheetData))
                return null;

            var rows = ParseRows(sheetData, isCsv);
            if (rows.Count == 0)
                return null;

            string[] headers = rows[0];
            int idIndex = Array.FindIndex(headers, h => h.Trim().Equals(IdColumnName, StringComparison.OrdinalIgnoreCase));
            if (idIndex < 0)
                return null;

            var result = new Dictionary<string, Dictionary<string, string>>();
            var languageIndices = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim();
                if (Regex.IsMatch(header, LanguageColumnPattern))
                {
                    languageIndices[header] = i;
                    result[header] = new Dictionary<string, string>();
                }
            }

            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                string[] columns = rows[rowIndex];
                if (columns.Length <= idIndex)
                    continue;

                string id = columns[idIndex].Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                foreach (var pair in languageIndices)
                {
                    if (columns.Length > pair.Value)
                        result[pair.Key][id] = columns[pair.Value];
                }
            }

            return result;
        }

        internal static List<string> DetectLanguages(string sheetData, bool isCsv)
        {
            var languages = new List<string>();
            if (string.IsNullOrWhiteSpace(sheetData))
                return languages;

            var rows = ParseRows(sheetData, isCsv);
            if (rows.Count == 0)
                return languages;

            foreach (string header in rows[0])
            {
                string trimmed = header.Trim();
                if (Regex.IsMatch(trimmed, LanguageColumnPattern))
                    languages.Add(trimmed);
            }

            return languages;
        }

        internal static bool DetectIsCsv(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            if (string.Equals(extension, ".tsv", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static List<string[]> ParseRows(string sheetData, bool isCsv)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrWhiteSpace(sheetData))
                return rows;

            char delimiter = isCsv ? ',' : '\t';
            var currentRow = new List<string>();
            var currentColumn = new StringBuilder();
            bool inQuotes = false;
            int length = sheetData.Length;

            for (int i = 0; i < length; i++)
            {
                char c = sheetData[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < length && sheetData[i + 1] == '"')
                    {
                        currentColumn.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    currentRow.Add(currentColumn.ToString());
                    currentColumn.Clear();
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < length && sheetData[i + 1] == '\n')
                    {
                        i++;
                    }

                    currentRow.Add(currentColumn.ToString());
                    currentColumn.Clear();

                    if (currentRow.Count > 1 || (currentRow.Count == 1 && !string.IsNullOrEmpty(currentRow[0])))
                    {
                        rows.Add(currentRow.ToArray());
                    }
                    currentRow.Clear();
                }
                else
                {
                    currentColumn.Append(c);
                }
            }

            if (currentColumn.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentColumn.ToString());
                if (currentRow.Count > 1 || (currentRow.Count == 1 && !string.IsNullOrEmpty(currentRow[0])))
                {
                    rows.Add(currentRow.ToArray());
                }
            }

            return rows;
        }
    }
}
