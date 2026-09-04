using System.Collections.Generic;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    public interface ITutorialController : ILoadUnit
    {
        public ReactiveProperty<bool> isEnable { get; set; }
        public ReactiveProperty<bool> isShowing { get; }

        public List<TutorialEvent> onTutorialShowEvents { get; set; }
        public List<TutorialEvent> onTutorialHideEvents { get; set; }
        public List<TutorialViewLevelData> tutorialViewLevelData { get; set; }

        public void ShowTutorial(TutorialViewSo tutorialViewSO);
        public void ShowTutorialAtStage(TutorialViewSo tutorialViewSO, int stage);
        public void ShowHint(TutorialViewSo tutorialViewSO, float durationSeconds = 0f);

        public TutorialViewSo CurrentHint { get; }
        public void HideHint();
        public void HideTutorial([CanBeNull] TutorialViewSo tutorialViewSO = null, bool force = false);
        public void SetEnable(bool enable);
        public UniTaskVoid ShowConfirmTutorial(bool enable);
        public void HideConfirmTutorial();
        public void AddTutorialOpenKey(TutorialViewSo tutorialViewSO, int id);
        public void AddTutorialCloseKey(TutorialViewSo tutorialViewSO, int id);
        public void AddStageOpenKey(TutorialViewSo tutorialViewSO, int stage, int id);
        public void AddStageCloseKey(TutorialViewSo tutorialViewSO, int stage, int id);
        public void GoToStageIfCurrent(int currentStage, int targetStage);
        public void GoToStageIfCurrent(TutorialViewSo tutorialViewSO, int currentStage, int targetStage);
        public void HighlightObservableObject(TutorialViewSo tutorialViewSO, int id);
        public void DisableHighlightObservableObject(TutorialViewSo tutorialViewSO, int id);

        /// <summary>
        /// Разрешает или запрещает взаимодействие с объектом независимо от стрелки.
        /// </summary>
        public void SetObjectInteractable(TutorialViewSo tutorialViewSO, int id, bool interactable);

        /// <summary>
        /// Показывает или прячет стрелку независимо от разрешения на взаимодействие.
        /// </summary>
        public void SetArrowVisible(TutorialViewSo tutorialViewSO, int id, bool visible);

        /// <summary>
        /// Прячет окно, не закрывая туториал. Окно вернётся само на следующей смене стадии.
        /// </summary>
        public void HideWindow();

        public void SetAlternativePanelAutoHide(bool value);

        public void SetAllowedAlternative(int index);

        public bool GoNextStage();

        public bool GoNextStage(TutorialViewSo tutorialViewSO);

        public void GoPrevStage();

        public void GoToStage(int stage);

        public void GoToStage(TutorialViewSo tutorialViewSO, int stage);

        /// <summary>
        /// Следующая стадия, а если она была последней — закрыть окно.
        /// Одна кнопка «Ок» на все шаги туториала.
        /// </summary>
        public void NextStageOrHide();
    }
}
