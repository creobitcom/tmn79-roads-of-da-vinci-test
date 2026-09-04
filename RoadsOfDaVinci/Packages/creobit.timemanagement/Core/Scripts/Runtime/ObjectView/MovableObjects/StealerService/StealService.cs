using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Cysharp.Threading.Tasks;
using R3.Triggers;
using UltEvents;
using UnityEngine;
using VContainer;
using static _8floor.TimeManagement.Core.Scripts.Runtime.Utils.RuntimeConstants.Enums;

public class StealService : MonoBehaviour
{
    private static readonly HashSet<StaticObjectView> ReservedTargets = new();

    [SerializeField] private List<StaticObjectView> _staticObjectViews = new List<StaticObjectView>();
    [SerializeField] private MovableObjectView _movableObjectView;
    [SerializeField] private bool _autoSteal = false;
    [SerializeField] private AnimationClip _stealAnimation;
    public UltEvent OnFailFind;
    public UltEvent OnStartSteal;
    public UltEvent OnEndSteal;

    private IObjectViewController _objectViewController;
    private IMovableObjectTaskManager _taskManager;

    private bool _inventoryFull = false;
    private CancellationTokenSource _cancellationTokenSource;
    private MovableObjectInventoryController _inventoryController;
    private StaticObjectView _currentStaticObject = null;
    private StaticObjectView _stolenObject = null;
    private bool _isBeingExpelled;
    private bool _restorePrimaryActionOnEnable;
    private bool _primaryActionStateBeforeExpulsion;
    private StaticObjectView _reservedTarget;
    private bool _reservedTargetCanInteract;
    private bool _reservedTargetCanReactToPrimaryAction;

    [Inject]
    private void Construct(IObjectViewController objectViewController)
    {
        _objectViewController = objectViewController;
    }

    private void Start()
    {
        var objectViews = FindObjectsByType<StaticObjectView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.ObjectDataSO.IsStealable).ToList();
        _staticObjectViews = objectViews;
        var movableObjectController = _objectViewController.GetMovableObjectController();
        _inventoryController = movableObjectController.GetInventoryController();
        _taskManager = movableObjectController.GetTaskManager();
    }

    private void OnEnable()
    {
        _isBeingExpelled = false;

        if (!_restorePrimaryActionOnEnable || _movableObjectView == null)
        {
            return;
        }

        _movableObjectView.SetPrimaryActionState(_primaryActionStateBeforeExpulsion);
        _restorePrimaryActionOnEnable = false;
    }

    private void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        ReleaseTargetReservation(true);
    }

    private async void OnTriggerEnter(Collider other)
    {
        if (_inventoryFull || _isBeingExpelled)
        {
            return;
        }

        if (_staticObjectViews.Any(x => x.gameObject == other.gameObject))
        {
            _currentStaticObject = _staticObjectViews.First(x => x.gameObject == other.gameObject);
            if (_autoSteal)
            {
                await TrySteal();
            }
        }
    }

    public async UniTask<bool> TrySteal()
    {
        if (_isBeingExpelled)
        {
            return false;
        }

        if (_currentStaticObject == null)
        {
            OnFailFind?.Invoke();
            return false;
        }

        if (!TryReserveTarget(_currentStaticObject))
        {
            return false;
        }

        _inventoryFull = true;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            await CallSteal(_currentStaticObject, _cancellationTokenSource.Token);
            ReleaseTargetReservation(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            _inventoryFull = false;
            ReleaseTargetReservation(true);
            return false;
        }
        catch
        {
            _inventoryFull = false;
            ReleaseTargetReservation(true);
            throw;
        }
    }

    private bool TryReserveTarget(StaticObjectView target)
    {
        if (target == null || !target.gameObject.activeInHierarchy || !ReservedTargets.Add(target))
        {
            return false;
        }

        _reservedTarget = target;
        _reservedTargetCanInteract = target.CanInteract;
        _reservedTargetCanReactToPrimaryAction = target.CanReactToPrimaryAction;

        target.CanInteract = false;
        target.SetPrimaryActionState(false);

        var taskProgress = _taskManager.GetTaskProgress(target);
        if (taskProgress is TaskProgress.Queued or TaskProgress.InProgress)
        {
            if (!_objectViewController.TryCancelTask(target))
            {
                ReleaseTargetReservation(true);
                return false;
            }
        }

        return true;
    }

    private void ReleaseTargetReservation(bool restoreInteraction)
    {
        if (ReferenceEquals(_reservedTarget, null))
        {
            return;
        }

        ReservedTargets.Remove(_reservedTarget);

        if (restoreInteraction && _reservedTarget != null)
        {
            _reservedTarget.CanInteract = _reservedTargetCanInteract;
            _reservedTarget.SetPrimaryActionState(_reservedTargetCanReactToPrimaryAction);
        }

        _reservedTarget = null;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_currentStaticObject) return;
        if (_currentStaticObject.gameObject == other.gameObject)
        {
            _currentStaticObject = null;
        }
    }

    private async UniTask CallSteal(StaticObjectView staticObjectView, CancellationToken token)
    {
        if (staticObjectView.transform.parent)
        {
            _stolenObject = staticObjectView.transform.parent.GetComponentInParent<StaticObjectView>();
            _stolenObject?.productionData?.SetBlockProduction(true); 
        }
        
        _movableObjectView.AStarAI.isStopped = true;
        _movableObjectView.AStarAI.SetPath(null);

        await UniTask.Yield(token);

        if (staticObjectView != null)
        {
            var targetPos = staticObjectView.transform.position;
            var myPos = _movableObjectView.transform.position;

            var direction = (targetPos - myPos).normalized;

            if (direction.sqrMagnitude > 0.001f)
            {
                var angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

                _movableObjectView.modelTransform.rotation =
                    _movableObjectView.initialRotation * Quaternion.AngleAxis(angle, Vector3.up);
            }
        }

        OnStartSteal?.Invoke();

        _movableObjectView.PlayAnimation("picking_up");

        await UniTask.Delay(TimeSpan.FromSeconds(_stealAnimation.length), cancellationToken: token);

        if (staticObjectView != null)
        {
            staticObjectView.gameObject.SetActive(false);
            staticObjectView.UnBlockInteractionGraphNode();
        }

        _movableObjectView.PlayAnimation("Idle");

        if (staticObjectView != null)
            _inventoryController.AddResources(_movableObjectView, staticObjectView.ObjectDataSO.OutputResources);

        OnEndSteal?.Invoke();

        _movableObjectView.AStarAI.isStopped = false;
    }

    public void ReturnResources()
    {
        if (!_isBeingExpelled)
        {
            _isBeingExpelled = true;
            _primaryActionStateBeforeExpulsion = _movableObjectView.CanReactToPrimaryAction;
            _restorePrimaryActionOnEnable = true;
            _movableObjectView.SetPrimaryActionState(false);
        }

        _inventoryController.StashResources(_movableObjectView);
        _inventoryFull = false;

        if (!_stolenObject) return;
        
        _stolenObject.productionData.SetBlockProduction(false); 
        _stolenObject = null;
    }

    public void ClearInventory()
    {
        _inventoryController.ClearInventory(_movableObjectView);
        _inventoryFull = false;

        if (!_stolenObject) return;
        _stolenObject.productionData.SetBlockProduction(false); 
        _stolenObject = null;
    }
}
