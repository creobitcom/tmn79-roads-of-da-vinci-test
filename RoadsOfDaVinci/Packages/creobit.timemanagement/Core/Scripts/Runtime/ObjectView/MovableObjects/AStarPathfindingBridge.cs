using System;
using Cysharp.Threading.Tasks;
using Pathfinding;
using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    // TODO : check class
    public class AStarPathfindingBridge : MonoBehaviour, IMovableObject
    {
        public event Action StartMove;
        public event Action<MovableObjectView> EndMove;

        private IAstarAI _aiPath;
        private Seeker _seeker;
        private bool _respectBlocked = true;

        private MovableObjectView _unit;
        private int _stopVersion;
        private int _moveVersion;
        // The move that already completed via arrival; its failure-retry watchdog must stay silent.
        private int _arrivedMoveVersion = -1;
        private bool _reachedDestinationWhileLocked;

        public void Load(MovableObjectView unit)
        {
            _aiPath = GetComponent<IAstarAI>();
            _aiPath.ReachedDestination += ReachedDestinationHandler;
            _aiPath.ForceInit();
            _unit = unit;
            _unit.MovementLockStateChanged += OnMovementLockStateChanged;

            // Make this unit's movement honor GraphNode.Blocked (see ApplyBlockedTraversal).
            _seeker = GetComponent<Seeker>();
            if (_seeker != null)
            {
                _seeker.preProcessPath -= ApplyBlockedTraversal;
                _seeker.preProcessPath += ApplyBlockedTraversal;
            }
        }

        private void ReachedDestinationHandler()
        {
            if (_unit != null && _unit.IsMovementLocked)
            {
                _reachedDestinationWhileLocked = true;
                return;
            }

            CompleteReachedDestination();
        }

        private void CompleteReachedDestination()
        {
            _reachedDestinationWhileLocked = false;

            // Arrival ends this move, so its failure-retry watchdog is obsolete. SetPath(null)
            // below cancels the seeker's current request, which retroactively flags the very
            // path that just delivered the unit as errored. RetryWhilePathFails took that for a
            // genuine path failure and re-searched a second later; the redundant arrival fired
            // a second EndMove -> PlayAnimation(Idle), killing the unit's work animation right
            // after an interaction started (worker froze mid-upgrade while the ring kept going).
            // Mark the move number instead of bumping _moveVersion: a bump would also silence
            // the watchdog of a NEWER move if a stale arrival from an old one landed here.
            _arrivedMoveVersion = _moveVersion;

            _aiPath.SetPath(null);

            EndMove?.Invoke(_unit);
        }

        private void OnMovementLockStateChanged(bool isLocked)
        {
            // The destination was reached while movement was locked (e.g. the unit was stunned right
            // at its home/work point). ReachedDestination is edge-triggered and won't fire again, so
            // we must complete it now. Do NOT re-check _aiPath.reachedDestination here: with the path
            // cleared / speed zeroed it reads unreliably, which left the unit stuck "running in place".
            if (!isLocked && _reachedDestinationWhileLocked)
            {
                CompleteReachedDestination();
            }
        }

        private void OnDestroy()
        {
            _aiPath.ReachedDestination -= ReachedDestinationHandler;
            if (_unit != null)
            {
                _unit.MovementLockStateChanged -= OnMovementLockStateChanged;
            }
            if (_seeker != null)
            {
                _seeker.preProcessPath -= ApplyBlockedTraversal;
            }
        }

        public void SetSpeed(float speed)
        {
            _aiPath ??= GetComponent<IAstarAI>();
            _aiPath.maxSpeed = speed;
        }

        public async UniTaskVoid Stop()
        {
            // Overlapping Stop() calls (e.g. CancelTask immediately followed by
            // MoveToHomePointAfterDelay in the same frame) used to capture the
            // already-zeroed speed and restore 0 forever, freezing the unit.
            var version = ++_stopVersion;
            var capturedSpeed = _aiPath.maxSpeed;
            _reachedDestinationWhileLocked = false;
            // A stop also invalidates any pending path-failure retries of the previous move.
            _moveVersion++;

            _aiPath.maxSpeed = 0;

            _aiPath.SetPath(null);

            // Need to wait a little before returning the speed, otherwise the object will shift a little.
            // A plain Yield instead of WaitUntil(!hasPath): a new path assigned during the wait
            // (new MoveTo or AIPath auto-repath) kept hasPath true forever and the speed stuck at 0.
            await UniTask.Yield(PlayerLoopTiming.Update);

            if (_stopVersion != version)
            {
                return; // a newer Stop() owns the speed restore
            }

            if (_unit != null)
            {
                _unit.SetCurrentSpeed();
                return;
            }

            _aiPath.maxSpeed = capturedSpeed;
        }

        public float GetSpeed()
        {
            return _aiPath.maxSpeed;
        }

        public async UniTask<Path> MoveTo(Vector3 target, bool respectBlocked = true, bool rescueOnUnreachable = false)
        {
            _respectBlocked = respectBlocked;
            // A fresh move invalidates any pending "reached while locked" completion.
            _reachedDestinationWhileLocked = false;
            var version = ++_moveVersion;
            _aiPath.destination = target;

            var path = _aiPath.SearchPath();
            // await UniTask.Delay(TimeSpan.FromSeconds(.5f));
            StartMove?.Invoke();
            RetryWhilePathFails(path, target, version, rescueOnUnreachable).Forget();
            return path;
        }

        private async UniTaskVoid RetryWhilePathFails(Path path, Vector3 target, int version, bool rescueOnUnreachable)
        {
            var token = this.GetCancellationTokenOnDestroy();
            // Fast 1 Hz retries first, then a slow probe: giving up entirely left the unit
            // frozen in Work/ReturnHome with no EndMove once a target stayed blocked past the
            // fast window (units have autoRepath = Never, so nothing else re-searches). A new
            // order or Stop() bumps _moveVersion and ends the loop.
            var fastRetriesLeft = 30;
            // Return-home moves (rescueOnUnreachable) get a BOUNDED budget: respectBlocked is
            // already false for them, so a path that still fails after the fast window is a
            // genuine graph failure (disconnected area / unwalkable node) that will never clear,
            // not a temporary Blocked obstruction. Retrying forever there left a unit running in
            // place — and, for a RunHome task return, stuck in ReturnHome and unassignable —
            // permanently. After the budget we rescue it home (RescueUnreachableDestination).
            // Task moves keep retrying forever (slowRetriesLeft stays negative) so a transiently
            // blocked target still resolves once the map opens.
            var slowRetriesLeft = rescueOnUnreachable ? 6 : -1;

            while (path != null)
            {
                path.Claim(this);

                var cancelled = await UniTask.WaitUntil(() => path.IsDone(), cancellationToken: token)
                    .SuppressCancellationThrow();

                var failed = !cancelled && path.error;
                path.Release(this);

                // _arrivedMoveVersion == version: the unit already arrived on this move, and the
                // path "error" is our own SetPath(null) from CompleteReachedDestination —
                // re-searching would only fire a duplicate arrival.
                if (cancelled || !failed || _moveVersion != version || _arrivedMoveVersion == version)
                {
                    return;
                }

                // The path calculation failed (the target got blocked mid-task). AIPath
                // swallows the error, so EndMove would never fire and the unit would freeze
                // in place forever. Keep retrying until the map unblocks or a newer
                // move/stop takes over the agent.
                var delaySeconds = 5;

                if (fastRetriesLeft > 0)
                {
                    fastRetriesLeft--;
                    delaySeconds = 1;
                }
                else if (rescueOnUnreachable && slowRetriesLeft <= 0)
                {
                    // Budget exhausted and home is still unreachable: snap the unit onto its
                    // destination node and run the normal arrival so it never freezes.
                    RescueUnreachableDestination(target);
                    return;
                }
                else if (rescueOnUnreachable)
                {
                    slowRetriesLeft--;
                }

                cancelled = await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: token)
                    .SuppressCancellationThrow();

                if (cancelled || _moveVersion != version || !isActiveAndEnabled)
                {
                    return;
                }

                _aiPath.destination = target;
                path = _aiPath.SearchPath();
            }
        }

        // Last-resort recovery for a return-home move whose destination is genuinely unreachable
        // (disconnected graph area / unwalkable node — NOT a GraphNode.Blocked obstruction, which
        // respectBlocked:false already ignores). Snaps the agent onto the destination's nearest
        // node and fires the normal arrival completion (CompleteReachedDestination -> EndMove),
        // which resets the run animation, completes a RunHome task return, and hides the unit in
        // base. Without it such a unit retries forever, running in place, and a RunHome return
        // stays stuck in ReturnHome (unassignable) permanently.
        private void RescueUnreachableDestination(Vector3 target)
        {
            if (_unit == null)
            {
                return;
            }

            // Юнита везёт транспорт — его позицией распоряжается транспорт. Спасательный
            // телепорт домой сейчас выдернул бы его прямо из кабины. Путь всё равно
            // пересчитается после высадки (Teleport в MovableObjectTransportHold).
            if (_unit.IsHeldByTransport)
            {
                return;
            }

            if (AstarPath.active != null)
            {
                var node = AstarPath.active.GetNearest(target).node;
                if (node != null)
                {
                    target = (Vector3)node.position;
                }
            }

            _aiPath.Teleport(target);
            CompleteReachedDestination();
        }

        // Injected into the unit's Seeker (preProcessPath) so EVERY movement path honors the
        // custom GraphNode.Blocked marker — the same rule task validation uses. Without it the
        // seeker planned purely on Penalty and could cut straight through a blocked obstacle to
        // reach a target behind it (e.g. walk through a rock to a dig site that validation had
        // already routed around).
        private void ApplyBlockedTraversal(Path p)
        {
            // Return-home runs (respectBlocked=false) keep the plain penalty-based search so a
            // unit still prefers a clear detour but can, as a last resort, walk out through an
            // obstacle instead of freezing in place when one is revealed boxing it in.
            if (_respectBlocked)
            {
                p.traversalProvider = BlockedNodeTraversalProvider.Instance;
            }
        }
    }

    /// <summary>
    /// Treats a node as impassable while its custom <see cref="GraphNode.Blocked"/> counter is
    /// >= 0 (default -1 = free) — EXCEPT the path's own endpoints, so a unit can still walk
    /// to/from the very obstacle it is clearing. Mirrors IsPathWalkable, which skips the
    /// first/last node when scanning for blockers. Blocked is only ever raised on the ground
    /// point graph, so units pathing on other graphs (e.g. flying) are unaffected.
    /// </summary>
    public sealed class BlockedNodeTraversalProvider : ITraversalProvider
    {
        public static readonly BlockedNodeTraversalProvider Instance = new BlockedNodeTraversalProvider();

        public bool CanTraverse(Path path, GraphNode node)
        {
            if (!DefaultITraversalProvider.CanTraverse(path, node))
                return false;

            if (node.Blocked < 0)
                return true;

            return path is ABPath abPath && (node == abPath.startNode || node == abPath.endNode);
        }

        public uint GetTraversalCost(Path path, GraphNode node)
        {
            return DefaultITraversalProvider.GetTraversalCost(path, node);
        }
    }
}
