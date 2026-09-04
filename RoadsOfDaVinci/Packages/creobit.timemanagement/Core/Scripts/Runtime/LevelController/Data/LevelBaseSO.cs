using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Boosters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data
{
    public abstract class LevelBaseSO : ScriptableObject, ITimeManagerSO
    {
        public abstract string LevelName { get; }
        public abstract string LevelDescription { get; }
        public abstract int LevelNumber { get; }
        public abstract bool OverrideLevelName { get; }
        public virtual bool UseLevelNameInStartTasksView => false;
        public abstract GameObject LevelPrefab { get; }
        public abstract AssetReferenceT<LevelBaseSO> NextLevel { get; }

        public abstract bool HaveFinalPoint { get; }
        public abstract Sprite LevelPveStartImage { get;  }
        public abstract ResourceAmount[] StartResources { get; }
        public abstract LevelTaskBase[] LevelTasks { get; }
        public abstract AudioClip Music { get; }
        public abstract LevelTimerData LevelTimerData { get; }
        public abstract BoosterDataSO[] BoostersData { get; }
        public virtual BoosterDataSO[] BoostersChargedOnStart => System.Array.Empty<BoosterDataSO>();
        public abstract float GlobalLightIntensityOnStart { get; }
        public abstract bool GlobalPostProcessActiveOnStart { get; }

        public abstract bool EnableTutorial { get; }
        public abstract bool UsingStartTutorial { get; }

        public abstract TutorialViewSo StartTutorial { get; }

        public abstract bool UsingStartDialogue { get; }

        public abstract string StartDialogue { get; }
        public abstract float DiseasableBaseMinInterval { get; }
        public abstract float DiseasableBaseMaxInterval { get; }
        public abstract float BreakableBaseMinInterval { get; }
        public abstract float BreakableBaseMaxInterval { get; }
        public abstract bool EnableDisease { get; }
        public abstract bool EnableRepair { get; }

        public abstract List<ResourceBaseSO> ResourcesForResourcePanel { get; }
    }
}
