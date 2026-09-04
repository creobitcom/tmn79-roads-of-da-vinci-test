using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Preview
{
    /// <summary>
    /// Живёт в тест-сцене превью. Сцена уже содержит настоящий префаб уровня, камеру и канвас
    /// как в игре, поэтому вид виден прямо в Game-view — Play нажимать не обязательно.
    /// В Play даёт экранные кнопки для листания стадий.
    /// </summary>
    [AddComponentMenu("8floor/Tutorial/Превью туториала (тест-сцена)")]
    public class TutorialPreviewRunner : MonoBehaviour
    {
        [Title("Окно")]
        [InfoBox("Окно уже лежит в сцене под канвасом. Здесь только ссылка на него, " +
                 "чтобы кнопки листали стадии.")]
        [SerializeField]
        private TutorialView view;

        private void Start()
        {
            if (view == null)
            {
                view = FindFirstObjectByType<TutorialView>();
            }

            if (view == null)
            {
                return;
            }

            RewireWindowButtons();
            view.ResetStages();
        }

        // Кнопка «Ок» внутри окна в игре зовёт мост (TutorialBridge), а он тут не заинжектен —
        // клик привёл бы к ошибке. Перехватываем на себя, чтобы кнопка листала стадии.
        private void RewireWindowButtons()
        {
            foreach (var button in view.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(NextOrRestart);
            }
        }

        private void NextOrRestart()
        {
            if (view == null)
            {
                return;
            }

            if (!view.TryGoNextStage())
            {
                view.ResetStages();
            }
        }

        private void OnGUI()
        {
            if (view == null)
            {
                return;
            }

            const float w = 150f;
            const float h = 44f;
            var y = Screen.height - h - 16f;

            GUI.Box(new Rect(12, y - 34, 3 * w + 24, 28),
                view.HasStages ? $"Стадия {view.CurrentStage} из {view.MaxStage}" : "Без стадий");

            if (GUI.Button(new Rect(12, y, w, h), "◀ Пред"))
            {
                view.GoPrevStage();
            }

            if (GUI.Button(new Rect(12 + w + 6, y, w, h), "След ▶"))
            {
                view.GoNextStage();
            }

            if (GUI.Button(new Rect(12 + 2 * (w + 6), y, w, h), "⟲ Заново"))
            {
                view.ResetStages();
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(TutorialView tutorialView) => view = tutorialView;
#endif
    }
}
