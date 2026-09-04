using System;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.AddressableAssets;
using UnityEngine;
using VContainer;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip;
using VContainer.Unity;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using Creobit.AddressablesController;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Localization;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom
{
    public class CollectionRoomController : ICollectionRoomController
    {
        private CollectionRoomJson _collectionRoomJson;
        private IAddressablesController _addressablesController;
        private IObjectResolver _objectResolver;
        private ISaveController _saveController;
        private IPlayerProfilesController _playerProfilesController;

        private CollectionRoomTooltipView _tooltipView;

        public List<CollectionRoomItemView> Items { get; private set; }

        [Inject]
        private void Construct(
            IAddressablesController addressablesController,
            IObjectResolver objectResolver,
            ISaveController saveController,
            IPlayerProfilesController playerProfilesController)
        {
            _addressablesController = addressablesController;
            _objectResolver = objectResolver;
            _saveController = saveController;
            _playerProfilesController = playerProfilesController;
        }


        
        public async UniTask Load(CollectionRoomJson collectionRoomJson)
        {
            _collectionRoomJson = collectionRoomJson;
            _tooltipView = await LoadFromReference<CollectionRoomTooltipView>(_collectionRoomJson.TooltipView);
            _tooltipView = SpawnTooltip<CollectionRoomTooltipView>(_tooltipView);

            _playerProfilesController.Service.SelectedProfile += OnProfileSelected;
        }

        private async UniTask<T> LoadFromReference<T>(AssetReference reference)
            where T : Object
        {
            GameObject handle = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(reference);

            return handle.GetComponent<T>();
        }

        private T SpawnTooltip<T>(ITooltipView tooltipView) where T : ITooltipView
        {
            var instance = (ITooltipView)_objectResolver.
                Instantiate((Component)tooltipView, _collectionRoomJson.MetaCanvas.transform);

            instance.CurrentGameObject.SetActive(false);

            return (T)instance;
        }

        public void Initialize(List<CollectionRoomItemView> items)
        {
            Items = items;

            foreach (var item in Items)
            {
                bool isOpened = _saveController.Service.IsCollectionItemSaved(item.Name);
                item.Initialize(this, isOpened);
            }
        }

        public void ShowTooltip(MetaTooltipData tooltipData, Vector3 position)
        {
            SetLocalizedData(_tooltipView, tooltipData);
            SetTooltipRect(_tooltipView, tooltipData.Offset, position);
        }

        private void SetLocalizedData(ITooltipView tooltipView, MetaTooltipData currentData)
        {
            tooltipView.SetTooltipText(
                LocalizationService.Instance.GetText(currentData.Name),
                LocalizationService.Instance.GetText(currentData.Description));
        }

        private void SetTooltipRect(ITooltipView tooltipView, Vector2 offset, Vector3 position)
        {
            var tooltipRectTransform = (RectTransform)tooltipView.CurrentGameObject.transform;
            var tooltipRect = tooltipRectTransform.rect;

            tooltipRectTransform.localPosition = UIHelper.ConvertWorldToLocalCanvasPosition(position,
                _collectionRoomJson.MainCamera, _collectionRoomJson.MetaCanvas, Vector2.up,
                offset, tooltipRect.width, tooltipRect.height);

            tooltipView.ForceUpdate();

            tooltipView.CurrentGameObject.SetActive(true);
        }

        private void OnProfileSelected(PlayerProfileData profileData)
        {
            if (Items == null) return;
            
            foreach (var item in Items)
            {
                bool isOpened = _saveController.Service.IsCollectionItemSaved(item.Name);
                item.SetItemState(isOpened);
            }
        }

        public void HideTooltip()
        {
            _tooltipView.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            _playerProfilesController.Service.SelectedProfile -= OnProfileSelected;
        }
    }
}