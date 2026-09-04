using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using Creobit.UI.Utility;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene
{
    [Serializable]
    public class MetaSceneReferences
    {
        [field: SerializeField]
        public Canvas MetaCanvas { get; private set; }
        
        [field: SerializeField]
        public Camera MainCamera { get; private set; }
        
        [field: SerializeField]
        public CinemachinePositionComposer PositionComposer { get; private set; }
        
        [field: SerializeField]
        public GameObject CameraTrackPoint { get; private set; }
        
        [field: SerializeField]
        public Transform DefaultTrackPoint { get; private set; }
        
        [field: SerializeField]
        public Vector2 DefaultTrackOffset { get; private set; }
        
        [field: SerializeField]
        public SpriteRenderer RefTransformForCameraScale { get; private set; }
        
#if TMN_Module
        
        [field: SerializeField]
        public CurrentLevelSO CurrentLevel { get; private set; }
        
        [field: SerializeField]
        public List<MapSpotSpriteByState> MapSpotSpritesByStates { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField]
        public PanelReference MapBottomPanel { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField]
        public GameObject MapBackground { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField] public GetActualMap GetActualMap { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField]
        public MapPageView MainMapPage => GetActualMap.GetMainMap(); 
        //{ get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField]
        public MapPageView BonusMapPage => GetActualMap.GetBonusMap();
        //{ get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField]
        public MapFlagView MapFlag { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField]
        public Button MapChangeButton { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MapUIReferences)]
        [field: SerializeField]
        public GameObject MapObject { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MenuUIReferences)]
        [field: SerializeField]
        public PanelReference MenuBottomPanel { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MenuUIReferences)]
        [field: SerializeField]
        public PanelReference CreateProfilePanel { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MenuUIReferences)]
        [field: SerializeField]
        public PanelReference AlertPanel { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MenuUIReferences)]
        [field: SerializeField]
        public GameObject ExitButton { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MenuUIReferences)]
        [field: SerializeField]
        public GameObject ExtrasButton { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MenuUIReferences)]
        [field: SerializeField]
        public GameObject Background { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.MenuUIReferences)]
        [field: SerializeField]
        public Transform MenuTrackPoint { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.CollectionRoomUIReferences)]
        [field: SerializeField]
        public PanelReference CollectionRoomBottomPanel { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.CollectionRoomUIReferences)]
        [field: SerializeField]
        public GameObject CollectionRoom { get; private set; }

        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.CollectionRoomUIReferences)]
        [field: SerializeField]
        public PanelReference TrophyBookPanel { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.CollectionRoomUIReferences)]
        [field: SerializeField]
        public AssetReference TooltipView { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.ComicsReferences)]
        [field: SerializeField]
        public Transform ComicsRoot;
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.ComicsReferences)]
        [field: SerializeField]
        public Transform ComicsTrackPoint { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.ComicsReferences)]
        [field: SerializeField]
        public Vector2 ComicsTrackOffset { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.GuidesUIReferences)]
        [field: SerializeField]
        public PanelReference GuidesSelectLevel;
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.GuidesUIReferences)]
        [field: SerializeField]
        public PanelReference GuidesMainPage;
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.GuidesUIReferences)]
        [field: SerializeField]
        public PanelReference GuidesUI;
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.GuidesUIReferences)]
        [field: SerializeField]
        public PanelReference GameplayGuidePanel;
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.GuidesUIReferences)]
        [field: SerializeField]
        public GuidesLevelRefs GuidesLevelRefs;
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.GuidesUIReferences)]
        [field: SerializeField]
        public MetaGuideLevelRefs MetaGuideLevelRefs;
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.GuidesUIReferences)]
        [field: SerializeField]
        public List<LocationDataByNum> LocationNamesByNum;

        [field: SerializeField]
        public Transform LogoPosition { get; private set; }
        
        [field: SerializeField]
        public PanelReference ExtrasPanel { get; private set; }
        
        [field: SerializeField]
        public PanelReference ExtrasMusicPanel { get; private set; }
        
        [field: FoldoutGroup(_8floor.TimeManagement.Core.Scripts.Runtime
            .Utils.RuntimeConstants.FoldoutNames.ComicsReferences)]
        [field: SerializeField]
        public PanelReference ComicsUI;
#endif
#if TMN_Module
        
        [field: SerializeField] public List<LevelsGameStruct> LevelsGamesStruct;
        public AllLevelsSO allLevels =>
            LevelsGamesStruct.FirstOrDefault(x => x.GameName == _gameSwitcher?.CurrentGame.Value.GameName).allLevels;
#endif
        
        private GameSwitcher _gameSwitcher;

        public void SetSwitcher(GameSwitcher gameSwitcher)
        {
            _gameSwitcher = gameSwitcher;
        }
        
    }
    
    [Serializable]
    public struct LevelsGameStruct
    {
        public string GameName;
        public AllLevelsSO allLevels;
    } 
    
}