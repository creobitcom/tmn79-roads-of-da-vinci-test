using System.Collections.Generic;
using Creobit.Loading;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom
{
    public class CollectionRoomBridge : MonoBehaviour
    {
        [SerializeField] private List<CollectionRoomItemView> _items;

        private ICollectionRoomController _collectionRoomController;
        private ILoadingController _loadingController;

        [Inject]
        private void Construct(ICollectionRoomController collectionRoomController,
            ILoadingController loadingController)
        {
            _collectionRoomController = collectionRoomController;
            _loadingController = loadingController;
        }

        private void Start()
        {
            _loadingController.LoadingFinished += OnLoaded;
        }

        private void OnLoaded()
        {
            Initialize(_items);
        }

        public void Initialize(List<CollectionRoomItemView> items)
        {
            _collectionRoomController.Initialize(items);
        }

        private void OnDestroy()
        {
            _loadingController.LoadingFinished -= OnLoaded;
        }
    }
}