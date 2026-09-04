using System;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public interface IMovableObject
    {
        public event Action StartMove;
        public event Action<MovableObjectView> EndMove;
        public void SetSpeed(float speed);
        public UniTaskVoid Stop();
        public float GetSpeed();
        // respectBlocked=false lets the path ignore GraphNode.Blocked (used for the return-home
        // run so a unit is never permanently boxed in by an obstacle revealed under its feet —
        // task moves keep it true so units don't cut straight through obstacles to a target).
        // rescueOnUnreachable=true bounds the path-failure retries and, once they run out, snaps
        // the unit onto the destination and completes the move so a genuinely unreachable home
        // (disconnected graph area / unwalkable node) can never leave it frozen. Only the
        // return-home run sets it; task moves keep retrying so a transiently blocked target still
        // resolves when the map reopens.
        public UniTask<Path> MoveTo(Vector3 target, bool respectBlocked = true, bool rescueOnUnreachable = false);
    }
}