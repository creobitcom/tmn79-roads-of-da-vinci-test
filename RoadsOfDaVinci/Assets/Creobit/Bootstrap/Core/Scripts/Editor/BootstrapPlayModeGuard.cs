#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Creobit.Bootstrap.Core.Scripts.Editor
{
    [InitializeOnLoad]
    public static class BootstrapPlayModeGuard
    {
        private const string EnabledPrefKey = "Creobit.BootstrapPlayModeGuard.Enabled";
        private const string MenuPath = "Tools/Creobit/Bootstrap/Require Bootstrap Scene on Play";
        private const string BootstrapSceneName = "Bootstrap";

        private static readonly string FallbackBootstrapScenePath =
            "Assets/_ProjectTemplate/Common/Bootstrap/Scenes/Bootstrap.unity";

        /// <summary>
        /// One-shot bypass for the next ExitingEditMode after the user chose "Play anyway".
        /// Without this, setting isPlaying=true re-enters the guard and cancels Play again.
        /// </summary>
        private static bool _allowNextPlay;

        public static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        static BootstrapPlayModeGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }

        public static string GetBootstrapScenePath()
        {
            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length > 0 && buildScenes[0].enabled && !string.IsNullOrEmpty(buildScenes[0].path))
                return buildScenes[0].path;

            return FallbackBootstrapScenePath;
        }

        public static bool IsBootstrapSceneActive()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
                return false;

            var bootstrapPath = GetBootstrapScenePath();
            if (string.Equals(activeScene.path, bootstrapPath, StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(activeScene.name, BootstrapSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                _allowNextPlay = false;
                return;
            }

            if (state != PlayModeStateChange.ExitingEditMode || !IsEnabled || IsBootstrapSceneActive())
                return;

            if (_allowNextPlay)
            {
                _allowNextPlay = false;
                return;
            }

            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ShowPlayBlockedDialog;
        }

        private static void ShowPlayBlockedDialog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var activeScene = SceneManager.GetActiveScene();
            var bootstrapPath = GetBootstrapScenePath();
            var bootstrapName = System.IO.Path.GetFileNameWithoutExtension(bootstrapPath);

            var message =
                $"Сейчас открыта сцена «{activeScene.name}», а игра должна запускаться из Bootstrap.\n\n" +
                $"Откройте сцену «{bootstrapName}» или подтвердите запуск с текущей сцены.";

            var choice = EditorUtility.DisplayDialogComplex(
                "Не Bootstrap-сцена",
                message,
                "Открыть Bootstrap",
                "Отмена",
                "Всё равно Play");

            switch (choice)
            {
                case 0:
                    OpenBootstrapSceneAndPlay(bootstrapPath);
                    break;
                case 2:
                    _allowNextPlay = true;
                    EditorApplication.isPlaying = true;
                    break;
            }
        }

        private static void OpenBootstrapSceneAndPlay(string bootstrapPath)
        {
            if (!System.IO.File.Exists(bootstrapPath))
            {
                EditorUtility.DisplayDialog(
                    "Bootstrap не найден",
                    $"Сцена Bootstrap не найдена по пути:\n{bootstrapPath}",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(bootstrapPath, OpenSceneMode.Single);
            _allowNextPlay = true;
            EditorApplication.isPlaying = true;
        }
    }

    [Overlay(typeof(SceneView), "Bootstrap Play Guard", true)]
    public class BootstrapPlayModeOverlay : Overlay, ITransientOverlay
    {
        public bool visible => BootstrapPlayModeGuard.IsEnabled && !BootstrapPlayModeGuard.IsBootstrapSceneActive();

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.85f, 0.25f, 0.1f, 0.95f),
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 10,
                    paddingRight = 10,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    maxWidth = 420
                }
            };

            var title = new Label("Запуск не из Bootstrap")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    marginBottom = 4
                }
            };

            var activeScene = SceneManager.GetActiveScene();
            var bootstrapName = System.IO.Path.GetFileNameWithoutExtension(BootstrapPlayModeGuard.GetBootstrapScenePath());
            var description = new Label(
                $"Открыта «{activeScene.name}». Play запустит игру не через Bootstrap. " +
                $"Нужна сцена «{bootstrapName}».")
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    color = Color.white
                }
            };

            root.Add(title);
            root.Add(description);
            return root;
        }
    }
}
#endif
