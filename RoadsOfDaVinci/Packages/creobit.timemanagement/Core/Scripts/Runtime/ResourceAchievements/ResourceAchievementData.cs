using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Resources
{
    [Serializable]
    public class ResourceAchievementData
    {
        [field: SerializeField] public ResourceAchievementMode Mode { get; private set; }
        [field: SerializeField] public int Threshold { get; private set; }
        [field: SerializeField] public List<ResourceBaseSO> TrackedResources { get; private set; } = new();
        [field: SerializeField] public bool IncludeInventoryResources { get; private set; }
    }
}
