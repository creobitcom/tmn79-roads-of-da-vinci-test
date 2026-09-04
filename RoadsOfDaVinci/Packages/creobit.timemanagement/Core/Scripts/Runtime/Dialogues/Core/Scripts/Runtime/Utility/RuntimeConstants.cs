using System.Collections.Generic;

namespace Creobit.Dialogues.Core.Scripts.Runtime.Utility
{
    public static class RuntimeConstants
    {
        public static List<string> CharacterStates { get; set; } = new();
        public static List<string> CharacterNames { get; set; } = new();

        public static class MenuItems
        {
            private const string MenuName = "Tools/Creobit";
            private const string ModuleName = "Dialogue";
            private const string ToolsPath = MenuName + "/" + ModuleName + "/";

            public const string DialogueParserTool = ToolsPath + "Dialogue Parser Tool";
            public const string CharacterDatabaseParserTool = ToolsPath + "Character Database Parser Tool";
        }

        public static class ParserData
        {
            public const string ParserConfigFilePath = "ParserConfig.json";
            public const string DialogueJsonFilePath = @"Dialogues/Core/DialoguesJson/Story.json";

            public const string AppName = "GoogleTableParse";
            public const string SheetName = "Story"; // TODO : move to editor choice
        }
        
        public static class LocalizationParserData
        {
            public const string SheetName = "GameText";
            public const string DefaultLanguage = "en-US";
        }
        
        public static class GameSettingsPrefs
        {
            public const string Settings = "GameSettings";
            public const string Language = "Language";
        }
    }
}