using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Audio;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC
{
    public class ComplexObjectBuildingController
    {
        private readonly IComplexObjectController _complexObjectController;
        private readonly ITooltipController _tooltipController;
        private readonly UnitBaseController _unitBaseController;
        private readonly ObjectSpecialTagController _objectSpecialTagController;
        private readonly GameplaySceneReferences _gameplaySceneReferences;
        private readonly IAudioService _audioController;
        private readonly IGameResourcesSystem _gameResourcesSystem;
        private readonly IMovableObjectTaskManager _taskManager;
        private readonly IGameplayIntervalsController _intervalsController;
        private readonly IObjectViewController _objectViewController;

        public ComplexObjectBuildingController(IComplexObjectController complexObjectController,
            ITooltipController tooltipController,
            UnitBaseController unitBaseController,
            ObjectSpecialTagController objectSpecialTagController,
            GameplaySceneReferences gameplaySceneReferences,
            IAudioService audioController,
            IGameResourcesSystem gameResourcesSystem,
            MovableObjectController movableObjectController,
            IGameplayIntervalsController intervalsController,
            IObjectViewController objectViewController)
        {
            _complexObjectController = complexObjectController;
            _tooltipController = tooltipController;
            _unitBaseController = unitBaseController;
            _objectSpecialTagController = objectSpecialTagController;
            _gameplaySceneReferences = gameplaySceneReferences;
            _audioController = audioController;
            _gameResourcesSystem = gameResourcesSystem;
            _intervalsController = intervalsController;
            _objectViewController = objectViewController;
            _taskManager = movableObjectController.GetTaskManager();
        }

        public void Load()
        {
            _complexObjectController.ObjectViewAdded += AddObjectView;
            _complexObjectController.OnStartBuilding += StartBuilding;
            _complexObjectController.OnBuildObject += coc =>
                ChangeState(coc, coc.transitionStateData[coc.currentStateIndex].TransitionTo);
            _complexObjectController.OnDestroyObject += coc =>
                ChangeState(coc, coc.initialObjectView, false);
        }

        private void AddObjectView(ComplexObject coc)
        {
            coc.OnIntervalStart += IntervalStarted;
            coc.OnIntervalEnd += IntervalCompleted;
            coc.OnTryBuild += TryBuild;
            
            InitStageAutoBuild(coc);
        }

        // Re-entry guard for the whole check-then-act transition. Two rapid build triggers
        // (double click / UseBuildFirst auto-build) each used to read resources as sufficient
        // BEFORE either subtracted, then both awaited the path check and both subtracted the cost
        // + registered a build task — double-charging one upgrade and spawning two tasks. Claiming
        // transitionInProgress before the first resource read (and thus before the first await)
        // makes only one transition per COC ever in flight; the finally releases it on every path
        // — success, early-out, or exception — so a throw can never brick the COC.
        private async UniTask<bool> ChangeState(ComplexObject coc, StaticObjectView objectView, bool requireResources = true)
        {
            if (coc.transitionInProgress)
            {
                return false;
            }

            coc.transitionInProgress = true;

            try
            {
                return await ChangeStateInternal(coc, objectView, requireResources);
            }
            finally
            {
                coc.transitionInProgress = false;
            }
        }

        private async UniTask<bool> ChangeStateInternal(ComplexObject coc, StaticObjectView objectView, bool requireResources = true)
        {
            coc.nextObjectView = objectView;

            var buildingSettings = coc.transitionStateData[coc.currentStateIndex].BuildingSettings;

            if (!_complexObjectController.IsEnoughResources(coc, requireResources, out coc.spentResources)) 
            {
                _tooltipController.ShowResourceNotEnough(coc);
                coc.NotifyNotResources();
                Debug.Log("Not enough resources");
                return false;
            }

            if (buildingSettings.UnitTypeCounts.Count > 0)
            {
                if (!_complexObjectController.IsEnoughRelevantUnits(buildingSettings, out var relevantBases))
                {
                    _tooltipController.ShowResourceNotEnough(coc);
                    coc.NotifyNotResources();
                    Debug.Log("Not enough units");
                    return false;
                }

                var (anyPathWalkable, closestTransform) = 
                    await _unitBaseController.IsPathWalkable(coc.transform.position, relevantBases);
                

                if (!anyPathWalkable)
                {
                    Debug.Log("Path is blocked");
                    _unitBaseController.ShowPath(coc.transform.position, relevantBases).Forget();
                    _tooltipController.ShowNoPath(coc);
                    coc.NotifyNoPath();
                    return false;
                }
                
                var objectViewList = new List<ITaskObject>();
                var lastPoint = coc.Position;
                
                foreach (var tag in coc.transitionStateData[coc.currentStateIndex].BuildingSettings.TaskTypeTags)
                {
                    if (tag.TagType == TagType.Object)
                    {
                        var newPoint = _objectSpecialTagController.GetObjectWithTag(tag, lastPoint);
                        if (newPoint != null)
                        {
                            lastPoint = newPoint.Position;
                            objectViewList.Add(newPoint);
                            continue;
                        }

                        _tooltipController.ShowCantReachAnotherObjectTooltip(tag, coc.transform.position);
                        Debug.Log("Cant find object: " + tag);
                        return false;

                    }
                }
                
                coc.onRegisterTask?.Invoke();
                
                _gameplaySceneReferences.ResourceAmountSubtracted
                    .ShowResourcesAmounts(coc.spentResources, _gameplaySceneReferences.ResourcesView, 
                        coc.transform, Vector3.up)
                    .Forget();

                _audioController.PlaySfx(coc.CurrentObjectView.ObjectDataSO.TaskRegisterSound);
                
                objectViewList.Add(coc);
                
                coc.UnitsCameToCoc.Clear();
            
                foreach (var typeCount in coc.transitionStateData[coc.currentStateIndex].BuildingSettings.UnitTypeCounts)
                {
                    var typeCountCopy = new UnitTypeCount()
                    {
                        count = typeCount.count,
                        tagMode = typeCount.tagMode,
                        unitType = typeCount.unitType
                    };
                    typeCountCopy.count = 0;
                    coc.UnitsCameToCoc.Add(typeCountCopy);
                }
                
                // До списания: списание само дёргает обновление тултипа, и без флага он успел бы
                // перекраситься в красное «не хватает» — хотя за стройку уже заплачено.
                coc.SetInteractionPending(true);

                _gameResourcesSystem.SubtractResource(coc.spentResources);

                await _taskManager.RegisterNewTask(closestTransform, coc, objectViewList, buildingSettings);

                return true;
            }
            
            coc.onRegisterTask?.Invoke();

            _gameplaySceneReferences.ResourceAmountSubtracted
                    .ShowResourcesAmounts(coc.spentResources, 
                        _gameplaySceneReferences.ResourcesView, coc.transform, Vector3.up)
                    .Forget();

            coc.SetInteractionPending(true);

            _gameResourcesSystem.SubtractResource(coc.spentResources);

            StartBuilding(coc);

            return true;
        }
        
        /// <summary>
        /// Start changing object state to next stage.
        /// </summary>
        private void StartBuilding(ComplexObject coc)
        {
            if (coc.currentObjectView.CanProduceObjects && coc.currentObjectView.productionData.SpawnedCollectibleObject)
            {
                coc.currentObjectView.productionData.SpawnedCollectibleObject.CanInteract = false;
            }

            var nextStateIndex = coc.StateIndexes[coc.nextObjectView];
            
            StartBuildingInterval(coc, coc.nextObjectView, nextStateIndex);
        }
        
        /// <summary>
        /// Start interval to build object. Subscribe to start, cancel and complete interval
        /// </summary>
        /// <param name="objectView">The object to build.</param>
        /// <param name="transformedObjectTransitionTime">The specific parameters to interval (specific actions on
        /// interval start/complete and etc).</param>
        /// <param name="nextStateIndex">The index of the stage to which the object is being transferred.</param>
        private void StartBuildingInterval(
            ComplexObject coc,
            StaticObjectView objectView,
            int nextStateIndex)
        {
            var parameters = coc.transitionStateData[coc.currentStateIndex].GameplayIntervalGeneralParameters;
            parameters.DurationSeconds /= coc.InteractionSpeed;
            coc.currentIntervalId = _intervalsController.StartInterval(
                new GameplayIntervalSpecificParameters(
                    null,
                    coc.IntervalStarted,
                    null,
                    null,
                    null,
                    () => IntervalCancelled(coc),
                    null,
                    () => IntervalCompleted(coc, objectView, nextStateIndex), 
                    coc.CurrentMovableObjectTaskView,
                    coc.CurrentObjectView
                    ),
                parameters);
        }

        private void IntervalStarted(ComplexObject coc)
        {
            // Auto-build paths start intervals without a task; a throw here would leave the
            // interval repeating and isUsing/badges desynced.
            if (coc.CurrentTask?.Units != null)
            {
                foreach (var unit in coc.CurrentTask.Units)
                {
                    if (coc.InteractionTime > 0.01f)
                    {
                        unit.PlayAnimation(coc.InteractionType.ToString());
                    }
                    unit.StartUnitInteract();
                }
            }
            
            coc.transitionStateData[coc.currentStateIndex].GameplayIntervalSpecificParametersUlt
                .OnIntervalStarted?.Invoke();

            
            coc.isUsing = true;
            _taskManager.UpdateBadges();
        }
        
        private void IntervalCancelled(ComplexObject coc)
        {
            // The flag must not outlive the interval it describes: with it stuck true the
            // COC ignores all future arrivals and the next build can never start.
            coc.buildingStarted = false;

            if (coc.currentBuildingEffect != null)
            {
                coc.CurrentObjectView.gameObject.SetActive(false);
            }
 
            coc.transitionStateData[coc.currentStateIndex].GameplayIntervalSpecificParametersUlt
                .OnIntervalCanceled?.Invoke();

            _intervalsController.CancelInterval(coc.currentIntervalId);

            // Стройку прервали — COC снова свободен, тултип должен вернуться.
            // Последним, когда buildingStarted уже сброшен.
            coc.SetInteractionPending(false);
        }
        
        private void IntervalCompleted(ComplexObject coc, StaticObjectView objectView, int nextStateIndex)
        {
            var staticObject = objectView;
            
            if (staticObject.IsBase)
            {
                _unitBaseController.MoveMovableObjects(coc.CurrentObjectView, staticObject);
            }
            
            coc.transitionStateData[coc.currentStateIndex].TransitionFrom.gameObject.SetActive(false);
            
            if (coc.transitionStateData[coc.currentStateIndex].BuildingSettings.UnitTypeCounts.Count > 0
                && coc.CurrentTask?.Units != null)
            {
                foreach (var movableObject in coc.CurrentTask.Units)
                {
                    coc.CurrentObjectView.OnSpecialUnitsEndBuild?.Invoke(movableObject);
                    movableObject.EndUnitInteract();
                }
            }
            
            coc.transitionStateData[coc.currentStateIndex]
                .GameplayIntervalSpecificParametersUlt.OnIntervalCompleted?.Invoke();
            
            _complexObjectController.UpdateProductionResource(coc, coc.CurrentObjectView, objectView);
            
            coc.CurrentObjectView = objectView;
            coc.currentStateIndex = nextStateIndex;
            coc.CurrentObjectView.gameObject.SetActive(true);

            coc.buildingComplete = true;
            coc.buildingStarted = false;

            if (coc.currentBuildingEffect != null)
            {
                coc.currentBuildingEffect.SetActive(false);
            }

            coc.isUsing = false;
            // Mirror IntervalStarted: badges hidden while isUsing must be re-rendered,
            // otherwise the next task on this object never shows its badge.
            _taskManager.UpdateBadges();
            _complexObjectController.UpdateUpgradeMarkView(coc);
            InitStageAutoBuild(coc);

            // Строго ПОСЛЕ сброса buildingStarted/isUsing: тултип по этому событию сразу
            // спрашивает «занят ли COC», и стоя выше он видел бы стройку ещё идущей —
            // прятался бы снова и уже не возвращался, второго события ведь не будет.
            //
            // Раньше здесь тултип просто гасился: наводишь на фабрику, она достраивается —
            // и подсказка пропадает, хотя курсор никуда не уходил. Теперь сообщаем о смене
            // стадии, а тултип решает сам — обновить карточку или ничего не делать
            // (если наведено на другой объект).
            coc.SetInteractionPending(false);
            coc.NotifyInteractionStateChanged();

            // Здание — это COC, а не ObjectView, поэтому OnInteractionStateChanged у него
            // не срабатывает и панель выбора альтернативы сама о смене не узнает. Панель при этом
            // НЕ закрываем: достройка меняет путь и доступность, а не сам факт выбора.
            _objectViewController.RefreshAlternativeSelection();
        }
        
        
        /// <summary>
        /// Default primary action to COC. If only build action available -- register/cancel build task,
        /// otherwise it opens the action selection panel.
        /// </summary>
        private async void TryBuild(ComplexObject coc)
        {
            if (!_gameplaySceneReferences.GameplaySettings.CanCancelTaskDuringRunning 
                && _taskManager.IsTaskStarted(coc.CurrentTask == null ? coc.CurrentObjectView : coc))
                return;
            if (coc.transitionStateData[coc.currentStateIndex].BuildingSettings.TaskTypeTags.Length > 0)
            {
                var lastPoint = coc.Position;
                var blockedObjects = new List<ITaskObject>();

                foreach (var tag in coc.transitionStateData[coc.currentStateIndex].BuildingSettings.TaskTypeTags)
                {
                    if (tag.TagType != TagType.Object) continue;
                    var newPoint = _objectSpecialTagController.GetComplexObjectWithTag(tag, lastPoint);
                    if (newPoint is ComplexObject obj)
                    {
                        var canUse = !await obj.CanUse() &&
                                     ((obj.transitionStateData.Length > 1 && obj.currentStateIndex > 0) ||
                        obj.transitionStateData.Length <= 1);
                        if (!canUse)
                        {
                            blockedObjects.Add(newPoint);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    _tooltipController.ShowCantReachAnotherObjectTooltip(tag, coc.transform.position);
                }
                if (blockedObjects.Count > 0)
                    return;
            }
            
            if (coc.CurrentTask == null)
            {
                if (_objectViewController.TryCancelTask(coc.CurrentObjectView))
                {
                    return;
                }
            }
            // Cancel only a task that actually belongs to this COC: when the COC is a mere
            // waypoint of another object's task, CurrentTask points at that FOREIGN task and
            // cancelling it here killed someone else's order and refunded wrong resources.
            else if (ReferenceEquals(coc.CurrentTask.MainObjectView, coc))
            {
                if (_taskManager.CancelTask(coc.CurrentTask, coc.buildingStarted, coc.spentResources))
                {
                    _gameResourcesSystem.AddResource(coc.spentResources);
                    coc.CancellationTokenSource?.Cancel();
                    coc.buildingStarted = false;
                    coc.CancellationTokenSource = new CancellationTokenSource();

                    // Стройку отменили, ресурсы вернулись — тултип должен вернуться тоже.
                    coc.SetInteractionPending(false);

                    return;
                }
            }
            
            var actionData = _complexObjectController.GetActionData(coc);

            if (_complexObjectController.IsOnlyOneActionAvailable(coc, actionData))
            {
                return;
            }

            coc.ActionData.Value = actionData;
        }

        private void AutoBuildByAnotherObject(ComplexObject coc, ObjectView.ObjectView requiredObject)
        {
            coc.transitionStateData[coc.currentStateIndex].ObjectsToAutoBuild.Remove(requiredObject);

            if (coc.transitionStateData[coc.currentStateIndex].ObjectsToAutoBuild.Count == 0)
            {
                coc.nextObjectView = coc.transitionStateData[coc.currentStateIndex].TransitionTo;
                StartBuildingInterval(coc, coc.nextObjectView, coc.StateIndexes[coc.nextObjectView]);
            }
        }

        private void InitStageAutoBuild(ComplexObject coc)
        {
            foreach (var autoBuild in coc.transitionStateData[coc.currentStateIndex].ObjectsToAutoBuild)
            {
                autoBuild.onEndInteract.AddListener(() => AutoBuildByAnotherObject(coc, autoBuild));
            }
        }
    }
}