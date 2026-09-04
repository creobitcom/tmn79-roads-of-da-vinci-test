using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Boosters;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.Mediators;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.CraftController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectHint;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using Creobit.Dialogues.Core.Scripts.Runtime.Controller;
using Creobit.Loading;
using Creobit.UI;
using Cysharp.Threading.Tasks; 
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.GameplayInit
{
    public class GameplayFlow : IAsyncStartable
    {
        private readonly ILoadingController _loadingController;
        private readonly ILevelController _levelController;
        private readonly IObjectResolver _objectResolver;
        private readonly ILevelLoader _levelLoader;
        private readonly IGameplayInputSystem _gameplayInputSystem;
        private readonly IMovableObjectTaskBadgeController _movableObjectTaskBadgeController;
        private readonly IGameResourcesSystem _gameResourcesSystem;
        private readonly ITooltipController _tooltipController;
        private readonly ILevelTasksController _levelTasksController;
        private readonly LevelTaskPresenter _levelTaskPresenter;
        private readonly ILevelTimer _levelTimer;
        private readonly IArtifactPartsController _artifactPartsController;
        private readonly CocPresenter _cocPresenter;
        private readonly IDialogueController _dialogueController;
        private readonly ITutorialController _tutorialController;
        private readonly IObjectViewController _objectViewController;
        private readonly BoostersPresenter _boostersPresenter;
        private readonly IBoostersController _boostersController;
        private readonly IGameplayIntervalsController _intervalsController;
        private readonly IComplexObjectController _complexObjectController;
        private readonly DialogueTutorialsMediator _dialogueTutorialsMediator;
        private readonly ICutsceneController _cutsceneController;
        private readonly CollectiblesGameplayPresenter _collectiblesGameplayPresenter;
        private readonly ICraftController _craftController;
        private readonly ObjectHintController _selectionHintController;

        public GameplayFlow(ILoadingController loadingController,
            ILevelController levelController,
            IObjectResolver objectResolver,
            ILevelLoader levelLoader,
            IGameplayInputSystem gameplayInputSystem,
            IMovableObjectTaskBadgeController movableObjectTaskBadgeController,
            IGameResourcesSystem gameResourcesSystem,
            ITooltipController tooltipController,
            ILevelTasksController levelTasksController,
            LevelTaskPresenter levelTaskPresenter,
            ILevelTimer levelTimer,
            IArtifactPartsController artifactPartsController,
            CocPresenter cocPresenter,
            IDialogueController dialogueController,
            ITutorialController tutorialController,
            BoostersPresenter boostersPresenter,
            IBoostersController boostersController,
            IGameplayIntervalsController intervalsController,
            DialogueTutorialsMediator dialogueTutorialsMediator,
            IObjectViewController objectViewController,
            IComplexObjectController complexObjectController,
            ICutsceneController cutsceneController,
            ICraftController craftController,
            ObjectHintController selectionHintController,
            CollectiblesGameplayPresenter collectiblesGameplayPresenter)
        {
            _loadingController = loadingController;
            _levelController = levelController;
            _objectResolver = objectResolver;
            _levelLoader = levelLoader;
            _gameplayInputSystem = gameplayInputSystem;
            _movableObjectTaskBadgeController = movableObjectTaskBadgeController;
            _gameResourcesSystem = gameResourcesSystem;
            _tooltipController = tooltipController;
            _levelTasksController = levelTasksController;
            _levelTaskPresenter = levelTaskPresenter;
            _levelTimer = levelTimer;
            _artifactPartsController = artifactPartsController;
            _cocPresenter = cocPresenter;
            _dialogueController = dialogueController;
            _tutorialController = tutorialController;
            _boostersPresenter = boostersPresenter;
            _boostersController = boostersController;
            _intervalsController = intervalsController;
            _dialogueTutorialsMediator = dialogueTutorialsMediator;
            _objectViewController = objectViewController;
            _complexObjectController = complexObjectController;
            _cutsceneController = cutsceneController;
            _collectiblesGameplayPresenter = collectiblesGameplayPresenter;
            _craftController = craftController;
            _selectionHintController = selectionHintController;
        }

        public async UniTask StartAsync(CancellationToken cancellation = new())
        {
            _loadingController.AddServicesToLoad(_levelController);
            _loadingController.AddLoadingTask(LoadStarterPanels);
            _loadingController.AddLoadingTask(LoadTMNServices);

            await _loadingController.ExecuteAllLoadingTasks();
        }

        private UniTask LoadStarterPanels()
        {
            var starterUILoader = Object.FindFirstObjectByType<UILoader>();

            if (starterUILoader == null)
            {
                return UniTask.CompletedTask;
            }

            _objectResolver.InjectGameObject(starterUILoader.gameObject);

            _loadingController.AddLoadingTask(starterUILoader.LoadPanels);

            return UniTask.CompletedTask;
        }
        
        private UniTask LoadTMNServices()
        {
            _loadingController.AddServicesToLoad(_levelLoader,
                _objectViewController,
                _complexObjectController,
                _gameResourcesSystem,
                _movableObjectTaskBadgeController,
                _levelTimer,
                _artifactPartsController,
                _cocPresenter,
                _levelTaskPresenter,
                _levelTasksController,
                _gameplayInputSystem,
                _tutorialController,
                _dialogueTutorialsMediator,
                _boostersPresenter,
                _boostersController,
                _dialogueController,
                _intervalsController,
                _tooltipController,
                _cutsceneController,
                _craftController,
                _selectionHintController,
                _collectiblesGameplayPresenter);

            _loadingController.AddLoadingTask(() => _levelLoader.LoadLevel());

            return UniTask.CompletedTask;
        }
    }
}