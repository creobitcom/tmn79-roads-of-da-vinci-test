using System;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.EventsInterceptors
{
    /// <summary>
    /// Intercept application events (quit, focus, pause)
    /// </summary>
    public class ApplicationEventsInterceptor : MonoBehaviour, ILoadUnit, IDisposable
    {
        public event Action Quitting;

        public event Action<bool> PauseStateChanged;
        public event Action<bool> FocusStateChanged;

        public UniTask Load()
        {
            Application.quitting += OnApplicationQuitting;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            Application.quitting -= OnApplicationQuitting;
        }

        private void OnApplicationQuitting()
        {
            Quitting?.Invoke();
        }

        private void OnApplicationPause(bool pause)
        {
            PauseStateChanged?.Invoke(pause);
        }

        private void OnApplicationFocus(bool focus)
        {
            FocusStateChanged?.Invoke(focus);
        }
    }
}