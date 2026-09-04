using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using VContainer;
using UltEvents;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    public class SaveBridge : MonoBehaviour
    {
        private ISaveController _saveController;

        [SerializeField] private UltEvent _onSaveCollectionItem;

        [Inject]
        private void Construct(ISaveController saveController)
        {
            _saveController = saveController;
        }

        public void SaveCollectionItem(ObjectView objectView)
        {
            _saveController.Service.SaveCollectionItem(objectView.ObjectDataSO.name);
            _onSaveCollectionItem?.Invoke();
        }
    }
}

