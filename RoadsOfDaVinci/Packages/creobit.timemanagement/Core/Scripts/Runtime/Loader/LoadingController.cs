using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Loader
{
    public class LoadingController : ILoadingController
    {
        private ILoadingService _loadingService;
        private LoaderSceneReferences _loaderSceneReferences;

        private readonly List<Func<UniTask>> _loadingTasks = new ();
        
        public event Action GameLoaded = delegate { };

        [Inject]
        private void Construct(ILoadingService loadingService,
            LoaderSceneReferences loaderSceneReferences)
        {
            _loadingService = loadingService;
            _loaderSceneReferences = loaderSceneReferences;
        }

        public async UniTask<T> LoadAssetWithProgress<T>(AsyncOperationHandle<T> handle)
        {
            var result = await handle.ToUniTask(_loaderSceneReferences.LoadingView);
            
            _loaderSceneReferences.LoadingView.Report(0.5f);
            
            return result;
        }

        public void AddServicesToLoad(params ILoadUnit[] loadUnits)
        {
            // AddLoadingTask(() => _loadingService.(_loaderSceneReferences.LoadingView, loadUnits));
        }

        public async UniTask ExecuteAllLoadingTasks()
        {
            _loaderSceneReferences.LoadingScreenTransform.gameObject.SetActive(true);

            for (var i = 0; i < _loadingTasks.Count; i++)
            {
                await _loadingTasks[i]();
            }
            
            GameLoaded?.Invoke();
            
            _loadingTasks.Clear();
            
            _loaderSceneReferences.LoadingScreenTransform.gameObject.SetActive(false);
        }

        public void AddLoadingTask(Func<UniTask> uniTask)
        {
            _loadingTasks.Add(uniTask);
        }
    }
}