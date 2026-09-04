using UltEvents;
using UnityEngine;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public class MetaBridge : MonoBehaviour
    {

        [SerializeField] private UltEvent _onStateChanged;

        private IMetaController _metaController;

        [Inject]
        private void Construct(IObjectResolver objectResolver)
        {
            objectResolver.TryResolve(out _metaController);
        }

        private void Start()
        {
            if (_metaController != null)
            {
                _metaController.OnStateChanged += _onStateChanged.InvokeSafe;
            }
        }

        public void LoadGameplay()
        {
            _metaController?.ChangeState(MetaStates.Gameplay);
        }
        
        public void OpenMenu()
        {
            _metaController?.ChangeState(MetaStates.Menu);
        }

        public void OpenMap()
        {
            _metaController?.ChangeState(MetaStates.Map);
        }

        public void OpenCutscene()
        {
            _metaController?.ChangeState(MetaStates.Comics);
        }

        public void OpenCollectionRoom()
        {
            _metaController?.ChangeState(MetaStates.CollectionRoom);
        }

        public void OpenGuides()
        {
            _metaController?.ChangeState(MetaStates.Guides);
        }

        private void OnDestroy()
        {
            if(_metaController != null)
            {
                _metaController.OnStateChanged -= _onStateChanged.InvokeSafe;
            }
        }

    }
}
