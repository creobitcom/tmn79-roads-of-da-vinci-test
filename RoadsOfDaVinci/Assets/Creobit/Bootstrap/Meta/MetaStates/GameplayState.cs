using Creobit.Bootstrap.Core.Scripts.Runtime.Meta;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameplayState : IMetaState
{
    public void StartState(MetaController metaController)
    {
        metaController.LoadGameplay();
    }

    public bool ChangeState(MetaController metaController, IMetaState newState)
    {
        if (newState.GetType() == typeof(MenuState)
            || newState.GetType() == typeof(ComicsState)
            || newState.GetType() == typeof(VideoCutsceneState)
            || newState.GetType() == typeof(MapState))
        {
            metaController.HandleStateChange(this, newState).Forget();
            return true;
        }

        return false;
    }

    public UniTask EndState(MetaController metaController, bool async)
    {
        return UniTask.CompletedTask;
    }
}
