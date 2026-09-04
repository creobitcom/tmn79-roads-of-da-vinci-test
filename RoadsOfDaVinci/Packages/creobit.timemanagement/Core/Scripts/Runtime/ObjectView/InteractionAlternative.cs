using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    [Serializable]
    [HideLabel]
    public class InteractionAlternative
    {
        [SerializeField] public List<UnitTypeCount> UnitTypeCount;
        [SerializeField] public ResourceAmount[] InputResources;
        [SerializeField] public float InteractionTime = 1f;
        [SerializeField] public ushort InteractionsAmount = 1;
        [SerializeField] public bool DeactivateAllOthers;
        [SerializeField] public Sprite AlternativeIcon;
        [SerializeField] public Sprite AlternativeIconDisabled;
    }
}