using Cysharp.Threading.Tasks;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public class CollectionRoomState : IMetaState
    {
        public void StartState(MetaController metaController)
        {
            var sceneRefs = metaController.SceneReferences;

            metaController.ShowPanel(sceneRefs.CollectionRoomBottomPanel, sceneRefs.MetaCanvas.transform);

            sceneRefs.CollectionRoom.SetActive(true);
        }

        public bool ChangeState(MetaController metaController, IMetaState newState)
        {
            if (newState.GetType() == typeof(MapState) || newState.GetType() == typeof(MenuState))
            {
                metaController.HandleStateChange(this, newState).Forget();
                return true;
            }
            
            return false;
        }

        public UniTask EndState(MetaController metaController, bool async)
        {
            var sceneRefs = metaController.SceneReferences;

            metaController.HidePanel(sceneRefs.CollectionRoomBottomPanel, sceneRefs.MetaCanvas.transform);

            sceneRefs.CollectionRoom.SetActive(false);

            return UniTask.CompletedTask;
        }
    }
}