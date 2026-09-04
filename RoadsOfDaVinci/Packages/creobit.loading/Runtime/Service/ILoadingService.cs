using System;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Creobit.Loading
{
    public interface ILoadingService
    {
        public void AddServicesToLoad(params ILoadUnit[] loadUnits);
        public void AddServicesToLoadWith<T>(T param, params ILoadUnit<T>[] loadUnits);
        public void AddLoadingTask(Func<UniTask> uniTask);
        public void LoadAssetWithProgress<T>(AsyncOperationHandle<T> handle);
        public void ScheduleSceneLoading(int buildIndex);
        public void ScheduleSceneLoading(string sceneName);
        public UniTask LoadScheduledScene();
        public UniTask ExecuteAllLoadingTasks(LoadingView loadingView);
    }
}