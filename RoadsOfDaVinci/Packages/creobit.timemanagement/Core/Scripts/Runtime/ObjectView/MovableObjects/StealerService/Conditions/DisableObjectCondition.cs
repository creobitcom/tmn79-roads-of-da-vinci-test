using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
public class DisableObjectCondition : AbstractCondition
{
    [SerializeField] private GameObject _object;

    private bool _disabled = false;
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    
    public override async void Initialize()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        await WaitDisableTask(_cancellationTokenSource.Token);
    }

    private async UniTask WaitDisableTask(CancellationToken token)
    {
        await UniTask.WaitUntil(() => !_object.activeSelf || _cancellationTokenSource.IsCancellationRequested, cancellationToken: token);
        _disabled = true;
    }

    public override bool Evaluate()
    {
        return _disabled;
    }

    public override void Dispose()
    {
        _cancellationTokenSource.Cancel();
    }

    public DisableObjectCondition(GameObject @object)
    {
        _object = @object;
    }
}