using Cysharp.Threading.Tasks;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    /// <summary>
    /// Оверлей видео-катсцены. Зеркалит ComicsState: блокирует мету на время ролика,
    /// после завершения управление возвращает MetaController.
    /// </summary>
    public class VideoCutsceneState : IMetaState
    {
        public void StartState(MetaController metaController)
        {
            metaController.LockState = true;

            metaController.PlayPendingVideoCutscene();
        }

        public bool ChangeState(MetaController metaController, IMetaState newState)
        {
            if (newState.GetType() == typeof(MapState)
                || newState.GetType() == typeof(MenuState)
                || newState.GetType() == typeof(GameplayState)
                || newState.GetType() == typeof(ComicsState))
            {
                metaController.HandleStateChange(this, newState).Forget();
                return true;
            }

            return false;
        }

        public UniTask EndState(MetaController metaController, bool async)
        {
            metaController.LockState = false;

            // Если после ролика показывали комикс — гасим его корень здесь,
            // потому что ComicsState.EndState в этом случае не отрабатывает.
            metaController.SceneReferences.ComicsRoot.gameObject.SetActive(false);

            return UniTask.CompletedTask;
        }
    }
}
