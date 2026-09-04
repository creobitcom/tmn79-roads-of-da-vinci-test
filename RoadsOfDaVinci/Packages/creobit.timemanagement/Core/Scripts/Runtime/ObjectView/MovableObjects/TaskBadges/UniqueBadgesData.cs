using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges
{
    [Serializable]
    [HideLabel]
    [InlineProperty]
    public class UniqueBadgesData
    {
        [field: SerializeField]
        public Vector3 TaskBadgeOffset { get; private set; }
        
        [field: SerializeField]
        public bool HasUniqueBadges { get; private set; }
        
        [field: SerializeField]
        [field: ShowIf(nameof(HasUniqueBadges))]
        public TaskBadgeWrapper[] UniqueBadges { get; private set; }
    }
}