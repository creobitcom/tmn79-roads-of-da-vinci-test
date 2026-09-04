using System.IO;
using Creobit.Dialogues.Core.Scripts.Runtime.Utility;

namespace Creobit.Dialogues.Core.Scripts.Editor.Utils
{
    public static class DialoguesEditorUtils
    {
        public static bool ConfigExists()
        {
            return File.Exists(Path.Combine("Assets/GoogleSheets/ParsedConfigs/", RuntimeConstants.ParserData.ParserConfigFilePath));
        }
    }
}