using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace Creobit.Localization
{
    public class GoogleSheetUnityWebRequestService : IGoogleSheetService
    {
        public async UniTask<string> Get(string sheetID, string sheetName)
        {
            // Format: https://docs.google.com/spreadsheets/d/{sheetId}/gviz/tq?tqx=out:csv&sheet={sheetName}
            string url = $"https://docs.google.com/spreadsheets/d/{sheetID}/export?format=tsv&sheet={sheetName}";

            using UnityWebRequest www = UnityWebRequest.Get(url);
            var operation = www.SendWebRequest();
            while (!operation.isDone)
                await UniTask.Yield();

            if (www.result == UnityWebRequest.Result.Success)
            {
                return www.downloadHandler.text;
            }
            else
            {
                Log.Bootstrap.Error($"Error: {www.error}");
                return null;
            }
        }
    }
}
