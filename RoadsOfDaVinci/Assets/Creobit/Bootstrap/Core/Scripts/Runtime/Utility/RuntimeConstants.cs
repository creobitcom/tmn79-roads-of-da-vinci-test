using System.Collections.Generic;
using System.IO;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility
{
    // TODO : Runtime constants should have only constants what needed in different services
    public static class RuntimeConstants
    {
        public static readonly Dictionary<SceneType, string> SceneNames = new()
        {
            { SceneType.Bootstrap, "Bootstrap" },
            { SceneType.Meta, "Meta" },
            { SceneType.Gameplay , "Gameplay" },
            { SceneType.TmnSelect , "TMNSelect" },
        };

        public static class FoldoutGroups
        {
            public const string SceneReferencesGroup = "Scene References";
        }

        public static class SceneIndex
        {
            public const int BootstrapScene = 0;
            public const int MetaScene = 1;
            public const int GameplayScene = 2;
            public const int TmnSelect = 3;
        }

        public static class ParserData
        {
            public const string ParserConfigFolderPath = "Assets/GoogleSheets/ParsedConfigs/";
            public const string SecretKeyFolderPath = "Assets/GoogleSheets/";

            public const string AppName = "GoogleTableParse";
        }

        public static class LocalizationParserData
        {
            public const string SheetName = "GameText";
            public const string DefaultLanguage = "en-US";

            public static readonly string[] SupportedLanguages =
            {
                "de-DE", "en-US", "es-ES", "fr-FR", "it-IT", "nl-NL", "pl-PL", "pt-BR", "ru-RU"
            };
        }

        public static class Paths
        {
            private const string ModulesDataFileName = "ModulesData.json";

            public const string PathToBootstrapScene = "Assets/Creobit/Bootstrap/Core/Scenes/Bootstrap.unity";

            public const string PathToLocalizationSettingsFolder =
                "Assets/Creobit/Bootstrap/Core/Settings/Localization/";

            public const string PathToLocalizationSettingsFile =
                "Assets/Creobit/Bootstrap/Core/Settings/Localization/LocalizationServiceData.json";

            public const string PathToModulesSettingsFolder = "Assets/Creobit/Bootstrap/Core/Settings/Modules/";

            public const string PathToBootstrapScope = "Assets/Creobit/Bootstrap/Core/Prefabs/BootstrapScope.prefab";
            public const string PathToMetaScope = "Assets/Creobit/Bootstrap/Core/Prefabs/MetaScope.prefab";
            public const string PathToGameplayScope = "Assets/Creobit/Bootstrap/Core/Prefabs/GameplayScope.prefab";

            public static string PathToModulesDataSettingsFile(string sceneName)
            {
                return $"{Path.Combine(PathToModulesSettingsFolder, sceneName + "_" + ModulesDataFileName)}";
            }
        }

        public static class MenuItems
        {
            private const string MenuName = "Tools/Creobit";
            private const string ModuleName = "Bootstrap";
            private const string ToolsPath = MenuName + "/" + ModuleName + "/";

            public const string LocalizationToolWindow = ToolsPath + "Localization Tool";
            public const string LogToolWindow = ToolsPath + "Log Tool";
            public const string ModulesToolWindow = ToolsPath + "Modules Tool";
            public const string AssetsSaverToolWindow = ToolsPath + "Assets Saver Tool";
        }

        public static class TabGroups
        {
            public const string LocalizationGeneralSettingsGroup = "Localization General Settings";
            public const string LocalizationImportGroup = "Localization Importer";
        }

        public static class GameSettingsPrefs
        {
            public const string Settings = "GameSettings";
            public const string Language = "Language";
        }

        public static class PlayerProfilesPrefs
        {
            public const string Profiles = "PlayerProfiles";
            public const string FirstProfile = "FirstProfile";
            public const string FirstDefaultProfileName = "NewUser";
            public const string MobileDefaultProfileName = "Player";
        }

        public static class PlayerProfilesRules
        {
            public const int MaxProfilesAmount = 5;
            public const string NamePattern = @"^[\p{L}0-9]+$";
        }

        public static class SaveItems
        {
            public const string CurrentLevel = "CurrentLevel";
        }
    }
}