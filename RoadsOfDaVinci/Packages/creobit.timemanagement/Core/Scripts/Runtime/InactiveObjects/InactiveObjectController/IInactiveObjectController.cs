using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using Creobit.Loading;
using Pathfinding;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects
{
    public interface IInactiveObjectController : IDisposable, ILoadUnit
    {
        IReadOnlyList<InactiveObjectRefs> InactiveObjects { get; }
        void AddInactiveObject(InactiveObjectRefs inactiveObject);
        void ChangeRadiusTemporary(InactiveObjectRefs inactiveObject, float toSize, float changeSizeTime);
        void SetNodesState(List<GraphNode> nodes, bool state);
        void SetObjectsState(List<IReactToActions> nodes, bool state);
        void SetDefaultState(bool state);
        void SetPriority(bool state);
        void SetRevealSettings(float duration, float overshoot, float exploredMaskLevel);
    }
}
