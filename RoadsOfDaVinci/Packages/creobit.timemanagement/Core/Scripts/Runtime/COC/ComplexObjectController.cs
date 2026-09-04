using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Audio;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using ObservableCollections;
using R3;
using UnityEngine;
using VContainer;
using static _8floor.TimeManagement.Core.Scripts.Runtime.Utils.RuntimeConstants.Enums;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC
{
    public class ComplexObjectController : IComplexObjectController
    {
        private readonly ITooltipController _tooltipController;
        private UnitBaseController _unitBaseController;
        private readonly IGameResourcesSystem _gameResourcesSystem;
        private ObjectSpecialTagController _specialTagController;
        private readonly GameplaySceneReferences _sceneReferences;
        private readonly IAudioService _audioController;
        private MovableObjectController _movableObjectController;
        private readonly IGameplayIntervalsController _intervalsController;
        private readonly IObjectViewController _objectViewController;
        private IMovableObjectTaskManager _taskManager;

        private const float MaxQueuedRetargetShift = 1f;
        private const float MaxRunningRetargetShift = 0.1f;

        private ComplexObjectBuildingController _buildingController;
        
        private readonly List<ComplexObject> _objectViews = new();
        
        public event Action<ComplexObject> ObjectViewAdded;
        public event Action<ComplexObject> OnStartBuilding;
        public event Action<ComplexObject> OnBuildObject;
        public event Action<ComplexObject> OnDestroyObject;

        [Inject]
        public ComplexObjectController(ITooltipController tooltipController,
            IGameResourcesSystem gameResourcesSystem,
            GameplaySceneReferences gameplaySceneReferences,
            IAudioService audioController,
            IGameplayIntervalsController intervalsController,
            IObjectViewController objectViewController)
        {
            _tooltipController = tooltipController;
            _gameResourcesSystem = gameResourcesSystem;
            _sceneReferences = gameplaySceneReferences;
            _audioController = audioController;
            _intervalsController = intervalsController;
            _objectViewController = objectViewController;
            
        }

        public UniTask Load()
        {
            _specialTagController = _objectViewController.GetObjectSpecialTagController();
            _unitBaseController = _objectViewController.GetUnitBaseController();
            _movableObjectController = _objectViewController.GetMovableObjectController();
            _taskManager = _movableObjectController.GetTaskManager();

            _buildingController = new ComplexObjectBuildingController(this, _tooltipController, _unitBaseController,
                _specialTagController, _sceneReferences, _audioController, _gameResourcesSystem, 
                _movableObjectController, _intervalsController, _objectViewController);
            
            _buildingController.Load();
            
            _gameResourcesSystem.Resources.ObserveReplace().Subscribe(_ =>
            {
                foreach (var objectView in _objectViews)
                {
                    UpdateUpgradeMarkView(objectView);
                }
                
            });
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            
        }
        
        public void AddObjectView(ComplexObject coc)
        {
            _objectViews.Add(coc);
            
            coc.OnBuild += BuildObject;
            coc.OnDestroy += DestroyObject;
            coc.OnInteract += Interact;
            coc.OnPerformAction += PerformAction;
            
            SetInitialObjectsState(coc);
            InitializeActions(coc);
            _tooltipController.InitTooltip(coc);
            
            ObjectViewAdded?.Invoke(coc);
        }
        
        private void SetInitialObjectsState(ComplexObject coc)
        {
            for (var i = 0; i < coc.transitionStateData.Length; i++)
            {
                coc.StateIndexes.Add(coc.transitionStateData[i].TransitionFrom, i);
                
                coc.transitionStateData[i].TransitionFrom.GetComponent<Collider>().enabled = false;
                
                if (coc.transitionStateData[i].TransitionFrom.Equals(coc.initialObjectView))
                {
                    coc.currentStateIndex = i;
                    
                    coc.initialObjectView.gameObject.SetActive(true);
                    
                    continue;
                }
                
                coc.transitionStateData[i].TransitionFrom.gameObject.SetActive(false);
            }
            
            UpdateUpgradeMarkView(coc);
        }
        
        private void InitializeActions(ComplexObject coc)
        {
            var assembly = Assembly.GetAssembly(typeof(CocAction));
    
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass 
                    || type.IsAbstract
                    || !typeof(CocAction).IsAssignableFrom(type))
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is not CocAction concreteType)
                {
                    continue;
                }
        
                concreteType.SetController(coc);
        
                coc.CocActions.Add(concreteType.CocActionType, concreteType);
            }
        }

        /// <summary>
        /// Check if only one action available. If it true, call this action
        /// </summary>
        /// <param name="actionData">The action data with info to actions to check.</param>
        /// <returns>Is only one action available</returns>
        public bool IsOnlyOneActionAvailable(ComplexObject coc, CocActionData actionData)
        {
            if (coc.UseBuildFirst && coc.CocActions[CocActionType.Build].IsAvailable())
            {
                coc.CocActions[CocActionType.Build].Action();
                return true;
            }
            
            var availableActionCount = 0;

            var onlyAction = CocActionType.Interact;

            foreach (var action in actionData.AvailableActions)
            {
                if (!action.Value)
                {
                    continue;
                }

                availableActionCount++;

                onlyAction = action.Key;
            }

            if (availableActionCount > 1)
            {
                return false;
            }

            coc.CocActions[onlyAction].Action();
            
            return true;
        }
        
        public async void Interact(ComplexObject coc, MovableObjectView unit = null)
        {
            var stageUnitTypeCounts = coc.transitionStateData[coc.currentStateIndex].BuildingSettings.UnitTypeCounts;

            // A build is already running: late arrivals belong to it and are released by
            // IntervalCompleted; counting them would poison the next stage's buckets.
            if (coc.buildingStarted)
            {
                return;
            }

            if (coc.UnitsCameToCoc is null || coc.UnitsCameToCoc.Count != stageUnitTypeCounts.Count)
            {
                coc.UnitsCameToCoc?.Clear();

                foreach (var typeCount in stageUnitTypeCounts)
                {
                    var typeCountCopy = new UnitTypeCount()
                    {
                        count = 0,
                        tagMode = typeCount.tagMode,
                        unitType = typeCount.unitType
                    };
                    coc.UnitsCameToCoc.Add(typeCountCopy);
                }
            }

            // Tagged buckets are matched first so a specialist is not absorbed by an
            // untagged catch-all bucket; buckets at capacity are skipped so overlapping
            // matches spill into the next unfilled bucket.
            if (!TryCountArrival(coc, unit, stageUnitTypeCounts, tagged: true))
            {
                TryCountArrival(coc, unit, stageUnitTypeCounts, tagged: false);
            }

            // Start building only when EVERY unit-type bucket is filled. Starting on the
            // first filled bucket kicked off the interval while other units were still
            // walking and could start a second concurrent interval for the same transition.
            var allTypesArrived = true;

            for (var index = 0; index < coc.UnitsCameToCoc.Count; index++)
            {
                var typeCountCurrent = coc.UnitsCameToCoc[index];
                var typeCountOrigin = coc.transitionStateData[coc.currentStateIndex].BuildingSettings.UnitTypeCounts[index];

                if (typeCountCurrent.count < typeCountOrigin.count)
                {
                    allTypesArrived = false;
                    break;
                }
            }

            if (allTypesArrived && coc.UnitsCameToCoc.Count > 0 && !coc.buildingStarted)
            {
                coc.buildingComplete = false;
                coc.buildingStarted = true;
                coc.UnitsCameToCoc.Clear();

                OnStartBuilding?.Invoke(coc);

                await ChangingState(coc);
            }
        }

        private static bool TryCountArrival(ComplexObject coc, MovableObjectView unit,
            List<UnitTypeCount> stageUnitTypeCounts, bool tagged)
        {
            for (var index = 0; index < coc.UnitsCameToCoc.Count; index++)
            {
                var unitTypeCount = coc.UnitsCameToCoc[index];
                var bucketIsTagged = unitTypeCount.unitType != null && unitTypeCount.unitType.Length > 0;

                if (bucketIsTagged != tagged || unitTypeCount.count >= stageUnitTypeCounts[index].count)
                {
                    continue;
                }

                // Match arrivals with the same tagMode-aware rule used to SELECT the units
                // (GetFreeUnits). The old All-tags check silently dropped valid arrivals, so
                // the bucket never filled and the units stood at the object forever.
                var hasUnit = !bucketIsTagged
                              || (unit != null && unit.MovableObjectDataSO.ObjectTypeTags
                                  .Contains(unitTypeCount.tagMode, unitTypeCount.unitType));

                if (hasUnit)
                {
                    unitTypeCount.count++;
                    return true;
                }
            }

            return false;
        }

        public void BuildObject(ComplexObject coc)
        {
            if (coc.transitionStateData[coc.currentStateIndex].TransitionTo != null)
            {
                OnBuildObject?.Invoke(coc);
            }
        }
        
        public void DestroyObject(ComplexObject coc)
        {
            OnDestroyObject?.Invoke(coc);
        }
        
        private async UniTask ChangingState(ComplexObject coc)
        {
            coc.CancellationTokenSource = new CancellationTokenSource();

            await UniTask.WaitUntil(() => coc.buildingComplete, cancellationToken: coc.CancellationTokenSource.Token);
        }
        
        public void PerformAction(ComplexObject coc, CocActionType cocActionType)
        {
            if (!coc.CocActions.TryGetValue(cocActionType, out var cocAction))
            {
                return;
            }
            
            cocAction.Action();
        }
        
        /// <summary>
        /// Check is enough resources to build next stage and out fill need resources.
        /// </summary>
        /// <param name="requireResources">Function always return true if not require resources.</param>
        /// <param name="resources">Out fill resources to build next stage.</param>
        /// <returns>True if is enough resources, otherwise false.</returns>
        public bool IsEnoughResources(ComplexObject coc, bool requireResources, out ResourceAmount[] resources)
        {
            resources = coc.GetResourceCost();

            if (!requireResources)
            {
                return true;
            }
            
            return resources.Length == 0 || _gameResourcesSystem.IsEnoughResources(resources);
        }

        private List<StaticObjectView> GetRelevantBases(UnitTypeCount typeCount)
        {
            var relevantBases = _unitBaseController
                .GetRelevantBases(typeCount.unitType, typeCount.tagMode);
            return relevantBases.relevantUnitsCount >= typeCount.count ? relevantBases.relevantBases : null;
        }
        
        /// <summary>
        /// Check enough relevant units and fill relevant home points.
        /// </summary>
        /// <param name="relevantBases">Home points to fill.</param>
        /// <returns>True if enough, otherwise false.</returns>
        public bool IsEnoughRelevantUnits(BuildingSettings settings, out List<StaticObjectView> relevantBases)
        {
            relevantBases = new List<StaticObjectView>();

            foreach (var typeCount in settings.UnitTypeCounts)
            {
                var typeBases = GetRelevantBases(typeCount);

                if (typeBases == null)
                {
                    Log.Gameplay.Info("Not enough relevant units");
                    return false;
                }
                
                relevantBases.AddRange(typeBases);
            }

            return relevantBases.Count > 0;
        }
        
        public void UpdateUpgradeMarkView(ComplexObject coc)
        {
            if (!coc.showUpgradeMark)
            {
                if (coc.upgradeMarkObject != null)
                {
                    coc.upgradeMarkObject.gameObject.SetActive(false);
                }

                return;
            }
            
            if (coc.upgradeMarkObject == null)
                return;

            if (coc.CanBuildObject() && coc.transitionStateData[coc.currentStateIndex].BuildingSettings.UseUpgradeMark)
            {
                coc.upgradeMarkObject.gameObject.SetActive(true);
                coc.upgradeMarkObject.sprite = IsEnoughResources(coc, true, out _)
                    ? coc.upgradeMarkEnable
                    : coc.upgradeMarkDisable;
            }
            else
            {
                coc.upgradeMarkObject?.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Create action data with info which actions is available.
        /// </summary>
        /// <returns>Action data with info which actions is available.</returns>
        public CocActionData GetActionData(ComplexObject coc)
        {
            var availableActions = new Dictionary<CocActionType, bool>();
            
            foreach (CocActionType cocActionType in Enum.GetValues(typeof(CocActionType)))
            {
                availableActions.Add(cocActionType, coc.CocActions[cocActionType].IsAvailable());
            }

            return new CocActionData()
            {
                ComplexObjectView = coc,
                AvailableActions = availableActions,
                CocPosition = coc.transform.position
            };
        }

        public async UniTask UpdateProductionResource(ComplexObject coc, 
            StaticObjectView currentObject, StaticObjectView newObject)
        {
            var productionData = currentObject.productionData;
            var newProductionData = newObject.productionData;

            ObjectView.ObjectView activeProduct = null;

            if (productionData.UseObjectSpawn && productionData.SpawnedSpitObject)
            {
                foreach (var state in coc.transitionStateData)
                {
                    var stateProductionData = state.TransitionFrom.productionData;
                    var product = stateProductionData.SpawnedSpitObject;

                    if (product && product.gameObject.activeSelf)
                    {
                        activeProduct = product;
                        break;
                    }
                }

                if (!newProductionData.UseObjectSpawn || !newProductionData.SpawnedSpitObject) return;

                // Старый объект уже скрыт, но его интервал продолжает тикать и потом выдаёт
                // продукт в невидимый объект: тот остаётся activeSelf и позже подхватывается
                // как activeProduct. Гасим в любом случае — объект заменён.
                currentObject.StopProduction();

                if (!activeProduct || !activeProduct.gameObject.activeSelf)
                {
                    // Невыкупленного продукта нет — переносить нечего, просто запускаем новое производство.
                    newObject.StartProduction();
                    return;
                }

                switch (productionData.UpgradedResourceBehaviour)
                {
                    case UpgradedResourceBehaviours.DoNothing:
                        await WaitUntilObjectState(coc, activeProduct.gameObject, false);
                        newObject.StartProduction();
                        break;
                    case UpgradedResourceBehaviours.Upgrade:
                        var newProduct = newProductionData.SpawnedSpitObject;

                        // Строго ДО TryRetireProduct: пока задача жива, её можно перевести на новый
                        // продукт целиком — с местом в очереди и номером на галочке. После отмены
                        // остаётся только оформить заказ заново, и он встанет в конец очереди.
                        var taskRetargeted = TryRetargetCollectTask(activeProduct, newProduct);
                        var productWasOrdered = !taskRetargeted && HasCollectTask(activeProduct);

                        // Апгрейднутый продукт выдаём ТОЛЬКО если старый удалось снять вместе с
                        // заказом на его сбор. Иначе за один произведённый ресурс игрок получит два:
                        // старый доедет с юнитом, новый будет лежать на сцене.
                        if (!TryRetireProduct(activeProduct))
                        {
                            newObject.StartProduction();
                            break;
                        }

                        ActivateProducedObject(newProduct);
                        // StartProduction ждёт, пока продукт не заберут, поэтому новый интервал
                        // стартует ровно после сбора перенесённого ресурса.
                        newObject.StartProduction();

                        if (productWasOrdered)
                        {
                            RestoreCollectTask(coc, newProduct).Forget();
                        }
                        break;
                    case UpgradedResourceBehaviours.Destroy:
                        TryRetireProduct(activeProduct);
                        newObject.StartProduction();
                        break;
                }
            }
            else
            {
                if (!newProductionData.UseObjectSpawn 
                    || !newProductionData.SpawnedSpitObject
                    || !productionData.SpawnedSpitObject)
                {
                    return;
                }

                newObject.StartProduction();
            }
        }
        
        /// <summary>
        /// Убирает со сцены продукт, который забирает апгрейд, вместе со всем, что ещё способно его
        /// выдать: заказом на сбор и незавершённым взаимодействием.
        ///
        /// Одного SetActive(false) мало. Задача сбора держит ссылку на ObjectView, а не на его
        /// GameObject: юнит доходит до координат уже спрятанного продукта, ObjectView.Interact
        /// пускает его по одному CanInteract (активность объекта там не проверяется), а таймер
        /// взаимодействия крутится на PlayerLoop и на выключенном объекте — сбор доигрывается
        /// вхолостую и старый ресурс выдаётся вдобавок к перенесённому апгрейдом.
        ///
        /// Возвращает false, если сбор уже начался и отменить его нельзя (CanCancelTaskDuringInteraction):
        /// тогда старый ресурс достаётся игроку честно и подменять его апгрейднутым нельзя —
        /// ресурсов снова стало бы два.
        /// </summary>
        private bool TryRetireProduct(ObjectView.ObjectView product)
        {
            if (product == null)
            {
                return true;
            }

            if (HasCollectTask(product) && !_objectViewController.TryCancelTask(product))
            {
                return false;
            }

            // Строго ДО SetActive(false): снятый CanInteract — единственное, что остановит юнита,
            // который к продукту уже идёт по задаче, не найденной выше (продукт как промежуточная
            // точка чужой последовательности).
            product.CanInteract = false;
            product.SetPrimaryActionState(false);
            product.ResetInteractionState();
            product.gameObject.SetActive(false);
            product.NotifyInteractionStateChanged();

            return true;
        }

        /// <summary>
        /// Переводит заказ на сбор со старого продукта на апгрейднутый: место в очереди, номер
        /// галочки и назначенные юниты сохраняются, а уже вышедший юнит доворачивает на новую точку
        /// вместо разворота домой.
        ///
        /// Работает, только пока точка почти не сдвинулась — проходимость при переводе заново не
        /// считается. Идущему юниту допуск строже: он не сдаётся и перезапрашивает путь бесконечно,
        /// поэтому недостижимая цель оставила бы его в Work навсегда. Незапущенную задачу движок
        /// перед стартом проверит сам и отменит, если пути нет, — там можно свободнее.
        /// Не прошли по допуску — заказ отменяется и оформляется заново, с проверкой пути.
        /// </summary>
        private bool TryRetargetCollectTask(ObjectView.ObjectView product, ObjectView.ObjectView newProduct)
        {
            if (_taskManager == null || product == null || newProduct == null)
            {
                return false;
            }

            // Обе точки: к Position юнит идёт, по interactionPosition считалась проходимость.
            var shift = Mathf.Max(Vector3.Distance(product.Position, newProduct.Position),
                Vector2.Distance(product.interactionPosition, newProduct.interactionPosition));

            var maxShift = _taskManager.GetTaskProgress(product) == TaskProgress.InProgress
                ? MaxRunningRetargetShift
                : MaxQueuedRetargetShift;

            if (shift > maxShift)
            {
                return false;
            }

            return _taskManager.TryRetargetTask(product, newProduct, product.interactionStarted);
        }

        private bool HasCollectTask(ObjectView.ObjectView product)
        {
            if (product == null)
            {
                return false;
            }

            return _taskManager?.GetTaskProgress(product) is TaskProgress.Queued or TaskProgress.InProgress;
        }

        /// <summary>
        /// Возвращает на апгрейднутый продукт заказ на сбор, снятый вместе со старым: для игрока это
        /// тот же ресурс, лежащий на том же месте, и терять галочку из-за смены уровня он не должен.
        ///
        /// Запасной путь для случаев, когда перевод задачи невозможен (продукт нового уровня стоит
        /// в другом месте): оформляем как обычный клик — с проверкой пути и ресурсов, но заказ
        /// встаёт в конец очереди. Основной путь — TryRetargetCollectTask.
        /// </summary>
        private async UniTask RestoreCollectTask(ComplexObject coc, ObjectView.ObjectView product)
        {
            // Панель выбора альтернативы открывается только по клику игрока: сами её не дёргаем.
            if (product == null || product.ObjectDataSO.AlternativeInteractions.Count > 1)
            {
                return;
            }

            // COC включает объект новой стадии уже после UpdateProductionResource, а регистрация
            // задачи требует активной иерархии.
            await WaitUntilObjectState(coc, product.gameObject, true, true);

            if (product == null || !product.gameObject.activeInHierarchy || !product.CanInteract)
            {
                return;
            }

            product.TryRegisterTask();
        }

        /// <summary>
        /// Повторяет выдачу продукта из ProductionController.IntervalCompleted. Одного SetActive мало:
        /// после сбора у продукта сброшены CanInteract и состояние взаимодействия, и перенесённый
        /// апгрейдом ресурс нельзя забрать.
        /// </summary>
        private void ActivateProducedObject(ObjectView.ObjectView producedObject)
        {
            producedObject.gameObject.SetActive(true);
            producedObject.CanInteract = producedObject.ObjectDataSO.CanInteract;
            producedObject.SetPrimaryActionState(producedObject.ObjectDataSO.CanReactToPrimaryAction);
            producedObject.ResetInteractionState();
        }

        private async UniTask WaitUntilObjectState(ComplexObject coc, GameObject objectView, bool state, bool useActiveInHierarchy = false)
        {
            try
            {
                await UniTask.WaitUntil(() =>
                {
                    if (!objectView || !objectView.gameObject)
                    {
                        return true;
                    }

                    if (useActiveInHierarchy)
                    {
                        return objectView.activeInHierarchy == state;
                    }

                    return objectView.activeSelf == state;
                }, cancellationToken: coc.CancellationTokenSource.Token);

                return;
            }
            catch (Exception)
            {
                return;
            }
        }
    }
}