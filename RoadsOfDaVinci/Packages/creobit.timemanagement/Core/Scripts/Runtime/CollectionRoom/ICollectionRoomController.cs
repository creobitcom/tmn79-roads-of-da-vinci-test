using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using System.Collections.Generic;
using Creobit.Loading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom
{
    public interface ICollectionRoomController : ILoadUnit<CollectionRoomJson>, System.IDisposable
    {
        public List<CollectionRoomItemView> Items { get; }

        public void Initialize(List<CollectionRoomItemView> items);
        public void ShowTooltip(MetaTooltipData tooltipData, Vector3 position);
        public void HideTooltip();
    }
    
    [Serializable]
    public class CollectionRoomJson
    {
        public AssetReference TooltipView;
        public Canvas MetaCanvas;
        public Camera MainCamera;
        public CollectionRoomJson(AssetReference tooltipView, Canvas metaCanvas, Camera mainCamera)
        {
            TooltipView = tooltipView;
            MetaCanvas = metaCanvas;
            MainCamera = mainCamera;
        }
    }
}