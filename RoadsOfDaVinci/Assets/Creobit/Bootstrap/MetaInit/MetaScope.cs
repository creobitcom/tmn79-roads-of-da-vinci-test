#if TMN_Module
using _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles.MetaUI;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.LogoController;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Controller;
#endif
using Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.BootstrapInit;
using Creobit.Bootstrap.Core.Scripts.Runtime.Meta;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.MetaInit
{
    public class MetaScope : LifetimeScope
    {
        [SerializeField] [HideLabel] [InlineProperty]
        private MetaSceneReferences _sceneReferences;
        [SerializeField] private CollectiblesDatabaseSO _collectiblesDatabase;

        protected override void Configure(IContainerBuilder builder)
        {
            var tmnSelectParent = Find<BootstrapScope>() as BootstrapScope;
            builder.RegisterInstance(tmnSelectParent.Switcher);

            // Register Instances
            builder.RegisterInstance(_sceneReferences);
            
#if TMN_Module
            builder.Register<IMetaInputSystem, MetaInputSystem>(Lifetime.Singleton);
            builder.Register<IMetaController, MetaController>(Lifetime.Singleton);
            builder.Register<IMapController, MapController>(Lifetime.Singleton);
            builder.Register<ILogoController, LogoController>(Lifetime.Singleton);
            builder.Register<IComicsController, ComicsController>(Lifetime.Singleton);
            builder.Register<IVideoCutsceneController, VideoCutsceneController>(Lifetime.Singleton);
            builder.Register<IGuidesController, GuidesController>(Lifetime.Singleton);
            builder.Register<ICollectionRoomController, CollectionRoomController>(Lifetime.Singleton);
            builder.Register<IExtrasController, ExtrasController>(Lifetime.Singleton);
            builder.RegisterInstance(_collectiblesDatabase);
            builder.Register(_ => _sceneReferences.allLevels, Lifetime.Transient);

            builder.RegisterEntryPoint<MetaAnalytics>();
#endif
            builder.RegisterEntryPoint<MetaFlow>();

        }
    }
}