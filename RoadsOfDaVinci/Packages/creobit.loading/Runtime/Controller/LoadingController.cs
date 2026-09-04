using System;
using System.Collections.Generic;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Creobit.Loading
{
    public sealed class LoadingController : ILoadingController
    {
        private readonly HashSet<ILoadingEventsListener> _listeners = new();
        
        private ILoadingService _loadingService;
        private LoadingView _loadingView;
        public event Action LoadingFinished = delegate { };

        public void RegisterListener(ILoadingEventsListener listener)
        {
            _listeners.Add(listener);
        }

        public void UnregisterListener(ILoadingEventsListener listener)
        {
            _listeners.Remove(listener);
        }
        
        public void LoadAssetWithProgress<T>(AsyncOperationHandle<T> handle)
        {
            _loadingService.LoadAssetWithProgress(handle);
        }

        public void AddServicesToLoad(params ILoadUnit[] loadUnits)
        {
            _loadingService.AddServicesToLoad(loadUnits);
        }

        public void AddServicesToLoadWith<T>(T param, params ILoadUnit<T>[] loadUnits)
        {
            _loadingService.AddServicesToLoadWith(param, loadUnits);
        }

        public void ScheduleSceneLoading(int buildIndex)
        {
            _loadingService.ScheduleSceneLoading(buildIndex);
        }

        public void ScheduleSceneLoading(string sceneName)
        {
            _loadingService.ScheduleSceneLoading(sceneName);
        }
        
        public async UniTask LoadScheduledScene()
        {
            foreach (var listener in _listeners)
            {
                await listener.OnLoadScheduledSceneStarted();
            }
            
            await _loadingService.LoadScheduledScene();
        }

        public async UniTask ExecuteAllLoadingTasks(bool withLoadingView = true)
        {
            Log.Bootstrap.Info("LOADING STARTED");

            await _loadingService.ExecuteAllLoadingTasks(withLoadingView ? _loadingView : null);

            Log.Bootstrap.Info("LOADING FINISHED");

            foreach (var listener in _listeners)
            {
                await listener.OnLoadingFinished();
            }
            
            LoadingFinished?.Invoke();
        }

        public void AddLoadingTask(Func<UniTask> uniTask)
        {
            _loadingService.AddLoadingTask(uniTask);
        }

        public async UniTask Init(GameObject loadingView)
        {
            _loadingService = new LoadingService();

            _loadingView = Object.Instantiate(loadingView).GetComponent<LoadingView>();
            _loadingView.gameObject.SetActive(false);

            Object.DontDestroyOnLoad(_loadingView);
        }
    }
}