using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using EditorUserBuildSettings = UnityEditor.EditorUserBuildSettings;

namespace EditionsUpgrade.Editor
{
    [InitializeOnLoad]
    public static class EditionsUpgradeEditor
    {
        private const string Tk2DMenuName = "8floor/EditionsUpgrade/TK2D";
        private const string DefinesCheckMenuName = "8floor/EditionsUpgrade/DefinesCheck";

#region EditionVariables
        
        // Prototype
        // private const string EditionMenuName = "8floor/EditionsUpgrade/Edition/edition_name";

        private const string EditionMenuName8FloorWindowsPremiumCe = "8floor/EditionsUpgrade/Edition/8floor_windows_premium_ce";
        private const string EditionMenuName8FloorWindowsPremiumSe = "8floor/EditionsUpgrade/Edition/8floor_windows_premium_se";
        private const string EditionMenuName8FloorMacOSPremiumCe = "8floor/EditionsUpgrade/Edition/8floor_macos_premium_ce";
        private const string EditionMenuName8FloorMacOSPremiumSe = "8floor/EditionsUpgrade/Edition/8floor_macos_premium_se";
        private const string EditionMenuNameSteamWindowsPremium = "8floor/EditionsUpgrade/Edition/steam_windows_premium";
        private const string EditionMenuNameSteamMacOSPremium = "8floor/EditionsUpgrade/Edition/steam_macos_premium";
        private const string EditionMenuNameMicrosoftWindowsPremium = "8floor/EditionsUpgrade/Edition/microsoft_windows_premium";
        private const string EditionMenuNameMicrosoftWindowsF2P = "8floor/EditionsUpgrade/Edition/microsoft_windows_f2p";
        private const string EditionMenuNameBfgWindowsPremiumCe = "8floor/EditionsUpgrade/Edition/bfg_windows_premium_ce";
        private const string EditionMenuNameBfgWindowsPremiumSe = "8floor/EditionsUpgrade/Edition/bfg_windows_premium_se";
        private const string EditionMenuNameBfgMacOSPremiumCe = "8floor/EditionsUpgrade/Edition/bfg_macos_premium_ce";
        private const string EditionMenuNameBfgMacOSPremiumSe = "8floor/EditionsUpgrade/Edition/bfg_macos_premium_se";
        private const string EditionMenuName8FloorAndroidPremiumCe = "8floor/EditionsUpgrade/Edition/8floor_android_premium_ce";
        private const string EditionMenuNameGooglePlayAndroidF2P = "8floor/EditionsUpgrade/Edition/googleplay_android_f2p";
        private const string EditionMenuNameGooglePlayAndroidPremium = "8floor/EditionsUpgrade/Edition/googleplay_android_premium";
        private const string EditionMenuNameAppstoreIosPremium = "8floor/EditionsUpgrade/Edition/appstore_ios_premium";
        private const string EditionMenuNameAppstoreIosF2P = "8floor/EditionsUpgrade/Edition/appstore_ios_f2p";
        private const string EditionMenuNameAppstoreMacosPremium = "8floor/EditionsUpgrade/Edition/appstore_macos_premium";
        private const string EditionMenuNameAppstoreMacosF2P = "8floor/EditionsUpgrade/Edition/appstore_macos_f2p";
        private const string EditionMenuNameEgsWindowsPremium = "8floor/EditionsUpgrade/Edition/egs_windows_premium";
        private const string EditionMenuNameHuaweiAndroidF2P = "8floor/EditionsUpgrade/Edition/huawei_android_f2p";
        private const string EditionMenuNameHuaweiAndroidPremium = "8floor/EditionsUpgrade/Edition/huawei_android_premium";
        
        // Prototype
        // private static bool _enabledEditionMenuName;
        
