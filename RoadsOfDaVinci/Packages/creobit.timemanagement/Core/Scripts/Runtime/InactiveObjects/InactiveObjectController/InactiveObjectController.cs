using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using Cysharp.Threading.Tasks;
using Pathfinding;
using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects
{
    public class InactiveObjectController : IInactiveObjectController
    {
        private bool defaultState;
        private bool activePriority;

        private readonly List<InactiveObjectRefs> _inactiveObjects = new();

        private readonly Dictionary<GraphNode, bool> _nodeFogStates = new();

        private readonly HashSet<InactiveObjectRefs> _exploredObjects = new();

        private float _revealDuration = 0.45f;
        private float _revealOvershoot = 0.12f;
        private float _exploredMaskLevel;

        private CancellationTokenSource _disposeCts;

        public UniTask Load()
        {
            _disposeCts ??= new CancellationTokenSource();

            return UniTask.CompletedTask;
        }

        public IReadOnlyList<InactiveObjectRefs> InactiveObjects => _inactiveObjects;

        public void AddInactiveObject(InactiveObjectRefs inactiveObject)
        {
            _inactiveObjects.RemoveAll(existing => existing == null);
            _exploredObjects.RemoveWhere(existing => existing == null);

            _inactiveObjects.Add(inactiveObject);
            inactiveObject.OnStateChanged.Subscribe(value => SetInactiveObjectState(inactiveObject));
            SetInactiveObjectState(inactiveObject, true);
        }

        public void ChangeRadiusTemporary(InactiveObjectRefs inactiveObject, float toSize, float changeSizeTime)
        {
            if (inactiveObject == null || _disposeCts == null)
            {
                return;
            }

            RunTemporaryRadius(inactiveObject, toSize, changeSizeTime, _disposeCts.Token).Forget();
        }

        private async UniTaskVoid RunTemporaryRadius(InactiveObjectRefs inactiveObject, float toSize, float changeSizeTime,
            CancellationToken token)
        {
            var originalRadius = inactiveObject.Radius;
            inactiveObject.ChangeRadius(toSize);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(changeSizeTime), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (inactiveObject != null)
            {
                inactiveObject.ChangeRadius(originalRadius);
            }
        }

        private const uint NodeBlockedPenalty = 1_000_000u;

        public void SetNodesState(List<GraphNode> nodes, bool state)
        {
            foreach (var node in nodes)
            {
                node.Blocked = state ? -1 : 1;
                node.Penalty = state ? 0u : NodeBlockedPenalty;
                _nodeFogStates[node] = state;
            }
        }

        public void SetObjectsState(List<IReactToActions> nodes, bool state)
        {
            foreach (var node in nodes)
            {
                node.IsInactiveBlocked = !state;
            }
        }

        public void SetDefaultState(bool state)
        {
            defaultState = state;
        }

        public void SetPriority(bool state)
        {
            activePriority = state;
        }

        public void SetRevealSettings(float duration, float overshoot, float exploredMaskLevel)
        {
            _revealDuration = duration;
            _revealOvershoot = overshoot;
            _exploredMaskLevel = exploredMaskLevel;
        }

        private void SetInactiveObjectState(InactiveObjectRefs inactiveObject, bool immediate = false)
        {
            TrySetObjectsState(inactiveObject, inactiveObject.ObjectSwitcher);
            TrySetNodesState(inactiveObject, inactiveObject.NodeSwitcher);

            var isUnBlock = inactiveObject.ObjectSwitcher && inactiveObject.NodeSwitcher;
            var isDrawn = inactiveObject.State && (defaultState ? !isUnBlock : isUnBlock);

            if (isDrawn)
            {
                _exploredObjects.Add(inactiveObject);
            }

            var targetMask = 0f;

            if (isDrawn)
            {
                targetMask = 1f;
            }
            else if (!defaultState && _exploredObjects.Contains(inactiveObject))
            {
                targetMask = _exploredMaskLevel;
            }

            inactiveObject.PlayMaskTransition(targetMask, immediate ? 0f : _revealDuration, _revealOvershoot);
        }

        private void TrySetObjectsState(InactiveObjectRefs inactiveObject, bool state)
        {
            foreach (var objectView in inactiveObject.IncludedObjects)
            {
                var isControlled = false;
                var isObjectActive = false;
                var isPriorityActive = false;

                foreach (var inactive in _inactiveObjects)
                {
                    if (inactive.State && inactive.IncludedObjects.Contains(objectView))
                    {
                        if (inactive.ObjectSwitcher == activePriority)
                        {
                            isPriorityActive = true;
                        }

                        isObjectActive = isPriorityActive ? activePriority : inactive.ObjectSwitcher;
                        isControlled = true;
                    }
                }

                if (!isControlled)
                {
                    objectView.IsInactiveBlocked = !defaultState;
                }
                else
                {
                    objectView.IsInactiveBlocked = !isObjectActive;
                }
            }
        }

        private void TrySetNodesState(InactiveObjectRefs inactiveObject, bool state)
        {
            foreach (var objectView in inactiveObject.IncludedNodes)
            {
                var isControlled = false;
                var isObjectActive = false;
                var isPriorityActive = false;

                foreach (var inactive in _inactiveObjects)
                {
                    if (inactive.State && inactive.IncludedNodes.Contains(objectView))
                    {
                        if (inactive.NodeSwitcher == activePriority)
                        {
                            isPriorityActive = true;
                        }

                        isObjectActive = isPriorityActive ? activePriority : inactive.NodeSwitcher;
                        isControlled = true;
                    }
                }

                bool targetWalkable = isControlled ? isObjectActive : defaultState;

                if (_nodeFogStates.TryGetValue(objectView, out bool currentWalkable))
                {
                    if (currentWalkable != targetWalkable)
                    {
                        if (targetWalkable)
                        {
                            objectView.Blocked -= 2;
                            objectView.Penalty = objectView.Penalty >= NodeBlockedPenalty
                                ? objectView.Penalty - NodeBlockedPenalty
                                : 0u;
                        }
                        else
                        {
                            objectView.Blocked += 2;
                            objectView.Penalty += NodeBlockedPenalty;
                        }
                        _nodeFogStates[objectView] = targetWalkable;
                    }
                }
                else
                {
                    if (defaultState != targetWalkable)
                    {
                        if (targetWalkable)
                        {
                            objectView.Blocked -= 2;
                            objectView.Penalty = objectView.Penalty >= NodeBlockedPenalty
                                ? objectView.Penalty - NodeBlockedPenalty
                                : 0u;
                        }
                        else
                        {
                            objectView.Blocked += 2;
                            objectView.Penalty += NodeBlockedPenalty;
                        }
                    }

                    _nodeFogStates[objectView] = targetWalkable;
                }
            }
        }

        public static List<Collider> GetOverlappingColliders(Collider target)
        {
            var result = new List<Collider>();

            var candidates = Physics.OverlapBox(
                target.bounds.center,
                target.bounds.extents,
                target.transform.rotation
            );

            foreach (var col in candidates)
            {
                if (col == target) continue;

                if (Physics.ComputePenetration(
                        target, target.transform.position, target.transform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out _,
                        out _))
                {
                    result.Add(col);
                }
            }

            return result;
        }

        public void Dispose()
        {
            _inactiveObjects.Clear();
            _nodeFogStates.Clear();
            _exploredObjects.Clear();

            if (_disposeCts != null)
            {
                _disposeCts.Cancel();
                _disposeCts.Dispose();
                _disposeCts = null;
            }
        }
    }
}
