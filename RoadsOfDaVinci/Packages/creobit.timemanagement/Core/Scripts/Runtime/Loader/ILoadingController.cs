using System;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Loader
{
    public interface ILoadingController
    {
        public event Action GameLoaded;
        
        public void AddServicesToLoad(params ILoadUnit[] loadUnits);

        public UniTask<T> LoadAssetWithProgress<T>(AsyncOperationHandle<T> handle);

        public void AddLoadingTask(Func<UniTask> loadingTask);

        public UniTask ExecuteAllLoadingTasks();
    }
}