        private static bool _enabledTk2D;
        private static bool _enabled8FloorWindowsPremiumCe;
        private static bool _enabled8FloorMacOSPremiumCe;
        private static bool _enabled8FloorWindowsPremiumSe;
        private static bool _enabled8FloorMacOSPremiumSe;
        private static bool _enabledSteamWindowsPremium;
        private static bool _enabledSteamMacOSPremium;
        private static bool _enabledMicrosoftWindowsPremium;
        private static bool _enabledMicrosoftWindowsF2P;
        private static bool _enabledBfgWindowsPremiumCe;
        private static bool _enabledBfgMacOSPremiumCe;
        private static bool _enabledBfgWindowsPremiumSe;
        private static bool _enabledBfgMacOSPremiumSe;
        private static bool _enabledAndroidPremiumCeBuild;
        private static bool _enabledGooglePlayAndroidF2PBuild;
        private static bool _enabledGooglePlayAndroidPremiumBuild;
        private static bool _enabledAppstoreIosPremiumBuild;
        private static bool _enabledAppstoreIosF2P;
        private static bool _enabledAppstoreMacosPremiumBuild;
        private static bool _enabledAppstoreMacosF2P;
        private static bool _enabledEgsWindowsPremium;
        private static bool _enabledHuaweiAndroidF2P;
        private static bool _enabledHuaweiAndroidPremium;

#endregion

        private static readonly string[] AllEditionMenuNames =
        {
            EditionMenuName8FloorWindowsPremiumCe,
            EditionMenuName8FloorWindowsPremiumSe,
            EditionMenuName8FloorMacOSPremiumCe,
            EditionMenuName8FloorMacOSPremiumSe,
            EditionMenuNameSteamWindowsPremium,
            EditionMenuNameSteamMacOSPremium,
            EditionMenuNameMicrosoftWindowsPremium,
            EditionMenuNameMicrosoftWindowsF2P,
            EditionMenuNameBfgWindowsPremiumCe,
            EditionMenuNameBfgWindowsPremiumSe,
            EditionMenuNameBfgMacOSPremiumCe,
            EditionMenuNameBfgMacOSPremiumSe,
            EditionMenuName8FloorAndroidPremiumCe,
            EditionMenuNameGooglePlayAndroidF2P,
            EditionMenuNameGooglePlayAndroidPremium,
            EditionMenuNameAppstoreIosPremium,
            EditionMenuNameAppstoreIosF2P,
            EditionMenuNameAppstoreMacosPremium,
            EditionMenuNameAppstoreMacosF2P,
            EditionMenuNameEgsWindowsPremium,
            EditionMenuNameHuaweiAndroidF2P,
            EditionMenuNameHuaweiAndroidPremium
        };

        private static string _allDefines;
        private static string _otherDefines;

#region Defines
        
        // Prototype
        // private static readonly string NameDefine = "DEFINE_NAME";
        
        private static readonly string Tk2DDefine = "TK2D";
        private static readonly string UdpDefine = "UDP";
        private static readonly string GooglePlayDefine = "GOOGLE_PLAY";
        private static readonly string PremiumDefine = "PREMIUM";
        private static readonly string CollectorDefine = "COLLECTOR";
        private static readonly string HasAdDefine = "HAS_AD";
        private static readonly string UnityAnalyticsDefine = "UNITY_ANALYTICS";
        private static readonly string MacAppstoreDefine = "MAC_APPSTORE";
        private static readonly string HuaweiDefine = "HUAWEI";
        private static readonly string SteamBuildDefine = "STEAM_BUILD";
        private static readonly string UpgradeDefine = "UPGRADE";

#endregion

        public static bool WriteDefinesToPlayerSettings = true;

        public static bool SuppressBuildTargetSwitch;

        public static string ComputedDefines => _allDefines;

        public static void ApplyEdition(string editionName)
        {
            switch (NormalizeEditionName(editionName))
            {
                case "8floor_windows_premium_ce":
                    WindowsPremiumToggle();
                    break;
                case "8floor_windows_premium_se":
                    WindowsPremiumSeToggle();
                    break;
                case "8floor_macos_premium_ce":
                    MacOSPremiumToggle();
                    break;
                case "8floor_macos_premium_se":
                    MacOSPremiumSeToggle();
                    break;
                case "steam_windows_premium":
                    SteamWindowsPremiumToggle();
                    break;
                case "googleplay_android_f2p":
                    AndroidF2PToggle();
                    break;
                case "googleplay_android_premium":
                    GooglePlayAndroidPremiumToggle();
                    break;
                case "appstore_ios_premium":
                    AppstoreIosPremiumToggle();
                    break;
                case "appstore_ios_f2p":
                    AppstoreIosF2PToggle();
                    break;
                case "appstore_macos_premium":
                    AppstoreMacosPremiumToggle();
                    break;
                case "appstore_macos_f2p":
                    AppstoreMacosF2PToggle();
                    break;
                default:
                    throw new System.ArgumentException($"Unknown edition: {editionName}", nameof(editionName));
            }
        }

