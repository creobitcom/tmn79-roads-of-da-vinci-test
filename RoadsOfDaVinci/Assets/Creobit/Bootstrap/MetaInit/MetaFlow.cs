using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.LogoController;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using Creobit.Bootstrap.Core.Scripts.Runtime.DTO;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Bootstrap.Core.Scripts.Runtime.Meta;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.Loading;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.MetaInit
{
    public class MetaFlow : IAsyncStartable
    {
        private const string GuidesContentFolder = "Guide";

        private ILoadingController _loadingController;
        private IMetaController _metaController;
        private IMapController _mapController;
        private IMetaInputSystem _metaInputSystem;
        private IUIController _uiController;    
        private ILogoController _logoController;
        private IObjectResolver _objectResolver;
        private IComicsController _comicsController;
        private ICollectionRoomController _collectionRoomController;
        private IExtrasController _extrasController;
        private IGuidesController _guidesController;
        private RuntimeData _runtimeData;
        private IFadeController _fadeController;
        private MetaSceneReferences _metaSceneReferences;
        private BootstrapPrefabReferences _bootstrapPrefabReferences;
        private GameSwitcher _gameSwitcher;
        
        
        public MetaFlow(ILoadingController loadingController,
            IMetaController metaController,
            IMapController mapController, 
            IMetaInputSystem metaInputSystem,
            IUIController uiController, 
            ILogoController logoController, 
            IObjectResolver objectResolver, 
            IComicsController comicsController,
            ICollectionRoomController collectionRoomController,
            IExtrasController extrasController,
            IGuidesController guidesController,
            RuntimeData runtimeData,
            IFadeController fadeController,
            GameSwitcher gameSwitcher,
            MetaSceneReferences metaSceneReferences,//maby remove
            BootstrapPrefabReferences bootstrapPrefabReferences
            )
        {
            _loadingController = loadingController;
            _metaController = metaController;
            _mapController = mapController;
            _metaInputSystem = metaInputSystem;
            _uiController = uiController;
            _logoController = logoController;
            _objectResolver = objectResolver;
            _comicsController = comicsController;
            _collectionRoomController = collectionRoomController;
            _extrasController = extrasController;
            _guidesController = guidesController;
            _runtimeData = runtimeData;
            _fadeController = fadeController;
            _gameSwitcher = gameSwitcher;
            
            _metaSceneReferences = metaSceneReferences;
            _bootstrapPrefabReferences = bootstrapPrefabReferences;
        }

        public async UniTask StartAsync(CancellationToken cancellation = new())
        {
            _metaSceneReferences.SetSwitcher(_gameSwitcher);
            _loadingController.AddLoadingTask(LoadStarterPanels);
            
            _objectResolver.Inject(_uiController);
            _loadingController.AddLoadingTask(LoadTMNServices);

            bool showLoadingView = _runtimeData.MetaState != null;

            await _loadingController.ExecuteAllLoadingTasks(showLoadingView);

            _fadeController.FadeOut().Forget();
        }
        

        private List<GuideLocationData> BuildGuideLocations()
        {
            var locations = _metaSceneReferences.LocationNamesByNum;

            return locations == null
                ? new List<GuideLocationData>()
                : locations
                    .Select(location => new GuideLocationData(location.num, location.locationName,
                        location.guideIconSprite))
                    .ToList();
        }

        private UniTask LoadTMNServices()
        {
            if (_metaSceneReferences.LogoPosition != null)
            {
                _objectResolver.InjectGameObject(_metaSceneReferences.LogoPosition.gameObject);
            }

            if (_logoController is LogoController logoController)
            {
                string gameName = _gameSwitcher?.CurrentGame?.Value?.GameName;
                if (!string.IsNullOrEmpty(gameName))
                {
                    logoController.GameFolder = gameName;
                }
            }

            _loadingController.AddServicesToLoad(_logoController,
                _metaController);
            var mapJson = new MapJson(_metaSceneReferences.MapChangeButton,
                _metaSceneReferences.MapSpotSpritesByStates, _metaSceneReferences.MapFlag,
                _metaSceneReferences.MapBackground, _metaSceneReferences.CurrentLevel, _metaSceneReferences.MainMapPage,
                _metaSceneReferences.BonusMapPage, _metaSceneReferences.allLevels);
            _loadingController.AddServicesToLoadWith(mapJson, _mapController);
            
            var extrasJson =
                new ExtrasControllerJson(_metaSceneReferences.ExtrasMusicPanel, _metaSceneReferences.ExtrasPanel);
            _loadingController.AddServicesToLoadWith(extrasJson, _extrasController);

            var guidesJson = new GuidesControllerJson(
                _metaSceneReferences.GuidesMainPage,
                _metaSceneReferences.GuidesSelectLevel,
                _metaSceneReferences.GuidesUI,
                _metaSceneReferences.GameplayGuidePanel,
                _metaSceneReferences.MetaGuideLevelRefs,
                _metaSceneReferences.GuidesLevelRefs,
                BuildGuideLocations(),
                _metaSceneReferences.allLevels,
                _metaSceneReferences.MetaCanvas.transform,
                GuidesContentFolder);
            _loadingController.AddServicesToLoadWith(guidesJson, _guidesController);

            var collectionJson = new CollectionRoomJson(_metaSceneReferences.TooltipView,
                _metaSceneReferences.MetaCanvas, _metaSceneReferences.MainCamera);
            _loadingController.AddServicesToLoadWith(collectionJson, _collectionRoomController);
            
            var comicsJson = new ComicsJson(_metaSceneReferences.ComicsRoot,
                _metaSceneReferences.ComicsUI, _metaSceneReferences.MetaCanvas);
            _loadingController.AddServicesToLoadWith(comicsJson, _comicsController);
            
            _loadingController.AddServicesToLoadWith(_metaSceneReferences.MainCamera, _metaInputSystem);

            
            return UniTask.CompletedTask;

        }
        
        private UniTask LoadStarterPanels()
        {
            var starterUILoader = Object.FindFirstObjectByType<UILoader>();

            if (starterUILoader == null)
            {
                return UniTask.CompletedTask;
            }

            _objectResolver.InjectGameObject(starterUILoader.gameObject);

            // _loadingController.AddServicesToLoad(_cameraController);

            _loadingController.AddLoadingTask(starterUILoader.LoadPanels);

            return UniTask.CompletedTask;
        }

    }
}
