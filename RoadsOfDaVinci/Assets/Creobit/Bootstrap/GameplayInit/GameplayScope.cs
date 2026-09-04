using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.Boosters;
using _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.Mediators;
using _8floor.TimeManagement.Core.Scripts.Runtime.Minigame;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.CraftController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectHint;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.Dialogues.Core.Scripts.Runtime.Controller;
using Creobit.Loading;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ILoadingController = _8floor.TimeManagement.Core.Scripts.Runtime.Loader.ILoadingController;
using LoadingController = _8floor.TimeManagement.Core.Scripts.Runtime.Loader.LoadingController;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.GameplayInit
{
    public class GameplayScope : LifetimeScope
    {
        [SerializeField]
        // [HideLabel]
        [InlineProperty]
        private GameplaySceneReferences _sceneReferences;

#if TMN_Module
        [SerializeField]
        private LevelController _levelController;
        [SerializeField]
        private GameplayIntervalsController _intervalsController;
        [SerializeField]
        private _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils.GameplaySceneReferences _tmnReferences;
#endif

        protected override void Configure(IContainerBuilder builder)
        {
            // Register Instances
            builder.RegisterInstance(_sceneReferences);
#if TMN_Module
            builder.RegisterInstance(_intervalsController).AsImplementedInterfaces();
            builder.RegisterInstance(_tmnReferences).AsSelf();
            builder.RegisterInstance(_tmnReferences.LoaderSceneReferences).AsSelf();
            
            builder.RegisterComponent(_levelController).AsImplementedInterfaces();
            
            builder.Register<IGameplayInputSystem, GameplayInputSystem>(Lifetime.Scoped);
			builder.Register<IReloadController, ReloadController>(Lifetime.Scoped);
			builder.Register<ILevelLoader, LevelLoader>(Lifetime.Scoped);
			builder.Register<IGameResourcesSystem, GameResourcesSystem>(Lifetime.Scoped);
			builder.Register<ITutorialController, TutorialController>(Lifetime.Scoped);
			builder.Register<IObjectViewController, ObjectViewController>(Lifetime.Scoped);
			builder.Register<IComplexObjectController, ComplexObjectController>(Lifetime.Scoped);
			builder.Register<IPauseController, PauseController>(Lifetime.Scoped);
			builder.Register<IMovableObjectTaskBadgeController, MovableObjectTaskBadgeController>(Lifetime.Scoped);
			builder.Register<ITooltipController, TooltipController>(Lifetime.Scoped);
			builder.Register<ILevelTasksController, LevelTasksController>(Lifetime.Scoped);
			builder.Register<LevelTaskPresenter>(Lifetime.Scoped);
			builder.Register<ILevelTimer, LevelTimer>(Lifetime.Scoped);
			builder.Register<IArtifactPartsController, ArtifactPartsController>(Lifetime.Scoped);
			builder.Register<IComplexObjectProvider, ComplexObjectProvider>(Lifetime.Scoped);
			builder.Register<CocPresenter>(Lifetime.Scoped);
			builder.Register<BoostersPresenter>(Lifetime.Scoped);
			builder.Register<IBoostersController, BoostersController>(Lifetime.Scoped);
			builder.Register<BaseUnitsStatesController>(Lifetime.Scoped);
			builder.Register<ILoadingController, LoadingController>(Lifetime.Scoped);
			builder.Register<DialogueTutorialsMediator>(Lifetime.Scoped);
			builder.Register<ILoadingService, LoadingService>(Lifetime.Scoped);
			builder.Register<IDialogueController, DisabledDialogueController>(Lifetime.Scoped);
            builder.Register<IInactiveObjectController, InactiveObjectController>(Lifetime.Scoped);
			builder.Register<MinigameController>(Lifetime.Scoped);
			builder.Register<ICutsceneController, CutsceneController>(Lifetime.Scoped);
			builder.Register<CollectiblesGameplayPresenter>(Lifetime.Scoped);
			builder.Register<ICraftController, CraftController>(Lifetime.Scoped);
			builder.Register<IInactiveAreaTransferController, InactiveAreaTransferController>(Lifetime.Scoped);
			builder.Register<IBubbleController, BubbleController>(Lifetime.Scoped);
			builder.Register<ObjectHintController>(Lifetime.Scoped);

            // Панель особых предметов лежит в сцене и в контейнер не регистрируется, но её вьюхе
            // (SpecialItemsPanelView) нужен ITooltipController — тултип с именем предмета при
            // наведении. Инжектим точечно по уже существующей ссылке: так не нужен ни отдельный
            // сервис, ни запись в autoInjectGameObjects. Панель стартует выключенной, но Inject
            // работает по объекту, а не по активности.
            if (_tmnReferences.InventoryResourcesViewRefs != null)
            {
                builder.RegisterBuildCallback(container =>
                    container.Inject(_tmnReferences.InventoryResourcesViewRefs));
            }

            builder.RegisterEntryPoint<GameplayAnalytics>();
#endif
            builder.RegisterEntryPoint<GameplayFlow>();
        }
    }
}