        private static string NormalizeEditionName(string editionName)
        {
            if (string.IsNullOrWhiteSpace(editionName))
                return string.Empty;

            editionName = editionName.Trim().Replace('\\', '/');
            var slashIndex = editionName.LastIndexOf('/');
            if (slashIndex >= 0)
                editionName = editionName.Substring(slashIndex + 1);

            return editionName.Trim().ToLowerInvariant();
        }

        private static void CommitDefines(NamedBuildTarget namedTarget)
        {
            if (WriteDefinesToPlayerSettings)
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, _allDefines);

            Creobit.LA8.Build.EditionArtifacts.Apply(
                namedTarget,
                Creobit.LA8.Build.EditionArtifacts.UsesCollectorsLogo(_allDefines),
                Creobit.LA8.Build.EditionArtifacts.UsesCollectorsIcon(_allDefines));

            SaveProjectSettings();

            Debug.Log($"[EditionsUpgrade] Издание применено: {namedTarget.TargetName} -> {_allDefines}");
        }

        private static void SaveProjectSettings()
        {
            foreach (var playerSettings in Resources.FindObjectsOfTypeAll<PlayerSettings>())
            {
                EditorUtility.SetDirty(playerSettings);
            }

            AssetDatabase.SaveAssets();
        }

        private static void SwitchBuildTarget(BuildTarget target)
        {
            if (SuppressBuildTargetSwitch || EditorUserBuildSettings.activeBuildTarget == target)
                return;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
        }
        
        static EditionsUpgradeEditor() {

            _enabledTk2D = EditorPrefs.GetBool(Tk2DMenuName, false);
            RestoreCheckedEdition();
            _allDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings
                .selectedBuildTargetGroup);
        }

        private static void RestoreCheckedEdition()
        {
            _enabled8FloorWindowsPremiumCe = EditorPrefs.GetBool(EditionMenuName8FloorWindowsPremiumCe, false);
            _enabled8FloorWindowsPremiumSe = EditorPrefs.GetBool(EditionMenuName8FloorWindowsPremiumSe, false);
            _enabled8FloorMacOSPremiumCe = EditorPrefs.GetBool(EditionMenuName8FloorMacOSPremiumCe, false);
            _enabled8FloorMacOSPremiumSe = EditorPrefs.GetBool(EditionMenuName8FloorMacOSPremiumSe, false);
            _enabledSteamWindowsPremium = EditorPrefs.GetBool(EditionMenuNameSteamWindowsPremium, false);
            _enabledGooglePlayAndroidF2PBuild = EditorPrefs.GetBool(EditionMenuNameGooglePlayAndroidF2P, false);
            _enabledGooglePlayAndroidPremiumBuild = EditorPrefs.GetBool(EditionMenuNameGooglePlayAndroidPremium, false);
            _enabledAppstoreIosPremiumBuild = EditorPrefs.GetBool(EditionMenuNameAppstoreIosPremium, false);
            _enabledAppstoreIosF2P = EditorPrefs.GetBool(EditionMenuNameAppstoreIosF2P, false);
            _enabledAppstoreMacosPremiumBuild = EditorPrefs.GetBool(EditionMenuNameAppstoreMacosPremium, false);
            _enabledAppstoreMacosF2P = EditorPrefs.GetBool(EditionMenuNameAppstoreMacosF2P, false);
        }

