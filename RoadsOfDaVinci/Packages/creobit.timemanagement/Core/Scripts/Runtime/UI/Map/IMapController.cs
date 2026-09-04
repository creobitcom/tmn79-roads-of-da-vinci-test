using System;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map
{
    public interface IMapController : ILoadUnit<MapJson>, IDisposable
    {
        /// <summary>
        /// Get current selected level.
        /// </summary>
        /// <returns>Current selected level. If selected null returns -1</returns>
        public int CurrentLevel { get; }

        public event Action OnMapOpened;
        public event Action OnMapClosed;
        public event Action OnSpotSelected;
        public event Action OnPageChanged;
        public event Action OnBackRequested;

        /// <summary>
        /// Show map: load and activate map view and page, set level from save.
        /// </summary>
        public void ShowMap();
        
        /// <summary>
        /// Hide all map scene objects.
        /// </summary>
        public void HideMap();
        
        /// <summary>
        /// Select map spot and save it spot as current.
        /// </summary>
        /// <param name="spot">Spot to select.</param>
        public void SelectMapSpot(MapSpotView spot);
        

        public void TryShowMapChangeButton();

        public MapSpotView GetMapSpot(int level);
        public MapSpotView GetSelectedMapSpot();
        public int GetCurrentLevel();
    }
}