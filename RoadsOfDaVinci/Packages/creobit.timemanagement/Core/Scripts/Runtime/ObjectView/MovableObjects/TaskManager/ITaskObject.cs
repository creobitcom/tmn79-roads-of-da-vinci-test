using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager
{
    public interface ITaskObject : IReactToActions, ILevelLoadUnit
    {
        public Vector3 Position { get; }
        public MovableObjectTask CurrentTask { get; set; }
        public UniqueBadgesData UniqueBadgesData { get; }
        public Vector3 BadgePosition { get; }
        public float InteractionOffset { get; }
        public bool IsUsing { get; }
    }
}