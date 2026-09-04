using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using Creobit.Bootstrap.Core.Scripts.Runtime.Meta;
using Cysharp.Threading.Tasks;
using R3;

public class GuidesState : IMetaState
{
    private IDisposable _stateSubscription;

    public void StartState(MetaController metaController)
    {
        var guidesController = metaController.GuidesController;

        if (guidesController == null)
        {
            return;
        }

        guidesController.ChangeState(GuidesStates.MainPage);

        _stateSubscription?.Dispose();
        _stateSubscription = guidesController.State
            .Subscribe(state => HandleGuidesExit(metaController, state));
    }

    public bool ChangeState(MetaController metaController, IMetaState newState)
    {
        if (newState.GetType() == typeof(MenuState))
        {
            metaController.HandleStateChange(this, newState, async: false).Forget();
            return true;
        }

        return false;
    }

    private void HandleGuidesExit(MetaController metaController, GuidesStates state)
    {
        if (state == GuidesStates.None)
        {
            metaController.ChangeState(MetaStates.Menu);
        }
    }

    public UniTask EndState(MetaController metaController, bool async)
    {
        _stateSubscription?.Dispose();
        _stateSubscription = null;

        metaController.GuidesController?.ChangeState(GuidesStates.None);

        return UniTask.CompletedTask;
    }
}
