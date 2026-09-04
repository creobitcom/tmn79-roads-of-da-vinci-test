using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using Creobit.Loading;
using Creobit.UI.Utility;
using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public interface IGuidesController : ILoadUnit<GuidesControllerJson>
    {
        public ReactiveProperty<GuidesStates> State { get; }

        public event Action<int> LevelGuideOpened;

        public void ChangeState(GuidesStates states);

        public void ShowLocationGuideView(int locationNum);

        public void ShowNextLocation();

        public void ShowPrevLocation();

        public void ShowLevelView(int levelNum);

        public void ShowPage(int pageIndex);

        public void ShowNextPage();

        public void ShowPrevPage();

        public void ShowGameplayGuide(int levelNum);
    }

    [Serializable]
    public class GuidesControllerJson
    {
        public PanelReference MainPagePanel;
        public PanelReference SelectLevelPanel;
        public PanelReference GuidePanel;
        public PanelReference GameplayGuidePanel;
        public MetaGuideLevelRefs MetaGuideLevelRefs;
        public GuidesLevelRefs LevelRefsPreset;
        public List<GuideLocationData> Locations;
        public AllLevelsSO AllLevels;
        public Transform PanelsParent;
        public string ContentFolder;

        public GuidesControllerJson(PanelReference mainPagePanel, PanelReference selectLevelPanel,
            PanelReference guidePanel, PanelReference gameplayGuidePanel, MetaGuideLevelRefs metaGuideLevelRefs,
            GuidesLevelRefs levelRefsPreset, List<GuideLocationData> locations, AllLevelsSO allLevels,
            Transform panelsParent, string contentFolder)
        {
            MainPagePanel = mainPagePanel;
            SelectLevelPanel = selectLevelPanel;
            GuidePanel = guidePanel;
            GameplayGuidePanel = gameplayGuidePanel;
            MetaGuideLevelRefs = metaGuideLevelRefs;
            LevelRefsPreset = levelRefsPreset;
            Locations = locations;
            AllLevels = allLevels;
            PanelsParent = panelsParent;
            ContentFolder = contentFolder;
        }
    }
}
