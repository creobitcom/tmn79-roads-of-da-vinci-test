using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges
{
    [Serializable]
    public class TaskBadgeWrapper
    {
        [field: SerializeField] 
        public RuntimeConstants.Enums.TaskProgress TaskProgress { get; private set; }

        [field: SerializeField]
        public Sprite BadgeSprite { get; private set; }
        
        [field: SerializeField]
        public bool IsBackActive { get; private set; }
    }
}