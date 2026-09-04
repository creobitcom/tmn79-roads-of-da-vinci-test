#if UNITY_EDITOR
using System.IO;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace _8floor.TimeManagement.Core.Scripts.Editor.VideoCutscenes
{
    /// <summary>
    /// Собирает префаб экрана видео-катсцены (Panel_VideoCutscene) со всей разводкой полей.
    /// Делается один раз на проект, дальше префаб можно править руками как обычный UI.
    /// </summary>
    public static class VideoCutscenePrefabGenerator
    {
        private const string DefaultFolder = "Assets/LA8_slv/Cutscenes";
        private const string PrefabName = "Panel_VideoCutscene.prefab";

        [MenuItem("Tools/Cutscenes/Video Cutscene Prefab")]
        private static void Generate()
        {
            var folder = DefaultFolder;

            if (!AssetDatabase.IsValidFolder(folder))
            {
                folder = EditorUtility.SaveFolderPanel("Куда положить префаб экрана", "Assets", string.Empty);

                if (string.IsNullOrEmpty(folder))
                {
                    return;
                }

                folder = "Assets" + folder.Substring(Application.dataPath.Length);
            }

            var path = Path.Combine(folder, PrefabName).Replace('\\', '/');

            if (File.Exists(path)
                && !EditorUtility.DisplayDialog("Префаб уже есть",
                    $"{path} будет перезаписан. Продолжить?", "Перезаписать", "Отмена"))
            {
                return;
            }

            var root = BuildHierarchy();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);

            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log($"Экран видео-катсцены создан: {path}\n" +
                      "Положи его в поле \"Префаб экрана\" у CutsceneLibrary.", prefab);
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("Panel_VideoCutscene",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(VideoPlayer),
                typeof(AudioSource),
                typeof(VideoCutsceneView));

            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            // overrideSorting Unity сбросит при сохранении (канвас тут корневой),
            // поэтому порядок отрисовки выставляет VideoCutsceneView в рантайме.
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var audioSource = root.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;

            var videoPlayer = root.GetComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

            // Чёрная подложка на весь экран.
            var background = CreateChild(rootRect, "Background", typeof(Image));
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = Color.black;

            // Кадр ролика с сохранением пропорций.
            var screen = CreateChild(rootRect, "Screen", typeof(RawImage), typeof(AspectRatioFitter));
            var screenRect = (RectTransform)screen.transform;
            screenRect.anchorMin = new Vector2(0.5f, 0.5f);
            screenRect.anchorMax = new Vector2(0.5f, 0.5f);
            screenRect.pivot = new Vector2(0.5f, 0.5f);
            screenRect.sizeDelta = new Vector2(1920f, 1080f);

            var fitter = screen.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            var rawImage = screen.GetComponent<RawImage>();
            rawImage.raycastTarget = false;

            // Плашка с субтитром.
            var plate = CreateChild(rootRect, "SubtitlePlate", typeof(Image));
            var plateRect = (RectTransform)plate.transform;
            plateRect.anchorMin = new Vector2(0f, 0f);
            plateRect.anchorMax = new Vector2(1f, 0f);
            plateRect.pivot = new Vector2(0.5f, 0f);
            plateRect.offsetMin = new Vector2(120f, 90f);
            plateRect.offsetMax = new Vector2(-120f, 90f);
            plateRect.sizeDelta = new Vector2(plateRect.sizeDelta.x, 160f);

            var plateImage = plate.GetComponent<Image>();
            plateImage.color = new Color(0f, 0f, 0f, 0.55f);
            plateImage.raycastTarget = false;

            var subtitle = CreateChild((RectTransform)plate.transform, "Subtitle", typeof(TextMeshProUGUI));
            var subtitleRect = (RectTransform)subtitle.transform;
            Stretch(subtitleRect);
            subtitleRect.offsetMin = new Vector2(24f, 12f);
            subtitleRect.offsetMax = new Vector2(-24f, -12f);

            var subtitleText = subtitle.GetComponent<TextMeshProUGUI>();
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.enableAutoSizing = true;
            subtitleText.fontSizeMin = 24f;
            subtitleText.fontSizeMax = 44f;
            subtitleText.raycastTarget = false;
            subtitleText.text = string.Empty;

            // Кнопка пропуска.
            var skip = CreateChild(rootRect, "SkipButton", typeof(Image), typeof(Button));
            var skipRect = (RectTransform)skip.transform;
            skipRect.anchorMin = new Vector2(1f, 0f);
            skipRect.anchorMax = new Vector2(1f, 0f);
            skipRect.pivot = new Vector2(1f, 0f);
            skipRect.anchoredPosition = new Vector2(-60f, 60f);
            skipRect.sizeDelta = new Vector2(280f, 90f);

            skip.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var skipLabel = CreateChild(skipRect, "Label", typeof(TextMeshProUGUI));
            Stretch((RectTransform)skipLabel.transform);

            var skipText = skipLabel.GetComponent<TextMeshProUGUI>();
            skipText.alignment = TextAlignmentOptions.Center;
            skipText.fontSize = 32f;
            skipText.raycastTarget = false;
            skipText.text = "Skip";

            skip.SetActive(false);

            Wire(root, videoPlayer, audioSource, rawImage, fitter, subtitleText, plate,
                skip.GetComponent<Button>(), root.GetComponent<CanvasGroup>());

            return root;
        }

        private static void Wire(GameObject root,
            VideoPlayer videoPlayer,
            AudioSource audioSource,
            RawImage screen,
            AspectRatioFitter fitter,
            TMP_Text subtitle,
            GameObject plate,
            Button skipButton,
            CanvasGroup canvasGroup)
        {
            var view = root.GetComponent<VideoCutsceneView>();
            var serialized = new SerializedObject(view);

            serialized.FindProperty("_videoPlayer").objectReferenceValue = videoPlayer;
            serialized.FindProperty("_audioSource").objectReferenceValue = audioSource;
            serialized.FindProperty("_screen").objectReferenceValue = screen;
            serialized.FindProperty("_aspectFitter").objectReferenceValue = fitter;
            serialized.FindProperty("_subtitleText").objectReferenceValue = subtitle;
            serialized.FindProperty("_subtitlePlate").objectReferenceValue = plate;
            serialized.FindProperty("_skipButton").objectReferenceValue = skipButton;
            serialized.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateChild(RectTransform parent, string name, params System.Type[] components)
        {
            var child = new GameObject(name, components);
            var rect = child.GetComponent<RectTransform>();

            if (rect == null)
            {
                rect = child.AddComponent<RectTransform>();
            }

            rect.SetParent(parent, false);

            return child;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
#endif
