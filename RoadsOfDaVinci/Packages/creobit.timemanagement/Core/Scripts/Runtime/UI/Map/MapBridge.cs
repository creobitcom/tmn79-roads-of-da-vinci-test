using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map
{
    public class MapBridge : MonoBehaviour
    {
        private IMapController _mapController;
        
        [SerializeField] private UltEvent _onMapOpened;
        [SerializeField] private UltEvent _onMapClosed;
        [SerializeField] private UltEvent _onSpotSelected;
        [SerializeField] private UltEvent _onPageChanged;

        [Inject]
        public void Construct(IMapController mapController)
        {
            _mapController = mapController;
        }

        private void Start()
        {
            if(_mapController != null)
            {
                _mapController.OnMapOpened += _onMapOpened.InvokeSafe;
                _mapController.OnMapClosed += _onMapClosed.InvokeSafe;
                _mapController.OnSpotSelected += _onSpotSelected.InvokeSafe;
                _mapController.OnPageChanged += _onPageChanged.InvokeSafe;
            }
        }

        private void OnDestroy()
        {
            if(_mapController != null)
            {
                _mapController.OnMapOpened -= _onMapOpened.InvokeSafe;
                _mapController.OnMapClosed -= _onMapClosed.InvokeSafe;
                _mapController.OnSpotSelected -= _onSpotSelected.InvokeSafe;
                _mapController.OnPageChanged -= _onPageChanged.InvokeSafe;
            }
        }

        public void ShowMap()
        {
            _mapController.ShowMap();
        }

        public void HideMap()
        {
            _mapController.HideMap();
        }

        public void SelectMapSpot(MapSpotView spot)
        {
            _mapController.SelectMapSpot(spot);
        }

        public MapSpotView GetSelectedMapSpot() => _mapController.GetSelectedMapSpot();
    }
}