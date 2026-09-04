using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Artifacts.Runtime.Service;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map
{
    /// <summary>
    /// The MapController class facilitates the management and interaction with the map UI.
    /// It provides functionality for displaying, hiding, and navigating through different
    /// pages and spots within the map.
    /// </summary>
    /// <remarks>
    /// This class implements the IMapController interface, integrating with various
    /// systems such as input handling, scene management, and loading. It uses dependency
    /// injection for its required components and manages the state of the map interface.
    /// </remarks>
    public class MapController : IMapController
    {
        /// <summary>
        /// Gets the level number of the currently selected map spot.
        /// Returns -1 if no map spot is selected.
        /// </summary>
        public int CurrentLevel => _selectedSpot != null ? _selectedSpot.levelNum : -1;

        public int LastUnlockedLevel
        {
            get
            {
                var lastUnlocked = _saveController.CurrentSaveData.LastUnlockedLevel;
                var maxLevels = _allLevels.MaxCampaignLevel;

                return lastUnlocked > maxLevels ? maxLevels : lastUnlocked;
            }
        }

        private bool IsBonusPageAvailable => _bonusPage != null
            && _mainPage != null
            && _allLevels != null
            && _allLevels.MaxCampaignLevel > _mainPage.GetLevelsCount();

        //private MetaSceneReferences _metaSceneReferences;
        private ILoadingController _loadingController;
        private IMetaInputSystem _metaInputSystem;
        private ISaveController _saveController;
        private IArtifactsService _artifactsService;
        private ICollectiblesService _collectiblesService;
        private AllLevelsSO _allLevels;

        private readonly List<MapPageView> _pages = new();
        private MapFlagView _mapFlag;
        
        private MapPageView _currentPage;
        private MapSpotView _selectedSpot;
        private MapPageView _mainPage;
        private MapPageView _bonusPage;
            
        private Dictionary<string, Sprite> _spotStateSprites = new();

        private Button _mapChangeButton;

        private CompositeDisposable _disposable = new();
        private GameObject _mapBG;

        private CurrentLevelSO _currentLevel;

        private bool _isMapShown;

        public event Action OnMapOpened;
        public event Action OnMapClosed;
        public event Action OnSpotSelected;
        public event Action OnPageChanged;
        public event Action OnBackRequested;

        [Inject]
        public void Construct(IMetaInputSystem metaInputSystem,
            ISaveController saveController,
            IArtifactsService artifactsService,
            IObjectResolver resolver)
        {
            _metaInputSystem = metaInputSystem;
            _saveController = saveController;
            _artifactsService = artifactsService;
            resolver.TryResolve(out _collectiblesService);
        }
        public UniTask Load(MapJson mapJson)
        {
            _mapFlag = mapJson.FlagView;
            _mapBG = mapJson.MapBg;
            _currentLevel = mapJson.CurrentLevel;
            _mainPage = mapJson.MainPage;
            _bonusPage = mapJson.BonusPage;
            _mapChangeButton = mapJson.MapChangeButton;

            _allLevels = mapJson.AllLevels;
            
            _mapChangeButton.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(0.1))
                .Subscribe(_ => ChangeMapPage()).AddTo(_disposable);
            
            _metaInputSystem.OnBackButtonPressed += HandleBackButtonPressed;
            
            foreach (var state in mapJson.MapSpotSpritesByState)
            {
                _spotStateSprites.Add(GenerateSpotStateSpriteKey(state.state, state.artifactState), state.sprite);
            }
            
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Initializes and displays the map interface by enabling necessary UI elements
        /// and setting the appropriate map page based on the available levels.
        /// </summary>
        /// <remarks>
        /// This method activates the map flag and background game objects, sets the
        /// internal state to indicate the map is being displayed, and determines which
        /// map page to show based on the total number of levels available.
        /// </remarks>
        public void ShowMap()
        {
            _isMapShown = true;

            _mapFlag.gameObject.SetActive(true);
            _mapBG.SetActive(true);

            OnMapOpened?.Invoke();

            int targetLevel;

            if (_currentLevel.WasVisited)
            {
                targetLevel = LastUnlockedLevel >= _currentLevel.LevelNum + 1
                    ? _currentLevel.LevelNum + 1
                    : _currentLevel.LevelNum;
            }
            else
            {
                targetLevel = LastUnlockedLevel;
            }

            targetLevel = Mathf.Min(targetLevel, LastUnlockedLevel);

            ShowPageView(
                _mainPage.GetLevelsCount() >= targetLevel || !IsBonusPageAvailable
                    ? _mainPage
                    : _bonusPage, LastUnlockedLevel, targetLevel);
        }

        /// <summary>
        /// Hides the map interface and disables all related UI elements.
        /// </summary>
        /// <remarks>
        /// This method deactivates the map flag, the map background, and the current map page's game objects.
        /// It also unsubscribes map spot event listeners from the current page and updates the internal state
        /// to reflect that the map is no longer active.
        /// </remarks>
        public void HideMap()
        {
            _isMapShown = false;

            _mapFlag.gameObject.SetActive(false);
            _mapBG.SetActive(false);
            _currentPage.gameObject.SetActive(false);

            foreach (var spot in _currentPage.MapSpots)
            {
                spot.OnReact -= SelectMapSpot;
            }

            OnMapClosed?.Invoke();
        }

        /// <summary>
        /// Changes the current map page view by toggling between the main map page
        /// and the bonus map page, updating the active page display accordingly.
        /// </summary>
        /// <remarks>
        /// When switching from the main map page to the bonus map page, the method
        /// determines which page to show and activates it while deactivating the
        /// current page. If transitioning from the main map page, and the highest
        /// level exceeded the levels defined on the main page, the last map spot
        /// of the current page is automatically selected.
        /// </remarks>
        public void ChangeMapPage()
        {
            var targetPage = _currentPage == _mainPage ? _bonusPage : _mainPage;

            if (targetPage == _bonusPage && !IsBonusPageAvailable)
            {
                return;
            }

            _currentPage.gameObject.SetActive(false);

            ShowPageView(targetPage, LastUnlockedLevel, LastUnlockedLevel);

            OnPageChanged?.Invoke();

            if (_currentPage == _mainPage && LastUnlockedLevel > _currentPage.GetLevelsCount())
            {
                SelectMapSpot(_currentPage.MapSpots.Last());
            }
        }

        private void HandleBackButtonPressed()
        {
            if (!_isMapShown || _currentPage == null)
            {
                return;
            }

            if (_currentPage == _bonusPage)
            {
                ChangeMapPage();
                return;
            }

            OnBackRequested?.Invoke();
        }

        /// <summary>
        /// Displays the specified map page view and configures the map spot views
        /// based on the player's current progress and the specified level number.
        /// </summary>
        /// <param name="pageView">The map page view to display.</param>
        /// <param name="maxlLevelNum">The level number up to which the map page should be configured.</param>
        /// <remarks>
        /// This method sets the current map page, activates the page view, and iterates
        /// through the map spots on the page. Each spot is configured based on its level
        /// relative to the player's current progress: passed, current, or blocked. Additionally,
        /// event handlers are subscribed to the map spots to allow level selection.
        /// </remarks>
        public void ShowPageView(MapPageView pageView, int maxlLevelNum, int selectedLevelNum)
        {
            _currentPage = pageView;
            pageView.gameObject.SetActive(true);

            MapSpotView matchedSpot = null;
            MapSpotView fallbackSpot = null;

            foreach (var spot in pageView.MapSpots)
            {
                spot.OnReact += SelectMapSpot;

                var spotArtifactState = GetSpotArtifactState(spot);
                
                _saveController.CurrentSaveData.Levels.TryGetValue(spot.levelNum, out var spotLevelData);

                if (spotLevelData is not null)
                {
                    spot.SetStars(spotLevelData.StarsCount);
                }
                else
                {
                    spot.SetStars(0);
                }

                if (spot.levelNum <= maxlLevelNum)
                {
                    spot.image.sprite = _spotStateSprites[GenerateSpotStateSpriteKey(MapSpotState.Passed, spotArtifactState)];
                }
                else
                {
                    spot.image.sprite = _spotStateSprites[GenerateSpotStateSpriteKey(MapSpotState.Block, spotArtifactState)];
                }

                if (spot.levelNum == selectedLevelNum)
                {
                    matchedSpot = spot;
                }

                if (spot.levelNum <= selectedLevelNum
                    && (fallbackSpot == null || spot.levelNum > fallbackSpot.levelNum))
                {
                    fallbackSpot = spot;
                }
            }

            var spotToSelect = matchedSpot != null ? matchedSpot : fallbackSpot;
            if (spotToSelect != null)
            {
                SelectMapSpot(spotToSelect);
                pageView.ScrollToFlag();
            }
        }

        /// <summary>
        /// Selects and highlights a specific map spot while updating the state of related UI elements.
        /// </summary>
        /// <param name="spot">The map spot to be selected and visually updated.</param>
        /// <remarks>
        /// This method updates the visual representation of the previously selected spot and the current spot,
        /// adjusts the position of the map flag to align with the selected spot, and sets the game-related metadata
        /// (e.g., current level and number of stars) for the selected spot.
        /// </remarks>
        public void SelectMapSpot(MapSpotView spot)
        {
            if (spot.levelNum > LastUnlockedLevel)
                return;

            if (_selectedSpot != null)
            {
                _selectedSpot.image.sprite = _spotStateSprites[
                       GenerateSpotStateSpriteKey(_selectedSpot.levelNum == LastUnlockedLevel ? MapSpotState.Selected
                       : MapSpotState.Passed, GetSpotArtifactState(_selectedSpot))];
            }

            OnSpotSelected?.Invoke();
            _mapFlag.transform.SetParent(spot.transform);
            _mapFlag.rect.position = spot.rect.position;
            
            _saveController.CurrentSaveData.Levels.TryGetValue(spot.levelNum, out var selectedLevel);

            var stars = 0;

            if (selectedLevel != null)
            {
                stars = selectedLevel.StarsCount;
            }

            foreach (var star in _mapFlag.stars)
            {
                star.SetActive(false);
            }

            for (int i = 0; i < stars; i++)
            {
                if (i < _mapFlag.stars.Count)
                {
                    _mapFlag.stars[i].SetActive(true);
                }
            }
            
            _selectedSpot = spot;

            var levelPair = _allLevels.AllLevels
                .FirstOrDefault(level => level.levelNum == spot.levelNum);

            if (levelPair.level != null)
            {
                _currentLevel.CurrentLevel = levelPair.level;
                _currentLevel.LevelNum = levelPair.levelNum;
            }
            
            spot.image.sprite = _spotStateSprites[GenerateSpotStateSpriteKey(MapSpotState.Selected, GetSpotArtifactState(spot))];
        }

        /// <summary>
        /// Determines the artifact state for a given map spot based on whether the artifact has been received.
        /// </summary>
        /// <param name="spotView">The view object representing the map spot, which contains information about the associated artifact.</param>
        /// <returns>The current state of the artifact. See <see cref="MapSpotArtifactState"/>.</returns>
        /// <remarks>
        private MapSpotArtifactState GetSpotArtifactState(MapSpotView spotView)
        {
            var levelPair = _allLevels.AllLevels
                .FirstOrDefault(level => level.levelNum == spotView.levelNum);

            if (_collectiblesService != null && levelPair.MapTrophy != null
                && levelPair.MapTrophy.Type == CollectibleType.Trophy)
            {
                return _collectiblesService.IsFullyCollected(levelPair.MapTrophy)
                    ? MapSpotArtifactState.Received
                    : MapSpotArtifactState.Available;
            }

            if (levelPair.ArtifactPartDataSO == null)
            {
                return MapSpotArtifactState.None;
            }
            
            if (_artifactsService.PartIsReceived(levelPair.ArtifactPartDataSO.Name))
            {
                return MapSpotArtifactState.Received;
            }

            return MapSpotArtifactState.Available;
        }

        /// <summary>
        /// Generates a unique sprite key based on the provided spot state and artifact state.
        /// </summary>
        /// <param name="state">The current state of the map spot. See <see cref="MapSpotState"/>.</param>
        /// <param name="artifactState">The current state of the artifact associated with the map spot. See <see cref="MapSpotArtifactState"/>.</param>
        /// <returns>A string representing the combined sprite key in the format 'State_ArtifactState'.</returns>
        private string GenerateSpotStateSpriteKey(MapSpotState state, MapSpotArtifactState artifactState)
        {
            return $"{state}_{artifactState}";
        }
        
        public void TryShowMapChangeButton()
        {
            var isLevelsAvailable = LastUnlockedLevel > _mainPage.MapSpots.Count;
            var isOfferAvailable = PlayerPrefs.HasKey("AllLevelsBuyKey") 
                                   && PlayerPrefs.GetString("AllLevelsBuyKey") == "true";
    
            if (isLevelsAvailable && isOfferAvailable)
                _mapChangeButton.gameObject.SetActive(true);
        }

        public MapSpotView GetMapSpot(int level)
        {
            foreach(var spot in _mainPage.MapSpots.Concat(_bonusPage.MapSpots))
            {
                if (spot.levelNum == level)
                    return spot;
            }

            return null;
        }

        public MapSpotView GetSelectedMapSpot()
        {
            return _selectedSpot;
        }

        public int GetCurrentLevel()
        {
            return CurrentLevel;
        }

        public void Dispose()
        {
            if (_currentPage != null && _currentPage.MapSpots != null)
            {
                foreach (var spot in _currentPage.MapSpots)
                {
                    if (spot != null)
                    {
                        spot.OnReact -= SelectMapSpot;
                    }
                }
            }

            if (_metaInputSystem != null)
            {
                _metaInputSystem.OnBackButtonPressed -= HandleBackButtonPressed;
            }

            _disposable.Clear();
        }
    }

    [Serializable]
    public class MapJson
    {
        public Button MapChangeButton;
        public List<MapSpotSpriteByState> MapSpotSpritesByState;
        public MapFlagView FlagView;
        public GameObject MapBg;
        public CurrentLevelSO CurrentLevel;
        public MapPageView MainPage;
        public MapPageView BonusPage;
        public AllLevelsSO AllLevels;

        public MapJson(Button mapChangeButton, List<MapSpotSpriteByState> mapSpotSpritesByState, MapFlagView flagView,
            GameObject mapBG, CurrentLevelSO currentLevel, MapPageView mainPage, MapPageView bonusPage, AllLevelsSO allLevels)

        {
            MapChangeButton = mapChangeButton;
            MapSpotSpritesByState = mapSpotSpritesByState;
            FlagView = flagView;
            MapBg = mapBG;
            CurrentLevel = currentLevel;
            MainPage = mainPage;
            BonusPage = bonusPage;
            AllLevels = allLevels;
        }
    }
}