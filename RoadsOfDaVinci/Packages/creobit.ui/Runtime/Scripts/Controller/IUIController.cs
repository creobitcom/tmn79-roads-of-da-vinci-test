using System;
using Creobit.Loading;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Creobit.UI
{
    public interface IUIController : ILoadUnit<GameObject>, IDisposable
    {
        /// <summary>
        /// Load panels when IUIController loaded.
        /// </summary>
        /// <param name="loadablePanelReferences">Array of panels to load</param>
        /// <param name="state">Initial state. Show panels if true, otherwise hide</param>
        /// <param name="parallel">Load panels concurrently when true. Default false keeps legacy sequential order.</param>
        public UniTask LoadPanels(LoadablePanelReference[] loadablePanelReferences, bool state = false,
            bool parallel = false);

        /// <summary>
        /// Load panels when IUIController loaded.
        /// </summary>
        /// <param name="preservedPanelReferences">Array of panels to preserve</param>
        /// <param name="parallel">Load panels concurrently when true. Default false keeps legacy sequential order.</param>
        public UniTask PreservePanels(PreservedPanelReference[] preservedPanelReferences, bool parallel = false);

        /// <summary>
        /// Cache panels to use.
        /// </summary>
        /// <param name="scenePanelReferences">Array of panels to cache</param>
        public void CachePanels(ScenePanelReference[] scenePanelReferences);

        /// <summary>
        /// Show panel (play all show animations).
        /// </summary>
        /// <param name="panelReference">Reference of panel to show</param>
        /// <param name="parent">Set this parent canvas if panel not loaded</param>
        public void ShowPanel(PanelReference panelReference, Transform parent);


        /// <summary>
        /// Show panel (play all show animations) and return the associated PanelData of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of PanelData to return, which must inherit from <see cref="PanelData"/>.</typeparam>
        /// <param name="panelReference">Reference of panel to show.</param>
        /// <param name="parent">Set this parent canvas if the panel is not loaded.</param>
        /// <returns>A <see cref="UniTask{T}"/> that represents the asynchronous operation and contains the resulting <typeparamref name="T"/> instance.</returns>
        public UniTask<T> ShowPanel<T>(PanelReference panelReference, Transform parent) where T : PanelData;

        /// <summary>
        /// Hide panel (play all hide animations).
        /// </summary>
        /// <param name="panelReference">Reference of panel to hide</param>
        /// <param name="parent">Set this parent canvas if panel not loaded</param>
        public void HidePanel(PanelReference panelReference, Transform parent);

        /// <summary>
        /// Get cached panel.
        /// </summary>
        /// <param name="panelReference">Reference of panel to get</param>
        /// <returns>Panel, if it was cached or is loading. Otherwise null.</returns>
        public UniTask<PanelData> GetPanel(PanelReference panelReference);

        /// <summary>
        /// Get cached panel animations.
        /// </summary>
        /// <param name="panelReference">Reference of panel to get animations</param>
        /// <param name="state">State of animations to get</param>
        /// <returns></returns>
        public DOTweenAnimation[] GetPanelAnimations(PanelReference panelReference, PanelState state);

        public void UnloadPanel(PanelReference panelReference);

        public void ClearCache();

    }
}