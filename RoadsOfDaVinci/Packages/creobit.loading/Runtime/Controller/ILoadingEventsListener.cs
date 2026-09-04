using Cysharp.Threading.Tasks;

namespace Creobit.Loading
{
    /// <summary>
    /// Listener of loading events.
    /// </summary>
    public interface ILoadingEventsListener
    {
        /// <summary>
        /// Called when the loading of a scheduled scene has started.
        /// </summary>
        public UniTask OnLoadScheduledSceneStarted();
        
        /// <summary>
        /// Called when the loading process has finished.
        /// </summary>
        public UniTask OnLoadingFinished();
    }
}
