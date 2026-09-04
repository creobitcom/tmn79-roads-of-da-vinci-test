using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;

public class TimerService : MonoBehaviour
{
    [Serializable]
    public class Timer
    {
        public string TimerName;
        public float Seconds;
        public UltEvent OnStart;
        public UltEvent OnEnd;
        
    }
    
    [SerializeField] private List<Timer> _timers = new List<Timer>();
    private CancellationTokenSource _cancellationTokenSource;

    public async void StartTimer(string timerName)
    {
        if (_cancellationTokenSource!=null)
        {
            _cancellationTokenSource.Cancel();
        }
        _cancellationTokenSource = new CancellationTokenSource();
        var timer = _timers.First(x=>x.TimerName==timerName);

        try
        {
            await TimerTask(timer, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Timer was restarted/destroyed — expected, ignore.
        }
    }

    private async UniTask TimerTask(Timer timer, CancellationToken token)
    {
        timer.OnStart?.Invoke();
        await UniTask.Delay(TimeSpan.FromSeconds(timer.Seconds), cancellationToken: token);
        if (token.IsCancellationRequested)
        {
            return;
        }
        timer.OnEnd?.Invoke();
    }

    private void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
    }
}