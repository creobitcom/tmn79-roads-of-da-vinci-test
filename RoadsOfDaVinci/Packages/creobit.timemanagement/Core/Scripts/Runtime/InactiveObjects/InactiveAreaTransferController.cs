using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.StandingPoint;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects
{
    public class InactiveAreaTransferController : IInactiveAreaTransferController, IDisposable
    {
        private readonly IObjectViewController _objectViewController;
        private readonly List<InactiveObjectRefs> _transferableAreas = new();
        private readonly IBubbleController _bubbleController;
        private readonly IInactiveObjectController _inactiveObjectController;
        private readonly IGameResourcesSystem _gameResourcesSystem;

        private bool _isTransferring;

        private CancellationTokenSource _disposeCts = new CancellationTokenSource();

        public InactiveAreaTransferController(
            IObjectViewController objectViewController,
            IBubbleController bubbleController,
            IInactiveObjectController inactiveObjectController,
            IGameResourcesSystem gameResourcesSystem)
        {
            _objectViewController = objectViewController;
            _bubbleController = bubbleController;
            _inactiveObjectController = inactiveObjectController;
            _gameResourcesSystem = gameResourcesSystem;
        }

        public UniTask Load() => UniTask.CompletedTask;

        public void Dispose()
        {
            _transferableAreas.Clear();
            _isTransferring = false;

            if (_disposeCts != null)
            {
                _disposeCts.Cancel();
                _disposeCts.Dispose();
                _disposeCts = null;
            }
        }


        public void RegisterTransferable(InactiveObjectRefs inactiveArea)
        {
            if (!_transferableAreas.Contains(inactiveArea))
            {
                _transferableAreas.Add(inactiveArea);
            }
        }

        public bool TryStartTransfer(StandingPointView targetPoint)
        {
            if (_isTransferring) return false;

            var targetArea = targetPoint.LinkedInactiveArea;
            if (targetArea == null || !targetArea.IsTransferable || targetArea.State)
                return false;

            if (!TryFindSource(out var sourceArea, out var sourcePoint, out var carrierUnit))
                return false;

            RunTransfer(carrierUnit, sourceArea, sourcePoint, targetArea, targetPoint).Forget();
            return true;
        }

        private bool TryFindSource(
            out InactiveObjectRefs sourceArea,
            out StandingPointView sourcePoint,
            out MovableObjectView carrierUnit)
        {
            _transferableAreas.RemoveAll(a => a == null);

            foreach (var area in _transferableAreas)
            {
                if (!area.IsTransferable || !area.State) continue;
                if (area.LinkedStandingPoint == null) continue;
                if (!area.LinkedStandingPoint.IsOccupied) continue;

                sourceArea = area;
                sourcePoint = area.LinkedStandingPoint;
                carrierUnit = sourcePoint.OccupyingUnit;
                return true;
            }

            sourceArea = null;
            sourcePoint = null;
            carrierUnit = null;
            return false;
        }

        private async UniTask RunTransfer(
            MovableObjectView carrierUnit,
            InactiveObjectRefs sourceArea,
            StandingPointView sourcePoint,
            InactiveObjectRefs targetArea,
            StandingPointView targetPoint)
        {
            _isTransferring = true;

            sourceArea.IsExternallyAnimated = true;
            targetArea.IsExternallyAnimated = true;

            var localCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token, _disposeCts.Token);
            CancellationTokenSource pulseCts = null;

            try
            {
                var pathFindable = _objectViewController.GetMovableObjectController().GetPathFindable();
                var seeker = carrierUnit.GetComponent<Seeker>();

                var (isWalkable, _, _) = await pathFindable.IsPathWalkable(
                    carrierUnit.transform.position,
                    targetPoint.interactionPosition,
                    seeker);

                if (!isWalkable)
                {
                    targetPoint.TryRegisterTask(carrierUnit);
                    return;
                }

                await UniTask.Yield();

                var sourceLightTransform = sourceArea.lightSprite.transform;
                var originalScale = sourceLightTransform.localScale;
                var targetRadius = ((SphereCollider)targetArea.mainCollider).radius;
                var targetLightSize = targetRadius / 5 * 1.6f;
                var targetOriginalScale = new Vector3(targetLightSize, targetLightSize, targetLightSize);
                var carrierScale = Vector3.one * sourceArea.CarrierLightScale;
                var duration = sourceArea.LightExpandDuration;
                
                if (sourceArea.EvacuationBubbleSequence != null)
                {
                    _bubbleController?.ShowSequence(sourceArea.EvacuationBubbleSequence, carrierUnit.transform);
                }
                
                var evacuated = await EvacUnitsFromAreaAsync(sourceArea, carrierUnit);

                if (evacuated.Count > 0)
                {
                    const float evacTimeout = 20f;
                    var evacWaited = 0f;
    
                    var allAreas = _inactiveObjectController.InactiveObjects; 

                    while (!linkedCts.IsCancellationRequested && 
                           sourceArea != null && 
                           sourceArea.mainCollider != null &&
                           evacuated.Any(u => 
                           {
                               if (u == null || u.State.Value != UnitState.ReturnHome) 
                                   return false;

                               var stillInsideDyingArea = IsUnitInsideArea(u, sourceArea.mainCollider);
                               var isSafeInOverlap = IsSafeInOtherArea(u.transform.position, sourceArea, allAreas);

                               return stillInsideDyingArea && !isSafeInOverlap;
                           }))
                    {
                        evacWaited += Time.deltaTime;
                        if (evacWaited > evacTimeout) break;
                        
                        await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                    }
                }
                
                _bubbleController?.HideCurrent();

                sourcePoint.FreePoint();
                if (carrierUnit != null)
                {
                    carrierUnit.CurrentStandingPoint = null;
                }

                targetPoint.TryRegisterTask(carrierUnit);

                const float taskWaitTimeout = 3f;
                float taskWaited = 0f;

                while (carrierUnit != null && carrierUnit.State.Value != UnitState.Work)
                {
                    taskWaited += Time.deltaTime;
                    if (taskWaited > taskWaitTimeout) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }

                if (carrierUnit == null) return;

                var carrierLightObj = new GameObject("CarrierLight");
                carrierLightObj.transform.SetParent(null);
                carrierLightObj.transform.position = carrierUnit.transform.position + new Vector3(0f, 0f, 50f);
                carrierLightObj.transform.localScale = Vector3.zero;
                carrierLightObj.layer = sourceArea.lightSprite.gameObject.layer;

                var sr = carrierLightObj.AddComponent<SpriteRenderer>();
                sr.sprite = sourceArea.lightSprite.sprite;
                sr.color = Color.white;
                sr.material = sourceArea.lightSprite.material;
                sr.sortingLayerName = "FogLight";
                sr.sortingOrder = 0;

                var sg = carrierLightObj.AddComponent<SortingGroup>();
                sg.sortingLayerName = "FogLight";
                sg.sortingOrder = 0;

                TrackUnitPosition(carrierUnit, carrierLightObj, linkedCts.Token).Forget();

                await AnimateCrossFade(
                    from: sourceLightTransform,
                    to: carrierLightObj.transform,
                    fromScale: originalScale,
                    toScale: carrierScale,
                    duration: duration,
                    token: linkedCts.Token);

                pulseCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                PulseCarrierLight(carrierLightObj.transform, carrierScale, pulseCts.Token).Forget();

                if (sourceArea != null && sourceArea.lightSprite != null)
                {
                    sourceArea.lightSprite.gameObject.SetActive(false);
                    sourceLightTransform.localScale = originalScale;
                }

                const float arrivalTimeout = 30f;
                float arrivalWaited = 0f;

                while (targetPoint != null && carrierUnit != null &&
                       targetPoint.OccupyingUnit != carrierUnit && carrierUnit.State.Value == UnitState.Work)
                {
                    arrivalWaited += Time.deltaTime;
                    if (arrivalWaited > arrivalTimeout) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }

                if (sourceArea != null)
                {
                    sourceArea.SetState(false);
                }

                pulseCts?.Cancel();

                if (targetPoint != null && targetArea != null && targetPoint.OccupyingUnit == carrierUnit)
                {
                    targetArea.SetState(true);
                    targetArea.lightSprite.gameObject.SetActive(true);
                    targetArea.lightSprite.transform.localScale = Vector3.zero;

                    await AnimateCrossFade(
                        from: carrierLightObj.transform,
                        to: targetArea.lightSprite.transform,
                        fromScale: carrierScale,
                        toScale: targetOriginalScale,
                        duration: duration,
                        token: linkedCts.Token);
                }

                Object.Destroy(carrierLightObj);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[InactiveAreaTransferController] Error during transfer: {e}");
            }
            finally
            {
                localCts.Cancel();
                localCts.Dispose();
                linkedCts.Dispose();

                if (pulseCts != null)
                {
                    pulseCts.Cancel();
                    pulseCts.Dispose();
                }

                if (sourceArea != null)
                {
                    sourceArea.IsExternallyAnimated = false;
                }

                if (targetArea != null)
                {
                    targetArea.IsExternallyAnimated = false;
                }

                _isTransferring = false;
            }
        }

        private async UniTask<List<MovableObjectView>> EvacUnitsFromAreaAsync(
            InactiveObjectRefs area,
            MovableObjectView carrierUnit)
        {
            var movableController = _objectViewController.GetMovableObjectController();
            var taskManager = movableController.GetTaskManager() as MovableObjectTaskManager;
            var objController = _objectViewController as ObjectViewController;
            var allUnits = movableController.GetUnits();

            var allAreas = Object.FindObjectsOfType<InactiveObjectRefs>(true);
            var evacuated = new List<MovableObjectView>();
            var tasksToCancel = new HashSet<MovableObjectTask>();

            foreach (var task in taskManager.Tasks.Values)
            {
                bool destInDanger = IsPointInsideCollider(task.Destination, area.mainCollider) ||
                                    (task.CurrentTaskObject != null &&
                                     area.IncludedObjects.Contains(task.CurrentTaskObject));

                if (destInDanger)
                {
                    bool destSafe = IsSafeInOtherArea(task.Destination, area, allAreas);
                    if (!destSafe)
                    {
                        tasksToCancel.Add(task);
                    }
                }
            }

            foreach (var unit in allUnits)
            {
                if (unit == carrierUnit || !unit.gameObject.activeInHierarchy) continue;

                bool unitInDanger = IsUnitInsideArea(unit, area.mainCollider);
                if (unitInDanger)
                {
                    bool unitSafe = IsSafeInOtherArea(unit.transform.position, area, allAreas);
                    if (!unitSafe)
                    {
                        var task = taskManager.GetTaskByUnit(unit);
                        if (task != null) tasksToCancel.Add(task);
                    }
                }
            }

            foreach (var task in tasksToCancel)
            {
                if (task.MainObjectView is ComplexObject taskComplexObject && !taskComplexObject.buildingStarted)
                {
                    if (taskManager.CancelTask(task, interactionStarted: false,
                            inputResources: taskComplexObject.spentResources))
                    {
                        _gameResourcesSystem.AddResource(taskComplexObject.spentResources);
                        taskComplexObject.SetInteractionPending(false);
                    }

                    continue;
                }

                var currentObj = task.MainObjectView as ObjectView.ObjectView;
                if (currentObj != null)
                {
                    objController?.TryRestoreInputResources(currentObj);
                    currentObj._cancellationTokenSource?.Cancel();
                    currentObj.ResetInteractionState();
                }

                taskManager.CancelTask(task, interactionStarted: false, inputResources: null);
            }

            foreach (var unit in allUnits)
            {
                if (unit == carrierUnit || !unit.gameObject.activeInHierarchy) continue;

                bool unitInDanger = IsUnitInsideArea(unit, area.mainCollider);
                if (unitInDanger)
                {
                    bool unitSafe = IsSafeInOtherArea(unit.transform.position, area, allAreas);
                    if (!unitSafe)
                    {
                        evacuated.Add(unit);
                        unit.returnHomeTokenSource?.Cancel();

                        if (unit.CurrentStandingPoint != null)
                        {
                            unit.CurrentStandingPoint.FreePoint();
                            unit.CurrentStandingPoint = null;
                        }

                        if (unit.State.Value == UnitState.Idle)
                        {
                            unit.State.Value = UnitState.ReturnHome;

                            Action<MovableObjectView> onEndMove = null;
                            onEndMove = (u) =>
                            {
                                if (u != null && u.MovableObject != null)
                                {
                                    u.MovableObject.EndMove -= onEndMove;
                                }

                                if (u != null)
                                {
                                    u.State.Value = UnitState.Idle;
                                }
                            };

                            movableController.MoveToHomePoint(unit, onEndMove).Forget();
                        }
                    }
                }
            }

            return evacuated;
        }

        private bool IsSafeInOtherArea(Vector3 position, InactiveObjectRefs areaToTurnOff,
            IEnumerable<InactiveObjectRefs> allAreas) 
        {
            foreach (var area in allAreas)
            {
                if (area == areaToTurnOff) continue;

                if (area.State && IsPointInsideCollider(position, area.mainCollider))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsPointInsideCollider(Vector3 point, Collider col)
        {
            if (col == null) return false;

            if (col is SphereCollider sphere)
            {
                var worldCenter = col.transform.TransformPoint(sphere.center);
                var worldRadius = sphere.radius * Mathf.Max(
                    col.transform.lossyScale.x,
                    col.transform.lossyScale.y,
                    col.transform.lossyScale.z);

                return Vector3.Distance(point, worldCenter) <= worldRadius;
            }

            return col.bounds.Contains(point);
        }

        private static bool IsUnitInsideArea(MovableObjectView unit, Collider col)
        {
            if (unit == null || col == null) return false;
            return IsPointInsideCollider(unit.transform.position, col);
        }

        private const float CarrierPulseAmplitude = 0.07f;
        private const float CarrierPulseSpeed = 3.4f;

        private static async UniTaskVoid PulseCarrierLight(
            Transform light,
            Vector3 baseScale,
            CancellationToken token)
        {
            var elapsed = 0f;

            try
            {
                while (light != null)
                {
                    elapsed += Time.deltaTime;
                    light.localScale = baseScale * (1f + CarrierPulseAmplitude * Mathf.Sin(elapsed * CarrierPulseSpeed));

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static async UniTask TrackUnitPosition(
            MovableObjectView unit,
            GameObject light,
            CancellationToken token)
        {
            while (!token.IsCancellationRequested && light != null && unit != null)
            {
                light.transform.position = unit.transform.position + new Vector3(0f, 0f, 50f);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private static async UniTask AnimateCrossFade(
            Transform from, Transform to,
            Vector3 fromScale, Vector3 toScale,
            float duration, CancellationToken token)
        {
            if (duration <= 0f)
            {
                if (from != null) from.localScale = Vector3.zero;
                if (to != null) to.localScale = toScale;
                return;
            }

            float elapsed = 0f;
            while (elapsed < duration && !token.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (from != null) from.localScale = Vector3.Lerp(fromScale, Vector3.zero, t);
                if (to != null) to.localScale = Vector3.Lerp(Vector3.zero, toScale, t);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (from != null) from.localScale = Vector3.zero;
            if (to != null) to.localScale = toScale;
        }
    }
}