using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Log = Creobit.Logger.Log;
using PlayerLoopHelper = Cysharp.Threading.Tasks.PlayerLoopHelper;

namespace Creobit.Loading
{
    public interface ILoadUnit
    {
        UniTask Load();
    }

    public interface ILoadUnit<in T>
    {
        UniTask Load(T param);
    }

    public sealed class LoadingService : ILoadingService
    {
        private readonly List<Func<UniTask>> _loadingTasks = new();
        private readonly Stopwatch _watch = new();

        private AsyncOperation _sceneOperation;

        private void OnLoadingBegin(object unit)
        {
            _watch.Restart();
            Log.Loading.Info($"{unit.GetType().Name} loading is started");
        }

        private async UniTask OnLoadingFinish(object unit, bool isError)
        {
            _watch.Stop();
            Log.Loading.Info($"{unit.GetType().Name} is {(isError ? "NOT " : "")}loaded with time {_watch.ElapsedMilliseconds}ms");

            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
            var mainThreadId = PlayerLoopHelper.MainThreadId;

            if (mainThreadId != currentThreadId)
            {
                _watch.Restart();
                Log.Loading.Info($"[THREAD] start switching from '{currentThreadId}' thread to main thread '{mainThreadId}'");

                await UniTask.SwitchToMainThread();

                _watch.Stop();
                Log.Loading.Info($"[THREAD] switch finished with time {_watch.ElapsedMilliseconds}");
            }
        }

        public void AddServicesToLoad(params ILoadUnit[] loadUnits)
        {
            // каждый юнит - отдельный шаг прогресса, иначе весь пакет выглядит как один рывок бара
            for (var i = 0; i < loadUnits.Length; i++)
            {
                var loadUnit = loadUnits[i];

                AddLoadingTask(() => BeginLoading(loadUnit));
            }
        }

        public void AddServicesToLoadWith<T>(T param, params ILoadUnit<T>[] loadUnits)
        {
            for (var i = 0; i < loadUnits.Length; i++)
            {
                var loadUnit = loadUnits[i];

                AddLoadingTask(() => BeginLoading(loadUnit, param));
            }
        }

        public void LoadAssetWithProgress<T>(AsyncOperationHandle<T> handle)
        {
            AddLoadingTask(() => handle.ToUniTask());
        }

        public void ScheduleSceneLoading(int buildIndex)
        {
            _sceneOperation = SceneManager.LoadSceneAsync(buildIndex);

            if (_sceneOperation == null)
            {
                return;
            }

            _sceneOperation.allowSceneActivation = false;
        }
        
        public void ScheduleSceneLoading(string sceneName)
        {
            _sceneOperation = SceneManager.LoadSceneAsync(sceneName);

            if (_sceneOperation == null)
            {
                return;
            }

            _sceneOperation.allowSceneActivation = false;
        }

        public async UniTask LoadScheduledScene()
        {
            if (_sceneOperation == null)
            {
                return;
            }

            _sceneOperation.allowSceneActivation = true;

            while (!_sceneOperation.isDone) await UniTask.Yield();

            _sceneOperation = null;
        }

        public void AddLoadingTask(Func<UniTask> uniTask)
        {
            _loadingTasks.Add(uniTask);
        }

        public async UniTask ExecuteAllLoadingTasks(LoadingView loadingView)
        {
            if (loadingView != null)
                loadingView.Activate();

            // Count растёт по ходу: задачи могут добавлять новые задачи.
            // Прогресс репортим ДО выполнения, иначе первый же кадр бара стартует с 1/Count.
            // Монотонность обеспечивает сам LoadingView, так что уменьшение доли от роста Count безопасно.
            for (var i = 0; i < _loadingTasks.Count; i++)
            {
                if (loadingView != null)
                    loadingView.Report((float)i / _loadingTasks.Count);

                await _loadingTasks[i]();

                if (loadingView != null)
                    // отдаём кадр, чтобы бар успел отрисоваться между тяжёлыми задачами
                    await UniTask.Yield();
            }

            _loadingTasks.Clear();

            if (_sceneOperation != null)
            {
                while (_sceneOperation.progress < 0.9f) await UniTask.Yield();
            }

            if (loadingView != null)
            {
                await loadingView.Finish();

                loadingView.Deactivate();
            }
        }

        private async UniTask BeginLoading(ILoadUnit loadUnit, bool skipExceptionThrow = false)
        {
            var isError = true;

            try
            {
                OnLoadingBegin(loadUnit);
                await loadUnit.Load();
                isError = false;
            }
            catch (Exception e)
            {
                Log.Loading.Error(e);
                
                if (!skipExceptionThrow)
                {
                    throw;
                }
            }
            finally
            {
                await OnLoadingFinish(loadUnit, isError);
            }
        }

        private async UniTask BeginLoading<T>(ILoadUnit<T> loadUnit, T param, bool skipExceptionThrow = false)
        {
            var isError = true;

            try
            {
                OnLoadingBegin(loadUnit);
                await loadUnit.Load(param);
                isError = false;
            }
            catch (Exception)
            {
                if (!skipExceptionThrow)
                {
                    throw;
                }
            }
            finally
            {
                await OnLoadingFinish(loadUnit, isError);
            }
        }
    }
}