using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems
{
    public class GameplayIntervalsController : MonoBehaviour, IGameplayIntervalsController
    {
        private ushort _lastIntervalId;

        private IDisposable _pauseStateDisposable;

        private readonly Dictionary<ushort, GameplayInterval> _intervals = new();

        private IPauseController _pauseController;
        private IReloadController _reloadController;

        [Inject]
        public void Construct(IPauseController pauseController,
            IReloadController reloadController)
        {
            _pauseController = pauseController;
            _reloadController = reloadController;
        }

        public UniTask Load()
        {
            _pauseStateDisposable = _pauseController.IsPaused
                .Skip(1)
                .Subscribe(_ => OnPausedStateChanged(_pauseController.IsPaused.CurrentValue));

            _reloadController.AddReloadableObject(this);

            return UniTask.CompletedTask;
        }

        private void FixedUpdate()
        {
            // Снимок вместе с ключами: тик может синхронно вызвать обратно StartInterval/
            // CancelInterval (например Production перезапускает интервал в LoopCompleted) —
            // мутировать живой _intervals прямо во время обхода нельзя.
            var snapshot = _intervals.ToList();

            for (var i = snapshot.Count - 1; i >= 0; i--)
            {
                snapshot[i].Value.TickInterval(Time.fixedDeltaTime);
            }

            // Интервалы, завершившиеся САМИ (конечное число повторов, без явного CancelInterval
            // от владельца) раньше никогда не убирались из _intervals — там же навсегда занят
            // и их id. Отдельным проходом после тика подчищаем такие: только если по этому ключу
            // в живом словаре всё ещё лежит именно этот объект (а не новый, успевший занять
            // тот же id за время тика — на практике не бывает, но проверка дешёвая и безопасная).
            foreach (var kvp in snapshot)
            {
                if (kvp.Value.IsFinished
                    && _intervals.TryGetValue(kvp.Key, out var current)
                    && current == kvp.Value)
                {
                    _intervals.Remove(kvp.Key);
                }
            }
        }

        public UniTask Reload()
        {
            ClearIntervals();

            // Счётчик общий на всю сессию (Production/LevelTimer/Craft/COC-апгрейды и т.д.),
            // ushort рано или поздно оборачивается при достаточно долгой сессии с частыми
            // рестартами. _intervals только что очищен, так что сброс здесь безопасен — не
            // может столкнуться со старым ключом, который кто-то ещё держит.
            _lastIntervalId = 0;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _pauseStateDisposable?.Dispose();

            _reloadController.RemoveReloadableObject(this);

            ClearIntervals();
        }

        public ushort StartInterval(GameplayIntervalSpecificParameters specificParameters, GameplayIntervalGeneralParameters generalParameters)
        {
            var interval = new GameplayInterval(specificParameters, generalParameters);

            // StartInterval() уже поднимает Started/LoopStarted (видимая анимация) независимо
            // от того, удастся ли ниже добавить интервал в _intervals — поэтому свободный ключ
            // подбираем ЗАРАНЕЕ, чтобы Add ниже не мог кинуть исключение и "потерять" уже
            // стартовавший (визуально) интервал, который после этого никогда не тикался бы.
            do
            {
                _lastIntervalId += 1;
            } while (_intervals.ContainsKey(_lastIntervalId));

            interval.StartInterval();

            _intervals.Add(_lastIntervalId, interval);

            return _lastIntervalId;
        }

        public void CancelInterval(ushort intervalId)
        {
            if (_intervals.TryGetValue(intervalId, out var interval))
            {
                interval.Cancel();

                _intervals.Remove(intervalId);
            }
        }

        public void SetIntervalSpeed(ushort intervalId, float speed)
        {
            if (_intervals.TryGetValue(intervalId, out var interval))
            {
                interval.SetSpeed(speed);
            }
        }

        public void PauseInterval(ushort intervalId)
        {
            if (_intervals.TryGetValue(intervalId, out GameplayInterval interval))
            {
                interval.Pause();
            }
        }

        public void UnPauseInterval(ushort intervalId)
        {
            if (_intervals.TryGetValue(intervalId, out GameplayInterval interval))
            {
                interval.Unpause();
            }
        }

        private void ClearIntervals()
        {
            foreach (var intervals in _intervals.Values)
            {
                intervals.Dispose();
            }

            _intervals.Clear();
        }

        private void OnPausedStateChanged(bool paused)
        {
            foreach (var intervalId in _intervals.Keys)
            {
                var interval = _intervals[intervalId];

                if (paused)
                {
                    interval.Pause();
                }
                else
                {
                    interval.Unpause();
                }
            }
        }
    }
}

