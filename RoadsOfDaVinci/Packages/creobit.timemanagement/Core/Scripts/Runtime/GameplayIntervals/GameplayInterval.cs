using System;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals
{
    public class GameplayInterval : IDisposable, IPausable
    {
        private readonly GameplayIntervalSpecificParameters _specificParameters;
        private readonly GameplayIntervalGeneralParameters _generalParameters;

        private int _pausesAmount;
        private int _currentLoopsAmount;
        private int _currentMilliseconds;
        private float _speed = 1f;
        private float _startTime;
        private int _currentTick;

        private float _lastTime;

        private bool _useCaptureTime;

        private CancellationTokenSource _cancellationTokenSource;

        private float CurrentTime => _useCaptureTime ? Time.time : Time.realtimeSinceStartup;

        // Истинно и при явной отмене (Cancel), и при естественном завершении конечного числа
        // повторов (CompleteInterval → _cancellationTokenSource.Cancel() перед Completed) —
        // используется GameplayIntervalsController, чтобы убирать завершённые интервалы из
        // _intervals самостоятельно, не дожидаясь явного CancelInterval от вызывающего кода.
        public bool IsFinished => _cancellationTokenSource != null && _cancellationTokenSource.IsCancellationRequested;

        public GameplayInterval(GameplayIntervalSpecificParameters specificParameters,
            GameplayIntervalGeneralParameters generalParameters)
        {
            _specificParameters = specificParameters;
            _generalParameters = generalParameters;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        public void Pause()
        {
            _pausesAmount += 1;
            _specificParameters.LoopPaused?.Invoke();
        }

        public void Unpause()
        {
            _pausesAmount -= 1;

            if (_pausesAmount <= 0)
            {
                _specificParameters.LoopUnpaused?.Invoke();
                _pausesAmount = 0;
                // 1000f: integer division dropped the sub-second part of the elapsed time,
                // extending the interval by up to 999 ms per pause/unpause cycle.
                _startTime = CurrentTime - _currentMilliseconds / 1000f;
            }
        }

        public void SetSpeed(float speed)
        {
            _speed = speed;
            _currentMilliseconds = (int)(1000 * (CurrentTime - _startTime));
            _lastTime = _currentMilliseconds;
        }

        public void StartInterval()
        {
            _useCaptureTime = Time.captureDeltaTime > 0f;
            _currentLoopsAmount = 0;
            _startTime = CurrentTime;

            _cancellationTokenSource = new CancellationTokenSource();

            _specificParameters.Started?.Invoke();

            StartLoop().Forget();
        }

        private async UniTaskVoid StartLoop()
        {
            await UniTask.WaitUntil(() => _pausesAmount <= 0);

            _currentMilliseconds = 0;
            _currentLoopsAmount += 1;
            _currentTick = 1;
            _lastTime = 0f;

            _startTime = CurrentTime;

            _specificParameters.LoopStarted?.Invoke();
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();

            _specificParameters.Canceled?.Invoke();
        }

        public void TickInterval(float delta)
        {
            if (_pausesAmount > 0 || _cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            _currentMilliseconds = (int)(1000 * (CurrentTime - _startTime));
            var deltaMs = _currentMilliseconds - _lastTime;
            if (_speed != 1f)
            {
                _currentMilliseconds += (int)((_speed - 1) * deltaMs);
            }

            if (_currentMilliseconds < _generalParameters.DurationMilliseconds)
            {
                if (_specificParameters.MovableObjectTaskView != null)
                {
                    var enableTaskView = _generalParameters.DurationSeconds > 0.01f;
                    _specificParameters.MovableObjectTaskView
                        .Report((float)_currentMilliseconds / _generalParameters.DurationMilliseconds, enableTaskView);
                }

                if (_specificParameters.ObjectView != null)
                {
                    _specificParameters.ObjectView.PlayInteractionSFX().Forget();
                }

                if (_currentMilliseconds / _generalParameters.TickIntervalMilliseconds >= _currentTick)
                {
                    _currentTick++;
                    _specificParameters.Ticked?.Invoke();
                }

                return;
            }

            CompleteInterval();
        }

        private void CompleteInterval()
        {
            if (_specificParameters.MovableObjectTaskView != null)
            {
                _specificParameters.MovableObjectTaskView.gameObject.SetActive(false);
            }

            _specificParameters.LoopCompleted?.Invoke();

            // A loop-completed handler may cancel the interval (production does this while
            // the created resource is waiting to be collected). Do not emit a ghost
            // LoopStarted callback for an interval that has already been canceled.
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (_generalParameters.IsInfiniteLoops || _currentLoopsAmount < _generalParameters.LoopAmounts)
            {
                StartLoop().Forget();
                return;
            }

            // Cancel BEFORE invoking Completed: if a handler throws, the interval must still
            // be terminated — otherwise TickInterval re-runs CompleteInterval (and the
            // throwing handler) every FixedUpdate forever.
            _cancellationTokenSource.Cancel();
            _specificParameters.Completed?.Invoke();
        }
    }
}
