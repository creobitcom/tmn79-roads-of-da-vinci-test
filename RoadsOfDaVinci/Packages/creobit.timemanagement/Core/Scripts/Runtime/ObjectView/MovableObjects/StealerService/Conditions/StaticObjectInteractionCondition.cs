using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using UnityEngine;

[Serializable]
public class StaticObjectInteractionCondition : AbstractCondition
{
    [SerializeField] private StaticObjectView _staticObjectView;

    private bool _interacted = false;

    private void Interacted()
    {
        _interacted = true;
    }
    
    public override void Initialize()
    {
        _staticObjectView.onEndInteract += Interacted;
    }

    public override bool Evaluate()
    {
        return _interacted;
    }

    public override void Dispose()
    {
        _staticObjectView.onEndInteract -= Interacted;
    }

    public StaticObjectInteractionCondition(StaticObjectView staticObjectView)
    {
        _staticObjectView = staticObjectView;
    }
}