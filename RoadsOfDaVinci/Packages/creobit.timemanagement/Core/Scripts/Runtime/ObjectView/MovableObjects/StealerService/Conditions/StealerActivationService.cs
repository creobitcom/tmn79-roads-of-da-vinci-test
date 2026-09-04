using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;

public class StealerActivationService : MonoBehaviour
{
    public enum ConditionType
    {
        StaticObjectView,
        DisableObject,
    }
    [Serializable]
    public struct ConditionView
    {
        public ConditionType ConditionType;
        public GameObject Object;
    }
    
    [SerializeField] private List<AbstractCondition> _conditions = new List<AbstractCondition>();
    [SerializeField] private List<ConditionView> _conditionViews;
    public UltEvent OnStartRegster;
    public UltEvent OnAllConditionsEqual;
    private async void Start()
    {
        RegisterConditions();
        OnStartRegster?.Invoke();
        await WaitAllConditions();
    }

    private void RegisterConditions()
    {
        foreach (var conditionView in _conditionViews)
        {
            switch (conditionView.ConditionType)
            {
                case ConditionType.StaticObjectView:
                    var staticObjectCondition = new StaticObjectInteractionCondition(conditionView.Object.GetComponent<StaticObjectView>());
                    _conditions.Add(staticObjectCondition);
                    staticObjectCondition.Initialize();
                    break;
                case ConditionType.DisableObject:
                    var disableCondition = new DisableObjectCondition(conditionView.Object);
                    _conditions.Add(disableCondition);
                    disableCondition.Initialize();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private async UniTask WaitAllConditions()
    {
        await UniTask.WaitUntil(() => _conditions.Count(x => x.Evaluate()) == _conditions.Count);
        OnAllConditionsEqual?.Invoke();
    }
}