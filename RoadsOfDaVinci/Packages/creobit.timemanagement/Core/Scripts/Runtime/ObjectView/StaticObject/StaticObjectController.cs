using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production.Repair;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.Diseasable;
using Creobit.Audio;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    [Serializable]
    public class StaticObjectController : IReloadable, IDisposable
    {
        private UnitAvailabilityController _unitAvailabilityController;
        private IGameplayIntervalsController _intervalsController;
        private ILevelController _levelController;
        private ILevelLoader _levelLoader;
        private ObjectSpecialTagController _objectSpecialTagController;
        private IObjectViewController _objectViewController;
        private IAudioService _audioController;
        private IReloadController _reloadController;
        
        private List<StaticObjectView> _staticObjects = new();
        private readonly CompositeDisposable _disposable = new();
        
        private ProductionController _productionController;

        public IReadOnlyList<StaticObjectView> StaticObjects => _staticObjects;
        public event Action<StaticObjectView> AddedStaticObject;
        public event Action<StaticObjectView> RemovedStaticObject;

        public StaticObjectController(IReloadController reloadController,
            MovableObjectController movableObjectController,
            ILevelController levelController,
            IObjectViewController objectViewController,
            IGameplayIntervalsController gameplayIntervalsController,
            IAudioService audioController,
            ILevelLoader levelLoader)
        {
            _reloadController = reloadController;
            _levelController = levelController;
            _objectViewController = objectViewController;
            _intervalsController = gameplayIntervalsController;
            _audioController = audioController;
            _levelLoader = levelLoader;

            _unitAvailabilityController = movableObjectController.GetUnitAvailabilityController();
        }
        
        public void Load()
        {
            _objectSpecialTagController = _objectViewController.GetObjectSpecialTagController();
             
            _productionController = new ProductionController(_intervalsController, _audioController, _levelController, 
                this, _objectSpecialTagController, _levelLoader, _unitAvailabilityController);
            
            _productionController.Load();
            
            _reloadController.AddReloadableObject(this);
            
            Enable();
        }
        
        public void Enable()
        {
            Log.Gameplay.Info("Static Object System loaded");
        }
        
        public async UniTask Reload()
        {
            Dispose();
            Load();
        }

        public void Dispose()
        {
            Log.Gameplay.Info("Static Object System disposed");
            
            _staticObjects.Clear();
            _reloadController.RemoveReloadableObject(this);

            _productionController.Dispose();
            _disposable.Dispose();
        }

        public void AddStaticObject(StaticObjectView staticObjectView)
        {
            _staticObjects.Add(staticObjectView);
            
            AddedStaticObject?.Invoke(staticObjectView);
        }

        public void RemoveStaticObject(StaticObjectView staticObjectView)
        {
            _staticObjects.Remove(staticObjectView);

            RemovedStaticObject?.Invoke(staticObjectView);
        }

        public DiseasableController GetDiseasableController() => _productionController?.GetDiseasableController();

        public BreakableController GetBreakableController() => _productionController?.GetBreakableController();

        public DiseasableBridge GetDiseasableBridge() => _productionController?.GetDiseasableBridge();

        public BreakableBridge GetBreakableBridge() => _productionController?.GetBreakableBridge();

        private void OnObjectViewDestroyedAfterFinalInteraction(ObjectView objectView)
        {
            RemoveStaticObject(objectView as StaticObjectView);
        }
    }
}