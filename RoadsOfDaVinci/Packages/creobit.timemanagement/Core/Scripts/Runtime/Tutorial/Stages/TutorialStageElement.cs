using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Stages
{
    public enum TutorialStageHideMode
    {
        [LabelText("Никогда")]
        Never = 0,

        [LabelText("На следующей стадии")]
        NextStage = 1,

        [LabelText("На стадии...")]
        AtStage = 2,
    }

    /// <summary>
    /// Помечает часть окна туториала как принадлежащую стадии.
    /// Дочерние объекты отдельно помечать не нужно: видимость идёт через CanvasGroup,
    /// а он гасит всю ветку целиком.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("8floor/Tutorial/Стадия туториала")]
    public class TutorialStageElement : MonoBehaviour
    {
        [SerializeField]
        [LabelText("Появляется на стадии")]
        [MinValue(0)]
        [Tooltip("0 — элемент виден с самого начала. Стадии туториала считаются с 1.")]
        private int stage = 1;

        [SerializeField]
        [FormerlySerializedAs("visibility")]
        [LabelText("Исчезает")]
        [EnumToggleButtons]
        private TutorialStageHideMode hideMode = TutorialStageHideMode.Never;

        [SerializeField]
        [LabelText("Исчезает на стадии")]
        [MinValue(1)]
        [ShowIf(nameof(HidesAtSpecifiedStage))]
        [InfoBox("Стадия исчезновения должна быть больше стадии появления.",
            InfoMessageType.Error, nameof(HasInvalidHideStage))]
        private int hideStage = 2;

        private CanvasGroup _canvasGroup;

        public int Stage => stage;

        public TutorialStageHideMode HideMode => hideMode;

        public int HideStage => hideStage;

        public CanvasGroup Group
        {
            get
            {
                if (_canvasGroup != null)
                {
                    return _canvasGroup;
                }

                if (!TryGetComponent(out _canvasGroup))
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }

                return _canvasGroup;
            }
        }

        public float Alpha => Group.alpha;

        /// <summary>
        /// Виден ли элемент на указанной стадии.
        /// </summary>
        public bool IsVisibleOn(int currentStage)
        {
            if (currentStage < stage)
            {
                return false;
            }

            return hideMode switch
            {
                TutorialStageHideMode.NextStage => currentStage == stage,
                TutorialStageHideMode.AtStage => currentStage < hideStage,
                _ => true,
            };
        }

        public void SetAlpha(float alpha)
        {
            Group.alpha = alpha;
        }

        /// <summary>
        /// Погашенный элемент не должен ловить клики, даже пока он ещё не полностью прозрачен.
        /// </summary>
        public void SetInteractive(bool interactive)
        {
            var group = Group;

            group.blocksRaycasts = interactive;
            group.interactable = interactive;
        }

        private bool HidesAtSpecifiedStage => hideMode == TutorialStageHideMode.AtStage;

        private bool HasInvalidHideStage => HidesAtSpecifiedStage && hideStage <= stage;

#if UNITY_EDITOR
        [Button("Показать эту стадию", ButtonSizes.Medium)]
        [GUIColor(0.5f, 0.9f, 0.5f)]
        private void EditorShowStage()
        {
            var tutorialView = GetComponentInParent<global::_8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.TutorialView>(true);

            if (tutorialView == null)
            {
                Debug.LogWarning($"[Tutorial] У {name} не найден родительский TutorialView.", this);
                return;
            }

            tutorialView.ApplyStagePreview(stage);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public void EditorSetup(int newStage, TutorialStageHideMode newHideMode, int newHideStage = 0)
        {
            stage = newStage;
            hideMode = newHideMode;
            hideStage = newHideStage > 0 ? newHideStage : Mathf.Max(1, newStage + 1);
        }
#endif
    }
}
