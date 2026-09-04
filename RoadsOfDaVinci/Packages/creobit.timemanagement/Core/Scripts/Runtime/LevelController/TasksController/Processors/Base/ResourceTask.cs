using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using ObservableCollections;
using R3;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base
{
    public abstract class ResourceTask : LevelTask
    {
        protected readonly ResourceBaseSO ObservableResource;

        [Inject] 
        protected IGameResourcesSystem GameResourcesSystem;

        protected ResourceTask(LevelTaskData levelTaskData, ResourceBaseSO observableResource, bool showOnlyAmount) : base(levelTaskData, showOnlyAmount)
        {
            ObservableResource = observableResource;
            _showOnlyAmount = showOnlyAmount;
        }

        public override void Setup()
        {
            base.Setup();
            
            SetupTask();
        }

        public override void Dispose()
        {
            base.Dispose();
            
            DisposeTask();
        }

        public override void ChangeProgress(short amount)
        {
            ChangeResourceProgress(amount);
        }

        protected virtual void DisposeTask()
        { }

        protected virtual void SetupTask()
        {
            GameResourcesSystem.CheckResourceIsLoaded(ObservableResource);
            ChangeProgress((short)GameResourcesSystem.GetResourceAmount(ObservableResource));
            
            var resourcesList = ObservableResource is InventoryResource
                ? GameResourcesSystem.InventoryResources
                : GameResourcesSystem.Resources;
            
            resourcesList.ObserveReplace().
                Where(resource => resource.NewValue.Key == ObservableResource)
                .Subscribe((replacementEvent) =>
                { 
                    ObservableResourceChanged(replacementEvent.NewValue.Value, replacementEvent.OldValue.Value);
                }).AddTo(CompositeDisposable);
        }

        protected abstract void ChangeResourceProgress(short amount);

        protected abstract void ObservableResourceChanged(int newValue, int oldValue);
    }
}