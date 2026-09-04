using System;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Loading;
using Cysharp.Threading.Tasks;

namespace Creobit.Fading
{
    /// <summary>
    /// Implements <see cref="ILoadingEventsListener"/> to trigger fade-in and fade-out effects
    /// </summary>
    public class FadeLoadingEventsListener : ILoadingEventsListener, IDisposable
    {   
        private readonly IFadeController _fadeController;
        private readonly ILoadingController _loadingController;

        public FadeLoadingEventsListener(IFadeController fadeController, ILoadingController loadingController)
        {
            _fadeController = fadeController;
            _loadingController = loadingController;
        }

        public void Init()
        {
          _loadingController.RegisterListener(this);
        }
        
        public void Dispose()
        {
            _loadingController.UnregisterListener(this);
        }

        public UniTask OnLoadScheduledSceneStarted()
        {
            return _fadeController.FadeIn();
        }
        
        public UniTask OnLoadingFinished()
        {
            _fadeController.FadeOut();
           return UniTask.CompletedTask;
        }
    }
}
