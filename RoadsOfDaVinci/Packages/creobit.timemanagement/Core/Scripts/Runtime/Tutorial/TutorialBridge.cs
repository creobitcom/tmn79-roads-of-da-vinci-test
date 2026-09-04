using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using Cysharp.Threading.Tasks;
using R3;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    public class TutorialBridge : MonoBehaviour, IReloadable
    {
        [SerializeField]
        private bool isScene;

        [SerializeField]
        private List<TutorialEvent> onTutorialShowEvents;

        [SerializeField]
        private List<TutorialEvent> onTutorialHideEvents;

        [SerializeField]
        private List<TutorialViewLevelData> tutorialViewData;

        private ITutorialController _tutorialController;
        private IGameplayInputSystem _inputSystem;
        private IReloadController _reloadController;
        private IDisposable _isEnableSubscription;
        private readonly HashSet<IReactToActions> _goNextStageOnceSources = new();

        public UltEvent<bool, bool> OnEnableDisableTutorial;

        [Inject]
        public void Construct(ITutorialController tutorialController,
            IReloadController reloadController,
            IGameplayInputSystem inputSystem)
        {
            _tutorialController = tutorialController;
            _inputSystem = inputSystem;
            _reloadController = reloadController;

            reloadController.AddReloadableObject(this);

            Init();
        }

        private void OnDestroy()
        {
            _reloadController?.RemoveReloadableObject(this);

            RemoveDataFromController();
        }

        public void ShowConfirmTutorial(bool enable)
        {
            _tutorialController.ShowConfirmTutorial(enable).Forget();
        }

        public void HideConfirmTutorial(bool accept)
        {
            _tutorialController.HideConfirmTutorial();
            
            if (accept)
            {
                _tutorialController.SetEnable(!_tutorialController.isEnable.Value);
            }
        }

        [Button]
        public void ShowTutorial(TutorialViewSo tutorialViewSO)
        {
            _tutorialController.ShowTutorial(tutorialViewSO);
        }

        public void ShowTutorialAtStage(TutorialViewSo tutorialViewSO, int stage)
        {
            _tutorialController.ShowTutorialAtStage(tutorialViewSO, stage);
        }

        public void ShowHint(TutorialViewSo tutorialViewSO)
        {
            _tutorialController.ShowHint(tutorialViewSO);
        }

        public void ShowHint(TutorialViewSo tutorialViewSO, float durationSeconds)
        {
            _tutorialController.ShowHint(tutorialViewSO, durationSeconds);
        }

        public void HideHint()
        {
            _tutorialController.HideHint();
        }

        [Button]
        public void HideTutorial()
        {
            _tutorialController.HideTutorial();
        }

        public void AddTutorialOpenKey(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.AddTutorialOpenKey(tutorialViewSO, id);
        }

        public void AddTutorialCloseKey(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.AddTutorialCloseKey(tutorialViewSO, id);
        }

        public void AddStageOpenKey(TutorialViewSo tutorialViewSO, int stage, int id)
        {
            _tutorialController.AddStageOpenKey(tutorialViewSO, stage, id);
        }

        public void AddStageCloseKey(TutorialViewSo tutorialViewSO, int stage, int id)
        {
            _tutorialController.AddStageCloseKey(tutorialViewSO, stage, id);
        }

        public void HighlightObservableObject(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.HighlightObservableObject(tutorialViewSO, id);
        }

        public void DeactivateTutorial() 
        {
            _tutorialController.SetEnable(false);
        }

        public void DisableHighlightObservableObject(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.DisableHighlightObservableObject(tutorialViewSO, id);
        }

        /// <summary>
        /// Разрешить клик по объекту, не показывая стрелку.
        /// </summary>
        public void EnableObjectInteraction(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.SetObjectInteractable(tutorialViewSO, id, true);
        }

        /// <summary>
        /// Запретить клик по объекту, стрелку не трогать.
        /// </summary>
        public void DisableObjectInteraction(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.SetObjectInteractable(tutorialViewSO, id, false);
        }

        /// <summary>
        /// Показать стрелку, не разрешая клик.
        /// </summary>
        public void ShowArrow(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.SetArrowVisible(tutorialViewSO, id, true);
        }

        /// <summary>
        /// Спрятать стрелку, разрешение на клик не трогать.
        /// </summary>
        public void HideArrow(TutorialViewSo tutorialViewSO, int id)
        {
            _tutorialController.SetArrowVisible(tutorialViewSO, id, false);
        }

        [Button]
        public void DisableAlternativePanelAutoHide()
        {
            _tutorialController.SetAlternativePanelAutoHide(false);
        }

        [Button]
        public void EnableAlternativePanelAutoHide()
        {
            _tutorialController.SetAlternativePanelAutoHide(true);
        }

        public void SetAlternativePanelAutoHide(bool value)
        {
            _tutorialController.SetAlternativePanelAutoHide(value);
        }

        public void AllowOnlyAlternative(int index)
        {
            _tutorialController.SetAllowedAlternative(index);
        }

        [Button]
        public void AllowAllAlternatives()
        {
            _tutorialController.SetAllowedAlternative(-1);
        }

        /// <summary>
        /// Следующая стадия, а если она была последней — закрыть окно.
        /// Это и есть кнопка «Ок» на всех шагах.
        /// </summary>
        [Button]
        public void NextStageOrHide()
        {
            _tutorialController.NextStageOrHide();
        }

        /// <summary>
        /// Спрятать окно на время, не закрывая туториал: стадия сохранится, а окно вернётся
        /// само, когда стадию пролистают. Это замена HideTutorial внутри цепочки шагов.
        /// </summary>
        [Button]
        public void HideWindow()
        {
            _tutorialController.HideWindow();
        }

        /// <summary>
        /// Вариант для кликов по объектам уровня. Only Once запоминается отдельно для каждого нажатого объекта.
        /// </summary>
        public void GoNextStage(bool onlyOnce)
        {
            var source = onlyOnce ? _inputSystem.CurrentPrimaryActionTarget : null;

            if (source != null && _goNextStageOnceSources.Contains(source))
            {
                return;
            }

            var stageChanged = _tutorialController.GoNextStage();

            // Запоминаем только успешный переход и только за конкретным нажатым объектом.
            if (source != null && stageChanged)
            {
                _goNextStageOnceSources.Add(source);
            }
        }

        [Button]
        public void GoPrevStage()
        {
            _tutorialController.GoPrevStage();
        }

        public void GoNextStage(TutorialViewSo tutorialViewSO)
        {
            _tutorialController.GoNextStage(tutorialViewSO);
        }

        public void GoToStage(int stage)
        {
            _tutorialController.GoToStage(stage);
        }

        public void GoToStage(TutorialViewSo tutorialViewSO, int stage)
        {
            _tutorialController.GoToStage(tutorialViewSO, stage);
        }

        public void GoToStageIfCurrent(int currentStage, int targetStage)
        {
            _tutorialController.GoToStageIfCurrent(currentStage, targetStage);
        }

        public void GoToStageIfCurrent(TutorialViewSo tutorialViewSO, int currentStage, int targetStage)
        {
            _tutorialController.GoToStageIfCurrent(tutorialViewSO, currentStage, targetStage);
        }

        [Button]
        public void HideTutorial(TutorialViewSo tutorialViewSO)
        {
            _tutorialController.HideTutorial(tutorialViewSO);
        }

        [Button]
        public void Validate()
        {
            var isCorrect = true;
            foreach (var actionToClose in tutorialViewData)
            {
                for (var index = 0; index < actionToClose.HighlightObjects.Count; index++)
                {
                    var availableObject = actionToClose.HighlightObjects[index];

                    if (availableObject.target == null)
                    {
                        Debug.LogError("AvailableObject is null on the position " + (index + 1));
                        isCorrect = false;
                        continue;
                    }

                    if (!availableObject.reactToActions.TryGetComponent<IReactToActions>(out var _))
                    {
                        Debug.LogError(availableObject.reactToActions.name + " on the position " + (index + 1) +
                                       " doesn't have IReactToActions component, choose correct object");
                        isCorrect = false;
                    }
                }
            }

            if (isCorrect)
                Debug.Log("All is correct");
        }

        private void Init()
        {
            RemoveDataFromController();

            _tutorialController.onTutorialShowEvents.AddRange(onTutorialShowEvents);
            _tutorialController.onTutorialHideEvents.AddRange(onTutorialHideEvents);
            _tutorialController.tutorialViewLevelData.AddRange(tutorialViewData);

            _isEnableSubscription?.Dispose();
            _isEnableSubscription = _tutorialController.isEnable.Subscribe(ProcessOnEnableDisableTutorial);
        }

        private void RemoveDataFromController()
        {
            if (_tutorialController == null)
            {
                return;
            }

            foreach (var tutorialEvent in onTutorialShowEvents)
            {
                _tutorialController.onTutorialShowEvents.Remove(tutorialEvent);
            }

            foreach (var tutorialEvent in onTutorialHideEvents)
            {
                _tutorialController.onTutorialHideEvents.Remove(tutorialEvent);
            }

            foreach (var levelData in tutorialViewData)
            {
                _tutorialController.tutorialViewLevelData.Remove(levelData);
            }

            _isEnableSubscription?.Dispose();
            _isEnableSubscription = null;
        }

        private void ProcessOnEnableDisableTutorial(bool enable)
        {
            OnEnableDisableTutorial?.Invoke(enable, !enable);
        }

        public UniTask Reload()
        {
            _goNextStageOnceSources.Clear();

            if (isScene)
                Init();

            return UniTask.CompletedTask;
        }
    }
}
