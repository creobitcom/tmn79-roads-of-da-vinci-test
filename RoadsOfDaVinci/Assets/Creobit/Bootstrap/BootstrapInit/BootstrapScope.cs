using _8floor.TimeManagement.Artifacts.Runtime.Providers;
using _8floor.TimeManagement.Artifacts.Runtime.Service;
using _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using Creobit.AddressablesController;
using Creobit.Audio;
using Creobit.Bootstrap.Core.Scripts.Runtime.Analytics;
using Creobit.Bootstrap.Core.Scripts.Runtime.AppDataConfigurationSystem;
using Creobit.Bootstrap.Core.Scripts.Runtime.DTO;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Settings;
using Creobit.Bootstrap.Core.Scripts.Runtime.Splash;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.EditionsUpgrade;
using Creobit.Loading;
using Creobit.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.BootstrapInit
{
    public class BootstrapScope : LifetimeScope
    {
        [FormerlySerializedAs("_sceneReferences")]
        [SerializeField]
        [HideLabel]
        [InlineProperty]
        private BootstrapPrefabReferences _prefabReferences;
        [SerializeField] private GameSwitcher _switcher;
        public GameSwitcher Switcher => _switcher;
        protected override void Configure(IContainerBuilder builder)
        {
            // RegisterInstances
            var switcher = FindFirstObjectByType<GameSwitcher>(FindObjectsInactive.Include);
            _switcher = switcher;
            
            autoInjectGameObjects.Add(switcher.gameObject);
            
            builder.RegisterInstance(switcher);
            builder.RegisterInstance(_prefabReferences);
            builder.RegisterInstance(_prefabReferences.Artifacts);

            // RegisterMonoBehaviours
            builder.RegisterInstance(_prefabReferences.ApplicationEventsInterceptor);
            var privacy = FindFirstObjectByType<PrivacyPolicyManager>(FindObjectsInactive.Include);
            builder.RegisterInstance<IPrivacyPolicyManager, PrivacyPolicyManager>(privacy);

            // RegisterControllers
            builder.Register<IAppDataConfigurationController, AppDataConfigurationController>(Lifetime.Singleton);
            builder.Register<IAddressablesController, AddressablesController.AddressablesController>(Lifetime.Singleton);
            builder.Register<IFadeController, FadeController>(Lifetime.Singleton);
            builder.Register<ILoadingController, LoadingController>(Lifetime.Singleton);
            builder.Register<IPlayerProfilesController, PlayerProfilesController>(Lifetime.Singleton);
            builder.Register<IArtifactsService, ArtifactsService>(Lifetime.Singleton);
            builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
            builder.Register<IGameSettingsController, GameSettingsController>(Lifetime.Singleton);
            builder.Register<IPlayerPrefsSaveProvider, FileSaveProvider>(Lifetime.Singleton);
            builder.Register<ISaveController, SaveController>(Lifetime.Singleton);
            builder.Register<ISplashController, SplashController>(Lifetime.Singleton);
            builder.Register<RuntimeData>(Lifetime.Singleton);
            builder.Register<ICollectiblesService, CollectiblesService>(Lifetime.Singleton);

            builder.Register<IAchievementsController, AchievementsController>(Lifetime.Singleton);

            builder.Register<IUIController, UIController>(Lifetime.Singleton);

            builder.Register<AnalyticsRecorder>(Lifetime.Singleton);

            builder.RegisterEntryPoint<BootstrapFlow>();
        }
    }
}