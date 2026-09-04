using Cysharp.Threading.Tasks;

namespace Creobit.Localization
{
    public interface IGoogleSheetService
    {
        public UniTask<string> Get(string sheetID, string sheetName);
    }
}