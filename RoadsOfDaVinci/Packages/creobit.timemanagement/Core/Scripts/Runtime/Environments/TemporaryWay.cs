using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Pathfinding;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    public class TemporaryWay : MonoBehaviour
    {
        [Title("Settings")] 
        [SerializeField] private bool _isOpen = false;
        [SerializeField] private float _closeTimer = 6f;

        [Title("Blocking Settings")] 
        [SerializeField] private Transform _blockPoint;

        [SerializeField] private float _blockRadius = 1.0f;

        [Title("Visual Events")] 
        [SerializeField] private UltEvent _onOpened;
        [SerializeField] private UltEvent _onClosed;

        private CancellationTokenSource _cts;
        private List<GraphNode> _affectedNodes = new();
        private bool _nodesInitialized = false;

        private void Start() => InitializeNodesAsync().Forget();

        private void OnEnable() => AstarPath.OnGraphsUpdated += OnGraphsUpdated;

        private void OnDisable()
        {
            AstarPath.OnGraphsUpdated -= OnGraphsUpdated;
            CancelTimer();
        }

        private void OnDestroy() => CancelTimer();

        private void OnGraphsUpdated(AstarPath script)
        {
            if (_nodesInitialized) ApplyBlockingState(_isOpen);
        }

        private async UniTaskVoid InitializeNodesAsync()
        {
            await UniTask.WaitUntil(() => AstarPath.active != null && !AstarPath.active.isScanning);

            _affectedNodes.Clear();

            AstarPath.active.data.GetNodes(node =>
            {
                var nodePos = (Vector3)node.position;

                if (Vector3.Distance(nodePos, _blockPoint.position) <= _blockRadius)
                {
                    _affectedNodes.Add(node);
                }
            });

            _nodesInitialized = true;
            ApplyBlockingState(_isOpen);

            if (_isOpen) _onOpened?.Invoke();
            else _onClosed?.Invoke();
        }

        [Button]
        public void SetOpen(bool value)
        {
            _isOpen = value;
            ApplyBlockingState(_isOpen);

            if (_isOpen)
            {
                _onOpened?.Invoke();
                CancelTimer();
                _cts = new CancellationTokenSource();
                StartCloseTimerAsync(_cts.Token).Forget();
            }
            else
            {
                _onClosed?.Invoke();
                CancelTimer();
            }
        }

        public void SetTimer(float value)
        {
            _closeTimer = value;
        }

        private async UniTaskVoid StartCloseTimerAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_closeTimer), cancellationToken: token);
                SetOpen(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ApplyBlockingState(bool isOpen)
        {
            if (!_nodesInitialized || AstarPath.active == null) return;

            AstarPath.active.AddWorkItem(new AstarWorkItem(ctx =>
            {
                foreach (var node in _affectedNodes)
                {
                    if (node == null) continue;

                    if (isOpen)
                    {
                        node.Blocked = -1;
                        node.Penalty = 0u;
                    }
                    else
                    {
                        node.Blocked = 1;
                        node.Penalty = 1000000u;
                    }
                }

                ctx.QueueFloodFill();
            }));
        }

        private void CancelTimer()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_blockPoint == null) return;

            var centerPos = _blockPoint.position;

            var zoneColor = _isOpen ? Color.green : Color.red;
            zoneColor.a = 0.2f;
            Gizmos.color = zoneColor;
            Gizmos.DrawSphere(centerPos, _blockRadius);

            zoneColor.a = 1f;
            Gizmos.color = zoneColor;
            Gizmos.DrawWireSphere(centerPos, _blockRadius);

            Gizmos.color = Color.cyan;
            var direction = _blockPoint.up;
            var arrowLen = 1.2f;
            var arrowEnd = centerPos + direction * arrowLen;

            Gizmos.DrawLine(centerPos, arrowEnd);

            var rotation = Quaternion.LookRotation(Vector3.forward, direction);
            var sideA = rotation * Quaternion.Euler(0, 0, 150) * Vector3.up * 0.4f;
            var sideB = rotation * Quaternion.Euler(0, 0, -150) * Vector3.up * 0.4f;

            Gizmos.DrawRay(arrowEnd, sideA);
            Gizmos.DrawRay(arrowEnd, sideB);
        }
#endif
    }
}