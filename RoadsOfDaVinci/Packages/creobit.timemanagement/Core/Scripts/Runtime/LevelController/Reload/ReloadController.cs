using System;
using System.Collections.Generic;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Loading;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload
{
    public class ReloadController : IReloadController
    {
        private ILoadingController _loadingController;
        private IFadeController _fadeController;
        
        private readonly List<IReloadable> _reloadableObjects = new();

        public event Action ReloadRequested = delegate { };

        public ReloadController(ILoadingController loadingController,
            IFadeController fadeController)
        {
            _loadingController = loadingController;
            _fadeController = fadeController;
        }

        public void AddReloadableObject(IReloadable reloadableObject, int pos=-1)
        {
            if (pos != -1)
            {
                _reloadableObjects.Insert(pos, reloadableObject);
            }
            else
            {
                _reloadableObjects.Add(reloadableObject);
            }
        }

        public void RemoveReloadableObject(IReloadable reloadableObject)
        {
            _reloadableObjects.Remove(reloadableObject);
        }

        public async void ReloadLevel()
        {
            _loadingController.AddLoadingTask(ReloadObjects);

            await _loadingController.ExecuteAllLoadingTasks();

            _fadeController.FadeOut().Forget();
        }

        public async UniTask ReloadObjects()
        {
            var reloadTasks = new List<UniTask>(_reloadableObjects.Count);

            foreach (var reloadableObject in _reloadableObjects.ToArray())
            {
                reloadTasks.Add(ReloadSafely(reloadableObject));
            }

            await UniTask.WhenAll(reloadTasks);

            ReloadRequested?.Invoke();
        }

        private static async UniTask ReloadSafely(IReloadable reloadableObject)
        {
            try
            {
                await reloadableObject.Reload();
            }
            catch (Exception exception)
            {
                Log.Gameplay.Error(exception);
            }
        }
    }
}