#region MenuItems
        
        [MenuItem(Tk2DMenuName)]
        private static void Tk2Toggle()
        {
            _enabledTk2D = !_enabledTk2D;
            EditorPrefs.SetBool(Tk2DMenuName, _enabledTk2D);
            SetTk2DDefine(_enabledTk2D);
        }

        [MenuItem(Tk2DMenuName, true)]
        private static bool Tk2DToggleValidate()
        {
            Menu.SetChecked(Tk2DMenuName ,_enabledTk2D);
            Debug.Log(_enabledTk2D);
            return true;
        }
        
        [MenuItem(EditionMenuName8FloorWindowsPremiumCe)]
        private static void WindowsPremiumToggle()
        {
            DisableAllEditions();
            _enabled8FloorWindowsPremiumCe = true;
            EditorPrefs.SetBool(EditionMenuName8FloorWindowsPremiumCe, _enabled8FloorWindowsPremiumCe);
            Enable8FloorWindowsPremiumCeDefines(true);
        }

        [MenuItem(EditionMenuName8FloorWindowsPremiumCe, true)]
        private static bool WindowsPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuName8FloorWindowsPremiumCe, _enabled8FloorWindowsPremiumCe);
            return true;
        }
        
        [MenuItem(EditionMenuNameSteamWindowsPremium)]
        private static void SteamWindowsPremiumToggle()
        {
            DisableAllEditions();
            _enabledSteamWindowsPremium = true;
            EditorPrefs.SetBool(EditionMenuNameSteamWindowsPremium, _enabledSteamWindowsPremium);
            EnableSteamWindowsPremiumDefines();
        }

        [MenuItem(EditionMenuNameSteamWindowsPremium, true)]
        private static bool SteamWindowsPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameSteamWindowsPremium, _enabledSteamWindowsPremium);
            return true;
        }
        
        private static void MicrosoftWindowsPremiumToggle()
        {
            DisableAllEditions();
            _enabledMicrosoftWindowsPremium = true;
            EditorPrefs.SetBool(EditionMenuNameMicrosoftWindowsPremium, _enabledMicrosoftWindowsPremium);
            Enable8FloorWindowsPremiumCeDefines(true);
        }

        private static bool MicrosoftWindowsPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameMicrosoftWindowsPremium, _enabledMicrosoftWindowsPremium);
            return true;
        }
        
        private static void MicrosoftWindowsF2PToggle()
        {
            DisableAllEditions();
            _enabledMicrosoftWindowsF2P = true;
            EditorPrefs.SetBool(EditionMenuNameMicrosoftWindowsF2P, _enabledMicrosoftWindowsF2P);
            Enable8FloorWindowsPremiumCeDefines(true);
        }

        private static bool MicrosoftWindowsF2PToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameMicrosoftWindowsF2P, _enabledMicrosoftWindowsF2P);
            return true;
        }
        
        [MenuItem(EditionMenuName8FloorWindowsPremiumSe)]
        private static void WindowsPremiumSeToggle()
        {
            DisableAllEditions();
            _enabled8FloorWindowsPremiumSe = true;
            EditorPrefs.SetBool(EditionMenuName8FloorWindowsPremiumSe, _enabled8FloorWindowsPremiumSe);
            Enable8FloorWindowsPremiumCeDefines(false);
        }

        [MenuItem(EditionMenuName8FloorWindowsPremiumSe, true)]
        private static bool WindowsPremiumSeToggleValidate()
        {
            Menu.SetChecked(EditionMenuName8FloorWindowsPremiumSe, _enabled8FloorWindowsPremiumSe);
            return true;
        }
        
        private static void BfgWindowsPremiumToggle()
        {
            DisableAllEditions();
            _enabledBfgWindowsPremiumCe = true;
            EditorPrefs.SetBool(EditionMenuNameBfgWindowsPremiumCe, _enabledBfgWindowsPremiumCe);
            Enable8FloorWindowsPremiumCeDefines(true);
        }

        private static bool BfgWindowsPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameBfgWindowsPremiumCe, _enabledBfgWindowsPremiumCe);
            return true;
        }
        
        private static void BfgWindowsPremiumSeToggle()
        {
            DisableAllEditions();
            _enabledBfgWindowsPremiumSe = true;
            EditorPrefs.SetBool(EditionMenuNameBfgWindowsPremiumSe, _enabledBfgWindowsPremiumSe);
            Enable8FloorWindowsPremiumCeDefines(false);
        }

        private static bool BfgWindowsPremiumSeToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameBfgWindowsPremiumSe, _enabledBfgWindowsPremiumSe);
            return true;
        }
        
        // This method will be called when user pressed the button in menu
        private static void EgsWindowsPremiumToggle()
        {
            // Disable all other editions
            DisableAllEditions();
            // Set current edition to true and automatically call Validate method to set checked mark on this edition
            _enabledEgsWindowsPremium = true;
            EditorPrefs.SetBool(EditionMenuNameEgsWindowsPremium, _enabledEgsWindowsPremium);
            // Write edition specific instructions
            Enable8FloorWindowsPremiumCeDefines(true);
        }

        private static bool EgsWindowsPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameEgsWindowsPremium, _enabledEgsWindowsPremium);
            return true;
        }
        
        [MenuItem(EditionMenuName8FloorMacOSPremiumCe)]
        private static void MacOSPremiumToggle()
        {
            DisableAllEditions();
            _enabled8FloorMacOSPremiumCe = true;
            EditorPrefs.SetBool(EditionMenuName8FloorMacOSPremiumCe, _enabled8FloorMacOSPremiumCe);
            EnableMacOSPremiumDefines(true);
        }

        [MenuItem(EditionMenuName8FloorMacOSPremiumCe, true)]
        private static bool MacOSPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuName8FloorMacOSPremiumCe, _enabled8FloorMacOSPremiumCe);
            return true;
        }
        
        private static void SteamMacOSPremiumToggle()
        {
            DisableAllEditions();
            _enabledSteamMacOSPremium = true;
            EditorPrefs.SetBool(EditionMenuNameSteamMacOSPremium, _enabledSteamMacOSPremium);
            EnableMacOSPremiumDefines(true);
        }

        private static bool SteamMacOSPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameSteamMacOSPremium, _enabledSteamMacOSPremium);
            return true;
        }
        
        [MenuItem(EditionMenuName8FloorMacOSPremiumSe)]
        private static void MacOSPremiumSeToggle()
        {
            DisableAllEditions();
            _enabled8FloorMacOSPremiumSe = true;
            EditorPrefs.SetBool(EditionMenuName8FloorMacOSPremiumSe, _enabled8FloorMacOSPremiumSe);
            EnableMacOSPremiumDefines(false);
        }

        [MenuItem(EditionMenuName8FloorMacOSPremiumSe, true)]
        private static bool MacOSPremiumSeToggleValidate()
        {
            Menu.SetChecked(EditionMenuName8FloorMacOSPremiumSe, _enabled8FloorMacOSPremiumSe);
            return true;
        }
        
        private static void MacOSBfgPremiumToggle()
        {
            DisableAllEditions();
            _enabledBfgMacOSPremiumCe = true;
            EditorPrefs.SetBool(EditionMenuNameBfgMacOSPremiumCe, _enabledBfgMacOSPremiumCe);
            EnableMacOSPremiumDefines(true);
        }

        private static bool MacOSBfgPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameBfgMacOSPremiumCe, _enabledBfgMacOSPremiumCe);
            return true;
        }
        
        private static void MacOSBfgPremiumSeToggle()
        {
            DisableAllEditions();
            _enabledBfgMacOSPremiumSe = true;
            EditorPrefs.SetBool(EditionMenuNameBfgMacOSPremiumSe, _enabledBfgMacOSPremiumSe);
            EnableMacOSPremiumDefines(false);
        }

        private static bool MacOSBfgPremiumSeToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameBfgMacOSPremiumSe, _enabledBfgMacOSPremiumSe);
            return true;
        }
        
        [MenuItem(EditionMenuNameGooglePlayAndroidF2P)]
        private static void AndroidF2PToggle()
        {
            DisableAllEditions();
            _enabledGooglePlayAndroidF2PBuild = true;
            EditorPrefs.SetBool(EditionMenuNameGooglePlayAndroidF2P, _enabledGooglePlayAndroidF2PBuild);
            EnableGooglePlayAndroidF2PDefines();
        }

        [MenuItem(EditionMenuNameGooglePlayAndroidF2P, true)]
        private static bool AndroidF2PToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameGooglePlayAndroidF2P, _enabledGooglePlayAndroidF2PBuild);
            return true;
        }
        
        [MenuItem(EditionMenuNameGooglePlayAndroidPremium)]
        private static void GooglePlayAndroidPremiumToggle()
        {
            DisableAllEditions();
            _enabledGooglePlayAndroidPremiumBuild = true;
            EditorPrefs.SetBool(EditionMenuNameGooglePlayAndroidPremium, _enabledGooglePlayAndroidPremiumBuild);
            EnableGooglePlayAndroidPremiumDefines();
        }

        [MenuItem(EditionMenuNameGooglePlayAndroidPremium, true)]
        private static bool GooglePlayAndroidPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameGooglePlayAndroidPremium, _enabledGooglePlayAndroidPremiumBuild);
            Debug.Log(_enabledGooglePlayAndroidPremiumBuild);
            return true;
        }
        
        [MenuItem(EditionMenuNameAppstoreIosPremium)]
        private static void AppstoreIosPremiumToggle()
        {
            DisableAllEditions();
            _enabledAppstoreIosPremiumBuild = true;
            EditorPrefs.SetBool(EditionMenuNameAppstoreIosPremium, _enabledAppstoreIosPremiumBuild);
            EnableAppstoreIosPremiumDefines();
        }

        [MenuItem(EditionMenuNameAppstoreIosPremium, true)]
        private static bool AppstoreIosPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameAppstoreIosPremium, _enabledAppstoreIosPremiumBuild);
            Debug.Log(_enabledAppstoreIosPremiumBuild);
            return true;
        }
        
        [MenuItem(EditionMenuNameAppstoreIosF2P)]
        private static void AppstoreIosF2PToggle()
        {
            DisableAllEditions();
            _enabledAppstoreIosF2P = true;
            EditorPrefs.SetBool(EditionMenuNameAppstoreIosF2P, _enabledAppstoreIosF2P);
            EnableAppstoreIosF2PDefines();
        }

        [MenuItem(EditionMenuNameAppstoreIosF2P, true)]
        private static bool AppstoreIosF2PToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameAppstoreIosF2P, _enabledAppstoreIosF2P);
            return true;
        }
        
        [MenuItem(EditionMenuNameAppstoreMacosPremium)]
        private static void AppstoreMacosPremiumToggle()
        {
            DisableAllEditions();
            _enabledAppstoreMacosPremiumBuild = true;
            EditorPrefs.SetBool(EditionMenuNameAppstoreMacosPremium, _enabledAppstoreMacosPremiumBuild);
            EnableAppstoreMacosPremiumDefines();
        }

        [MenuItem(EditionMenuNameAppstoreMacosPremium, true)]
        private static bool AppstoreMacosPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameAppstoreMacosPremium, _enabledAppstoreMacosPremiumBuild);
            return true;
        }
        
        [MenuItem(EditionMenuNameAppstoreMacosF2P)]
        private static void AppstoreMacosF2PToggle()
        {
            DisableAllEditions();
            _enabledAppstoreMacosF2P = true;
            EditorPrefs.SetBool(EditionMenuNameAppstoreMacosF2P, _enabledAppstoreMacosF2P);
            EnableAppstoreMacosF2PDefines();
        }

        [MenuItem(EditionMenuNameAppstoreMacosF2P, true)]
        private static bool AppstoreMacosF2PToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameAppstoreMacosF2P, _enabledAppstoreMacosF2P);
            return true;
        }
        
        private static void Android8FloorPremiumCeToggle()
        {
            DisableAllEditions();
            _enabledAndroidPremiumCeBuild = true;
            EditorPrefs.SetBool(EditionMenuName8FloorAndroidPremiumCe, _enabledAndroidPremiumCeBuild);
            Enable8FloorAndroidPremiumDefines();
        }

        private static bool Android8FloorPremiumCeToggleValidate()
        {
            Menu.SetChecked(EditionMenuName8FloorAndroidPremiumCe, _enabledAndroidPremiumCeBuild);
            Debug.Log(_enabledAndroidPremiumCeBuild);
            return true;
        }
        private static void HuaweiAndroidF2PCeToggle()
        {
            DisableAllEditions();
            _enabledHuaweiAndroidF2P = true;
            EditorPrefs.SetBool(EditionMenuNameHuaweiAndroidF2P, _enabledHuaweiAndroidF2P);
            EnableHuaweiAndroidF2PDefines();
        }

        private static bool HuaweiAndroidF2PToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameHuaweiAndroidF2P, _enabledHuaweiAndroidF2P);
            Debug.Log(_enabledHuaweiAndroidF2P);
            return true;
        }
        private static void HuaweiAndroidPremiumCeToggle()
        {
            DisableAllEditions();
            _enabledHuaweiAndroidPremium = true;
            EditorPrefs.SetBool(EditionMenuNameHuaweiAndroidPremium, _enabledHuaweiAndroidPremium);
            EnableHuaweiAndroidPremiumDefines();
        }

        private static bool HuaweiAndroidPremiumToggleValidate()
        {
            Menu.SetChecked(EditionMenuNameHuaweiAndroidPremium, _enabledHuaweiAndroidPremium);
            Debug.Log(_enabledHuaweiAndroidPremium);
            return true;
        }
        
