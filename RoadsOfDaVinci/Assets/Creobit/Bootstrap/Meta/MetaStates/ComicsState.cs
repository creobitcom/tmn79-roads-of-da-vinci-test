using _8floor.TimeManagement.Core.Scripts.Runtime.Comics;
using Cysharp.Threading.Tasks;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public class ComicsState : IMetaState
    {
        public void StartState(MetaController metaController)
        {
            metaController.LockState = true;

            var sceneRefs = metaController.SceneReferences;

            sceneRefs.ComicsRoot.gameObject.SetActive(true);

            sceneRefs.CameraTrackPoint.transform.position = sceneRefs.ComicsTrackPoint.position;
            sceneRefs.PositionComposer.Composition.ScreenPosition = sceneRefs.ComicsTrackOffset;

            if (metaController.PendingComics != null)
            {
                var comics = metaController.PendingComics;
                metaController.ClearPendingComics();
                metaController.ShowComics(comics);
            }
            else if (metaController.IsComicsReady(out var comicsData))
            {
                metaController.ShowComics(comicsData);
            }
        }

        public bool ChangeState(MetaController metaController, IMetaState newState)
        {
            if (newState.GetType() == typeof(MapState) 
                || newState.GetType() == typeof(MenuState) 
                || newState.GetType() == typeof(GameplayState))
            {
                metaController.HandleStateChange(this, newState).Forget();
                return true;
            }

            return false;
        }

        public UniTask EndState(MetaController metaController, bool async)
        {
            metaController.LockState = false;

            var sceneRefs = metaController.SceneReferences;

            sceneRefs.ComicsRoot.gameObject.SetActive(false);

            return UniTask.CompletedTask;
        }
    }
}
