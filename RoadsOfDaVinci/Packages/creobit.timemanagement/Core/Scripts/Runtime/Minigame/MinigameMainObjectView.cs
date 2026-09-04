using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Minigame
{
    public class MinigameMainObjectView : StaticObjectView
    {
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Minigame)]
        public UltEvent onMinigameComplete;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Minigame)]
        public List<MinigameTarget> targets;

        [HideInInspector]
        public bool isUsed;

        [Inject]
        protected void Construct(MinigameController minigameController)
        {
            minigameController.AddMainObject(this);
        }
    }
}