#endregion
        
        private static void SetTk2DDefine(bool enabled)
        {
            if (AssetDatabase.FindAssets("TK2DROOT", null).Length == 0)
            {
                Debug.Log("TK2D wasn't found in project.");
                return;
            }

            Debug.Log("TK2DROOT exist");
            SetDefine(Tk2DDefine, enabled);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, _allDefines);


        }

        private static void Enable8FloorWindowsPremiumCeDefines(bool ce)
        {
            // _allDefines заполняется в статическом конструкторе для платформы, выбранной на момент
            // перезагрузки домена. Если до этого была выбрана другая платформа, здесь лежат ЕЁ
            // дефайны, и они утекут в Standalone. Перечитываем актуальные для целевой платформы.
            RefreshDefines(NamedBuildTarget.Standalone);

            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            if (ce)
                SetDefine(CollectorDefine, true);
            CommitDefines(NamedBuildTarget.Standalone);

            SwitchBuildTarget(BuildTarget.StandaloneWindows);
        }

        private static void EnableSteamWindowsPremiumDefines()
        {
            RefreshDefines(NamedBuildTarget.Standalone);

            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            SetDefine(CollectorDefine, true);
            SetDefine(SteamBuildDefine, true);
            SetDefine(UpgradeDefine, true);
            CommitDefines(NamedBuildTarget.Standalone);

            SwitchBuildTarget(BuildTarget.StandaloneWindows);
        }

        private static void EnableMacOSPremiumDefines( bool ce)
        {
            RefreshDefines(NamedBuildTarget.Standalone);
            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            if (ce)
                SetDefine(CollectorDefine, true);
            CommitDefines(NamedBuildTarget.Standalone);

            SwitchBuildTarget(BuildTarget.StandaloneOSX);
        }

        private static void Enable8FloorAndroidPremiumDefines()
        {
            RefreshDefines(NamedBuildTarget.Android);
            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            SetDefine(CollectorDefine, true);
            CommitDefines(NamedBuildTarget.Android);

            SwitchBuildTarget(BuildTarget.Android);
        }
        
        private static void EnableGooglePlayAndroidF2PDefines()
        {
            RefreshDefines(NamedBuildTarget.Android);
            DisableAllOurDefines();
            SetDefine(GooglePlayDefine, true);
            SetDefine(HasAdDefine, true);
            SetDefine(CollectorDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            SetDefine(UpgradeDefine, true);
            CommitDefines(NamedBuildTarget.Android);

            SwitchBuildTarget(BuildTarget.Android);
        }
        
        private static void EnableGooglePlayAndroidPremiumDefines()
        {
            RefreshDefines(NamedBuildTarget.Android);
            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            SetDefine(GooglePlayDefine, true);
            SetDefine(CollectorDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            SetDefine(UpgradeDefine, true);
            CommitDefines(NamedBuildTarget.Android);

            SwitchBuildTarget(BuildTarget.Android);
        }
        
        private static void EnableUdpAndroidF2PDefines()
        {
            RefreshDefines(NamedBuildTarget.Android);
            DisableAllOurDefines();
            SetDefine(UdpDefine, true);
            SetDefine(HasAdDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            CommitDefines(NamedBuildTarget.Android);

            SwitchBuildTarget(BuildTarget.Android);
        }
        
        private static void EnableUdpAndroidPremiumDefines()
        {
            RefreshDefines(NamedBuildTarget.Android);
            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            SetDefine(UdpDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            CommitDefines(NamedBuildTarget.Android);

            SwitchBuildTarget(BuildTarget.Android);
        }
        
        private static void EnableAppstoreIosPremiumDefines()
        {
            RefreshDefines(NamedBuildTarget.iOS);
            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            SetDefine(CollectorDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            SetDefine(UpgradeDefine, true);
            CommitDefines(NamedBuildTarget.iOS);

            SwitchBuildTarget(BuildTarget.iOS);
        }
        
        private static void EnableAppstoreIosF2PDefines()
        {
            RefreshDefines(NamedBuildTarget.iOS);
            DisableAllOurDefines();
            SetDefine(HasAdDefine, true);
            SetDefine(CollectorDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            SetDefine(UpgradeDefine, true);
            CommitDefines(NamedBuildTarget.iOS);

            SwitchBuildTarget(BuildTarget.iOS);
        }
        
        private static void EnableAppstoreMacosPremiumDefines()
        {
            RefreshDefines(NamedBuildTarget.Standalone);
            DisableAllOurDefines();
            SetDefine(PremiumDefine, true);
            SetDefine(CollectorDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            SetDefine(MacAppstoreDefine, true);
            SetDefine(UpgradeDefine, true);
            CommitDefines(NamedBuildTarget.Standalone);

            SwitchBuildTarget(BuildTarget.StandaloneOSX);
        }
        
        private static void EnableAppstoreMacosF2PDefines()
        {
            RefreshDefines(NamedBuildTarget.Standalone);
            DisableAllOurDefines();
            SetDefine(HasAdDefine, false);
            SetDefine(CollectorDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            SetDefine(MacAppstoreDefine, true);
            SetDefine(UpgradeDefine, true);
            CommitDefines(NamedBuildTarget.Standalone);

            SwitchBuildTarget(BuildTarget.StandaloneOSX);
        }
        
        private static void EnableHuaweiAndroidF2PDefines()
        {
            RefreshDefines(NamedBuildTarget.Android);
            DisableAllOurDefines();
            SetDefine(HuaweiDefine, true);
            SetDefine(HasAdDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            CommitDefines(NamedBuildTarget.Android);

            SwitchBuildTarget(BuildTarget.Android);
        }
        
        private static void EnableHuaweiAndroidPremiumDefines()
        {
            RefreshDefines(NamedBuildTarget.Android);
            DisableAllOurDefines();
            SetDefine(HuaweiDefine, true);
            SetDefine(PremiumDefine, true);
            SetDefine(CollectorDefine, true);
            SetDefine(UnityAnalyticsDefine, true);
            CommitDefines(NamedBuildTarget.Android);

            SwitchBuildTarget(BuildTarget.Android);
        }
        
#region Utils
        [MenuItem(DefinesCheckMenuName)]
        private static void DefinesCheck()
        {
            var str = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings
                .selectedBuildTargetGroup);
            Debug.Log(str);
        }

        // Сбрасывать нужно ВСЕ 22 флага: иначе галка предыдущего издания остаётся стоять
        // и меню показывает два выбранных издания одновременно.
        private static void DisableAllEditions()
        {
            _enabledGooglePlayAndroidF2PBuild = false;
            _enabled8FloorWindowsPremiumCe = false;
            _enabled8FloorWindowsPremiumSe = false;
            _enabled8FloorMacOSPremiumCe = false;
            _enabled8FloorMacOSPremiumSe = false;
            _enabledAndroidPremiumCeBuild = false;
            _enabledGooglePlayAndroidPremiumBuild = false;
            _enabledAppstoreIosPremiumBuild = false;
            _enabledAppstoreIosF2P = false;
            _enabledAppstoreMacosPremiumBuild = false;
            _enabledAppstoreMacosF2P = false;
            _enabledSteamWindowsPremium = false;
            _enabledSteamMacOSPremium = false;
            _enabledMicrosoftWindowsPremium = false;
            _enabledMicrosoftWindowsF2P = false;
            _enabledBfgWindowsPremiumCe = false;
            _enabledBfgWindowsPremiumSe = false;
            _enabledBfgMacOSPremiumCe = false;
            _enabledBfgMacOSPremiumSe = false;
            _enabledEgsWindowsPremium = false;
            _enabledHuaweiAndroidF2P = false;
            _enabledHuaweiAndroidPremium = false;

            foreach (var menuName in AllEditionMenuNames)
            {
                EditorPrefs.SetBool(menuName, false);
            }
        }

        private static void RefreshDefines(NamedBuildTarget namedTarget)
        {
            _allDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        }

        private static void SetDefine(string defineName, bool value)
        {
            var defines = (_allDefines ?? string.Empty)
                .Split(';')
                .Select(define => define.Trim())
                .Where(define => !string.IsNullOrEmpty(define) && define != defineName)
                .ToList();

            if (value)
                defines.Add(defineName);

            _allDefines = string.Join(";", defines);
        }

        private static void DisableAllOurDefines()
        {
            SetDefine(UdpDefine, false);
            SetDefine(GooglePlayDefine, false);
            SetDefine(PremiumDefine, false);
            SetDefine(CollectorDefine, false);
            SetDefine(HasAdDefine, false);
            SetDefine(UnityAnalyticsDefine, false);
            SetDefine(MacAppstoreDefine, false);
            SetDefine(HuaweiDefine, false);
            SetDefine(SteamBuildDefine, false);
            SetDefine(UpgradeDefine, false);
        }
#endregion
    }
}
