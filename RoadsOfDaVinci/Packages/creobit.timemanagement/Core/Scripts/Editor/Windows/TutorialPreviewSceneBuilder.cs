using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Windows
{
    /// <summary>
    /// Собирает тест-сцену: настоящий префаб уровня + камера и канвас как в игре + окно туториала
    /// поверх. Вид виден сразу в Game-view, без Play — значит скрипты уровня не стартуют
    /// и не сыплют ошибками из-за отсутствующего DI.
    /// </summary>
    public static class TutorialPreviewSceneBuilder
    {
        private const string ScenePath = "Assets/_TutorialPreview.unity";

        public static TutorialView BuildAndOpen(GameObject windowPrefab, GameObject levelPrefab, string title,
            float cameraOrthoSize, Vector2 referenceResolution, float cameraDistance)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SpawnLevel(levelPrefab);

            CreateCamera(cameraOrthoSize, cameraDistance);
            var canvas = CreateCanvas(referenceResolution);
            CreateEventSystem();

            var view = SpawnWindow(windowPrefab, canvas);

            var runner = new GameObject("TutorialPreviewRunner", typeof(TutorialPreviewRunner))
                .GetComponent<TutorialPreviewRunner>();
            runner.EditorSetup(view);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Туториалы] Тест-сцена «{title}» готова: {ScenePath}. " +
                      "Смотри в Game-view — окно лежит на реальном уровне. " +
                      "Стадии листаются кнопками в тулзе, а в Play — экранными кнопками.");

            return view;
        }

        /// <summary>
        /// Ставит уровень так же, как это делает LevelLoader — обычным инстансом префаба.
        /// </summary>
        private static void SpawnLevel(GameObject levelPrefab)
        {
            if (levelPrefab == null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(levelPrefab);
            instance.transform.position = levelPrefab.transform.position;
        }

        /// <summary>
        /// Камера копирует настройки геймплей-сцены: ортографическая, на заданном отдалении.
        /// Размер там получается из фона — (высота фона / pixels per unit) / 2.
        /// </summary>
        private static void CreateCamera(float orthoSize, float distance)
        {
            var go = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            go.transform.position = new Vector3(0f, 0f, -Mathf.Abs(distance));
        }

        private static Canvas CreateCanvas(Vector2 referenceResolution)
        {
            var go = new GameObject("Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Ровно как в геймплей-сцене: масштабирование под экран от 1920×1080 в режиме Expand.
            // Это не то же самое, что «1 UI-пиксель = 1 пиксель экрана» — от режима напрямую
            // зависит видимый размер окна.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            return canvas;
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static TutorialView SpawnWindow(GameObject windowPrefab, Canvas canvas)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(windowPrefab, canvas.transform);

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;

            // Окно принесло свой канвас — под экранным родителем он должен просто наследовать режим.
            var nested = instance.GetComponent<Canvas>();

            if (nested != null)
            {
                nested.overrideSorting = false;
            }

            var view = instance.GetComponentInChildren<TutorialView>(true);

            if (view != null)
            {
                view.ResetStages();
            }

            return view;
        }
    }
}
