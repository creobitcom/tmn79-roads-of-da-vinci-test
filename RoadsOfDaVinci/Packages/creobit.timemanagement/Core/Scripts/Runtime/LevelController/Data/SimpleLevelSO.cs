using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Boosters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Tasks;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data
{
    [CreateAssetMenu(fileName = "LevelXXX", menuName = "8floor/TimeManager/Levels/SimpleLevelSO")]
    public class SimpleLevelSO : LevelBaseSO
    {
        [SerializeField] private string _levelName;
        [SerializeField] private string _levelDescription;
        [SerializeField] private int _levelNumber;
        [SerializeField] private bool _overrideLevelName;
        [SerializeField] private bool _useLevelNameInStartTasksView;
        [SerializeField] private GameObject _levelPrefab;
        [SerializeField] private AssetReferenceT<LevelBaseSO> _nextLevel;
        [SerializeField] private bool _haveFinalPoint;
        [SerializeField] private Sprite _guide;
        [SerializeField] private Sprite _levelPveStartImage;
        [SerializeField] private ResourceAmount[] _startResources;
        [SerializeField] private LevelTaskBase[] _levelTasks;
        [SerializeField] private AudioClip _music;
        [SerializeField] private LevelTimerData _levelTimerData;
        [SerializeField] private BoosterDataSO[] _boostersData;
        [SerializeField] private BoosterDataSO[] _boostersChargedOnStart;
        [SerializeField] private float _globalLightIntensityOnStart;
        [SerializeField] private bool _globalPostProcessActiveOnStart;

        [SerializeField] private bool _enableTutorial;
        [SerializeField] private bool _usingStartTutorial;

        [ShowIf(nameof(_usingStartTutorial))]
        [SerializeField]
        private TutorialViewSo _startTutorial;

        [SerializeField] private bool _usingStartDialogue;

        [ShowIf(nameof(_usingStartDialogue))]
        [SerializeField]
        private string _startDialogue;

        [SerializeField]
        private bool _enableDisease = false;

        [ShowIf(nameof(_enableDisease))]
        [SerializeField]
        private float _diseasableBaseMinInterval = 135;

        [ShowIf(nameof(_enableDisease))]
        [SerializeField]
        private float _diseasableBaseMaxInterval = 150;

        [SerializeField]
        private bool _enableRepair = false;

        [ShowIf(nameof(_enableRepair))]
        [SerializeField]
        private float _breakableBaseMinInterval = 100;

        [ShowIf(nameof(_enableRepair))]
        [SerializeField]
        private float _breakableBaseMaxInterval = 150;

        [Title("LevelPVE")]
        [LabelText("Resources in PanelResource")]
        [SerializeField]
        private List<ResourceBaseSO> _resourcesForResourcePanel;

        public override string LevelName => _levelName;
        public override string LevelDescription => _levelDescription;
        public override int LevelNumber => _levelNumber;
        public override bool OverrideLevelName => _overrideLevelName;
        public override bool UseLevelNameInStartTasksView => _useLevelNameInStartTasksView;
        public override GameObject LevelPrefab => _levelPrefab;
        public override AssetReferenceT<LevelBaseSO> NextLevel => _nextLevel;
        public override bool HaveFinalPoint => _haveFinalPoint;
        public override Sprite LevelPveStartImage => _levelPveStartImage;
        public override ResourceAmount[] StartResources => _startResources;
        public override LevelTaskBase[] LevelTasks => _levelTasks;
        public override AudioClip Music => _music;
        public override LevelTimerData LevelTimerData => _levelTimerData;
        public override BoosterDataSO[] BoostersData => _boostersData;
        public override BoosterDataSO[] BoostersChargedOnStart => _boostersChargedOnStart;
        public override float GlobalLightIntensityOnStart => _globalLightIntensityOnStart;
        public override bool GlobalPostProcessActiveOnStart => _globalPostProcessActiveOnStart;

        public override bool EnableTutorial => _enableTutorial;
        public override bool UsingStartTutorial => _usingStartTutorial;

        public override TutorialViewSo StartTutorial => _startTutorial;
        public override bool UsingStartDialogue => _usingStartDialogue;        
        public override string StartDialogue => _startDialogue;
        public override float DiseasableBaseMinInterval => _diseasableBaseMinInterval;
        public override float DiseasableBaseMaxInterval => _diseasableBaseMaxInterval;
        public override float BreakableBaseMinInterval => _breakableBaseMinInterval;
        public override float BreakableBaseMaxInterval => _breakableBaseMaxInterval;
        public override bool EnableDisease => _enableDisease;
        public override bool EnableRepair => _enableRepair;
        public override List<ResourceBaseSO> ResourcesForResourcePanel => _resourcesForResourcePanel;
    }
}
