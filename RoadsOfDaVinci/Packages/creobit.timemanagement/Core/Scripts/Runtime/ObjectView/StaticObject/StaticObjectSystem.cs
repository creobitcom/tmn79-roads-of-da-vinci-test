using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Cysharp.Threading.Tasks;
using R3;
using VContainer;
using Log = Creobit.Logger.Log;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    [Serializable]
    public class StaticObjectSystem : IStaticObjectSystem
    {
        private List<StaticObjectView> _staticObjects = new();
        private readonly CompositeDisposable _disposable = new();

        private IReloadController _reloadController;
        public IReadOnlyList<StaticObjectView> StaticObjects => _staticObjects;
        public event Action<StaticObjectView> AddedStaticObject;
        public event Action<StaticObjectView> RemovedStaticObject;
        
        [Inject]
        private void Construct(IReloadController reloadController)
        {
            _reloadController = reloadController;
        }
        
        public UniTask Load()
        {
            _reloadController.AddReloadableObject(this);
            
            Enable();
            return UniTask.CompletedTask;
        }
        
        public void Enable()
        {
            Log.Gameplay.Info("Static Object System loaded");
        }
        
        public async UniTask Reload()
        {
            Dispose();
            await Load();
        }

        public void Dispose()
        {
            Log.Gameplay.Info("Static Object System disposed");
            
            _staticObjects.Clear();
            
            _reloadController.RemoveReloadableObject(this);

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

        private void OnObjectViewDestroyedAfterFinalInteraction(ObjectView objectView)
        {
            RemoveStaticObject(objectView as StaticObjectView);
        }
    }
}