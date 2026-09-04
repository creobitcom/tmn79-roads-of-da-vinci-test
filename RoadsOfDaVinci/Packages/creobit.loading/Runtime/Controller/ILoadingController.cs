using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Creobit.Loading
{
    public interface ILoadingController
    {
        public event Action LoadingFinished;
        
        public void RegisterListener(ILoadingEventsListener listener);
        
        public void UnregisterListener(ILoadingEventsListener listener);

        public void AddServicesToLoad(params ILoadUnit[] loadUnits);

        public void AddServicesToLoadWith<T>(T param, params ILoadUnit<T>[] loadUnits);

        public void LoadAssetWithProgress<A>(AsyncOperationHandle<A> handle);

        public void ScheduleSceneLoading(int buildIndex);
        public void ScheduleSceneLoading(string sceneName);

        public UniTask LoadScheduledScene();

        public void AddLoadingTask(Func<UniTask> loadingTask);

        public UniTask ExecuteAllLoadingTasks(bool withLoadingView = true);

        public UniTask Init(GameObject loadingView);
    }
}