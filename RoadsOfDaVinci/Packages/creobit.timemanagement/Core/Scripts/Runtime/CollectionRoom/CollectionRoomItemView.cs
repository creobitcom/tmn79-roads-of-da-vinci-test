using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom
{
    public class CollectionRoomItemView : MonoBehaviour, ICollectionRoomItemView
    {

        [SerializeField] private ObjectDataSO _objectData;
        [SerializeField] private MetaTooltipData _tooltipData;

        private ICollectionRoomController _collectionRoomController;

        public string Name => _objectData?.name;

        public bool IsOpened { get; private set; }

        public void Initialize(ICollectionRoomController collectionRoomController, bool isOpened)
        {
            _collectionRoomController = collectionRoomController;

            SetItemState(isOpened);
        }

        public void SetItemState(bool isOpened)
        {
            if(isOpened)
            {
                Open();
            } else
            {
                Close();
            }
        }

        private void Open()
        {
            IsOpened = true;
            gameObject.SetActive(true);
        }

        private void Close()
        {
            IsOpened = false;
            gameObject.SetActive(false);
        }

        public void OnHover()
        {
            _collectionRoomController.ShowTooltip(_tooltipData, transform.position);
        }

        public void OnHoverEnd()
        {
            _collectionRoomController.HideTooltip();
        }
    }
}
