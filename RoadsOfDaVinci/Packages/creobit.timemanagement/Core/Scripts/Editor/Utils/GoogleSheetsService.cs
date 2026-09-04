using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Google
{
    public static class GoogleSheetsService
    {
        public static IList<IList<object>> ReadFromGoogleSheets(string tableId, string sheetName)
        {
            var secretKeyPath = GetSecretKeyPath();
            var sheetsService = CreateSheetsService(secretKeyPath, ParserData.AppName);
            var request = sheetsService.Spreadsheets.Values.Get(tableId, sheetName);
            return request.Execute().Values;
        }

        private static string GetSecretKeyPath()
        {
            if (!Directory.Exists(ParserData.SecretKeyFolderPath))
            {
                throw new DirectoryNotFoundException(
                    $"The folder '{ParserData.SecretKeyFolderPath}' does not exist.");
            }

            var files = Directory.GetFiles(ParserData.SecretKeyFolderPath);
            if (files.Length < 1)
            {
                throw new FileNotFoundException(
                    $"The folder '{ParserData.SecretKeyFolderPath}' must contain at least 1 file.");
            }

            return files.FirstOrDefault(file => file.EndsWith(".secret.json")) 
                   ?? throw new FileNotFoundException("No file with '.secret.json' extension found.");
        }

        private static SheetsService CreateSheetsService(string secretKeyPath, string appName)
        {
            var googleCredential = GoogleCredential.FromFile(secretKeyPath)
                .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);

            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = googleCredential,
                ApplicationName = appName
            });
        }
    }
    
    public static class ParserData
    {
        public const string ParserConfigFolderPath = "Assets/GoogleSheets/ParsedConfigs/";
        public const string SecretKeyFolderPath = "Assets/GoogleSheets/";

        public const string AppName = "GoogleTableParse";
    }
}