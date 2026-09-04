using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;

// Имя ObjectView занято одноимённым НЕЙМСПЕЙСОМ внутри ...Runtime, поэтому тип класса
// адресуем через алиас — иначе компилятор разрешает ObjectView в неймспейс.
using ObjectViewBase = _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.ObjectView;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.RelocatingObstacle
{
    /// <summary>
    /// «Препятствие, которое переезжает» (GG6: кочка; в оригинале MovableObstacle).
    ///
    /// Цикл: игрок расчищает препятствие → оно прячется и освобождает дорогу → выдержка
    /// (таймер) → ждём, пока все юниты простаивают → вырастает в случайной свободной точке
    /// из списка и снова закрывает дорогу. Повторяется бесконечно.
    ///
    /// Ожидаемая структура префаба — компонент лежит на КОРНЕ, который сам по себе не ObjectView
    /// (вьюшка после финального взаимодействия гасит себя, и включить её изнутри уже не выйдет):
    ///
    ///   Root                    ← этот компонент + TimerService, остаётся активным всегда
    ///   ├── Obstacle            ← RelocatableObstacleView: BlocksPath, CanInteract,
    ///   │                         InteractionsAmount = 1, DeactivateAfterFinalInteraction = true
    ///   └── Spots               ← пустышки в позициях точек-кандидатов
    ///       ├── Spot0 … SpotN
    ///
    /// Двигается ВЬЮШКА, а не корень: корень держит точки, поэтому переезжать ему нельзя —
    /// иначе точки уехали бы вместе с ним.
    ///
    /// Выдержку задаёт <see cref="TimerService"/> на том же корне (штатный компонент движка,
    /// им же пользуются враги). Мы стартуем таймер из кода, а его OnEnd должен вызывать
    /// <see cref="OnRespawnDelayElapsed"/> — эту связь расставляет тулса портирования.
    /// Если связь не расставлена, сработает сторож и напишет ошибку, а цикл продолжится,
    /// чтобы уровень не остался без механики.
    /// </summary>
    [DisallowMultipleComponent]
    public class RelocatingObstacleService : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField, Tooltip("Переезжающее препятствие. Пусто — ищется в детях.")]
        private RelocatableObstacleView _obstacleView;

        [SerializeField, Tooltip("Точки-кандидаты. Пусто — берутся все дети контейнера Spots.")]
        private List<Transform> _spots = new List<Transform>();

        [SerializeField, Tooltip("Контейнер точек. Используется, когда список выше пуст.")]
        private Transform _spotsRoot;

        [SerializeField, Tooltip("Штатный таймер движка на этом же корне. Пусто — ищется на объекте.")]
        private TimerService _timerService;

        [SerializeField, Tooltip("Имя таймера выдержки в TimerService.")]
        private string _respawnTimerName = "Respawn";

        [Header("Rules")]
        [SerializeField, Tooltip("Ждать, пока все юниты простаивают. Как в оригинале: выдержка — " +
                                 "это минимум, а вылезает препятствие в первый спокойный момент.")]
        private bool _requireUnitsIdle = true;

        [SerializeField, Min(0f), Tooltip("Предохранитель на ожидание простоя, сек. Если юнит залипнет " +
                                          "в работе, без него препятствие не вернётся до конца уровня. " +
                                          "0 — ждать бесконечно, как в оригинале.")]
        private float _idleGateTimeout = 15f;

        [SerializeField, Min(0f), Tooltip("Радиус, в котором точка считается занятой другим объектом.")]
        private float _spotOccupiedRadius = 0.35f;

        [SerializeField, Min(0.1f), Tooltip("Как часто перепроверять, освободилась ли точка, сек.")]
        private float _spotRetryInterval = 1f;

        [SerializeField, Min(0f), Tooltip("Сторож разводки: сколько ждать вызова OnRespawnDelayElapsed " +
                                          "от таймера, прежде чем ругнуться и продолжить самим, сек.")]
        private float _timerWiringWatchdog = 30f;

        [Header("Анимация")]
        [SerializeField, Tooltip("Аниматор визуала. Пусто — ищется в детях препятствия.")]
        private Animator _animator;

        [SerializeField, Tooltip("Клип покоя. Оригинал показывает препятствие сразу им, без прорастания.")]
        private string _idleAnimation = "zuzuIdle";

        [SerializeField, Tooltip("Клип ухода под землю после расчистки. Пусто — уходит мгновенно.")]
        private string _hideAnimation = "zuzuHide";

        [SerializeField, Min(0f), Tooltip("Длительность ухода, сек. 0 — взять длину клипа из аниматора.")]
        private float _hideAnimationDuration;

        [Header("Events")]
        [SerializeField, Tooltip("Препятствие расчищено, спряталось и освободило дорогу.")]
        private UltEvent _onHidden;

        [SerializeField, Tooltip("Препятствие выросло в новой точке и снова закрыло дорогу.")]
        private UltEvent _onRelocated;

        private bool _subscribed;
        private bool _cycleRunning;
        private bool _awaitingTimer;
        private bool _noSpotsReported;
        private bool _hiding;
        private Collider _collider;

        private void Reset()
        {
            _obstacleView = GetComponentInChildren<RelocatableObstacleView>(true);
            _timerService = GetComponent<TimerService>();
            _spotsRoot = transform.Find("Spots");
        }

        private void Awake()
        {
            if (_obstacleView == null)
            {
                _obstacleView = GetComponentInChildren<RelocatableObstacleView>(true);
            }

            if (_timerService == null)
            {
                _timerService = GetComponent<TimerService>();
            }

            if (_obstacleView == null)
            {
                Debug.LogError($"[RelocatingObstacle] {name}: не задан RelocatableObstacleView.", this);
                enabled = false;
                return;
            }

            if (_obstacleView.transform == transform)
            {
                Debug.LogError($"[RelocatingObstacle] {name}: вьюшка лежит на самом корне. Она гасит себя " +
                               "после расчистки, и включить её будет некому — вынеси её в дочерний объект.", this);
            }

            CollectSpotsIfNeeded();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed || _obstacleView == null)
            {
                return;
            }

            _obstacleView.OnEndInteract += HandleEndInteract;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _obstacleView == null)
            {
                return;
            }

            _obstacleView.OnEndInteract -= HandleEndInteract;
            _subscribed = false;
        }

        private void CollectSpotsIfNeeded()
        {
            if (_spots != null && _spots.Count > 0)
            {
                return;
            }

            _spots = new List<Transform>();

            if (_spotsRoot == null)
            {
                _spotsRoot = transform.Find("Spots");
            }

            if (_spotsRoot == null)
            {
                return;
            }

            foreach (Transform spot in _spotsRoot)
            {
                _spots.Add(spot);
            }
        }

        /// <summary>
        /// Движок зовёт OnEndInteract уже ПОСЛЕ ApplyFinalInteractionEffects, а тот на финальном
        /// взаимодействии снимает CanInteract. По нему и определяем, что препятствие расчищено —
        /// признак работает и когда объект гасится движком, и когда остаётся включённым.
        /// </summary>
        private void HandleEndInteract(ObjectViewBase view)
        {
            if (_cycleRunning || view == null || view.CanInteract)
            {
                return;
            }

            StartCycle();
        }

        private void StartCycle()
        {
            _cycleRunning = true;

            _obstacleView.ReleaseGraphNode();
            PlayHide().Forget();
            _onHidden?.Invoke();

            if (_spots == null || _spots.Count == 0)
            {
                // Битые данные уровня (в GG6 такие есть): список точек пуст или не разрешился.
                // Оригинал в этом месте падал с NullReference и препятствие исчезало навсегда.
                // Воспроизводим наблюдаемое поведение, но без исключения.
                if (!_noSpotsReported)
                {
                    _noSpotsReported = true;
                    Debug.LogWarning($"[RelocatingObstacle] {name}: список точек пуст — препятствие больше " +
                                     "не появится. Так же ведёт себя оригинал на уровнях с битым payload.", this);
                }

                return;
            }

            if (_timerService != null)
            {
                _awaitingTimer = true;
                _timerService.StartTimer(_respawnTimerName);
                WatchTimerWiring().Forget();
            }
            else
            {
                Debug.LogWarning($"[RelocatingObstacle] {name}: нет TimerService — выдержки не будет.", this);
                WaitForCalmAndRelocate().Forget();
            }
        }

        /// <summary>
        /// Вызывается из TimerService.OnEnd (связь расставляет тулса портирования).
        /// Флаг _awaitingTimer гарантирует, что цикл не стартует дважды, если сторож уже
        /// продолжил за неразведённый таймер.
        /// </summary>
        public void OnRespawnDelayElapsed()
        {
            if (!_cycleRunning || !_awaitingTimer)
            {
                return;
            }

            _awaitingTimer = false;
            WaitForCalmAndRelocate().Forget();
        }

        /// <summary>
        /// Сторож разводки: если OnEnd таймера ни на что не завязан, механика молча умерла бы.
        /// Ругаемся и продолжаем сами, чтобы уровень остался играбельным.
        /// </summary>
        private async UniTaskVoid WatchTimerWiring()
        {
            var token = this.GetCancellationTokenOnDestroy();
            var deadline = _timerWiringWatchdog;

            if (deadline <= 0f)
            {
                return;
            }

            var elapsed = 0f;
            while (_awaitingTimer && elapsed < deadline)
            {
                elapsed += Time.deltaTime;
                var cancelled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (cancelled)
                {
                    return;
                }
            }

            if (!_awaitingTimer)
            {
                return;
            }

            Debug.LogError($"[RelocatingObstacle] {name}: за {deadline:0} сек от таймера '{_respawnTimerName}' " +
                           "не пришёл вызов OnRespawnDelayElapsed. Проверь, что OnEnd таймера завязан на этот " +
                           "компонент. Продолжаю цикл сам.", this);

            _awaitingTimer = false;
            WaitForCalmAndRelocate().Forget();
        }

        private async UniTaskVoid WaitForCalmAndRelocate()
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (_requireUnitsIdle && await WaitUntilUnitsIdle(token))
            {
                return;
            }

            var spot = await PickFreeSpot(token);
            if (spot == null)
            {
                return;
            }

            // Уход под землю мог ещё не доиграть (штатно выдержка длиннее клипа, но её могли
            // и уменьшить) — показывать препятствие поверх недоигранной анимации нельзя.
            var cancelled = await UniTask.WaitWhile(() => _hiding, cancellationToken: token)
                .SuppressCancellationThrow();
            if (cancelled)
            {
                return;
            }

            _obstacleView.transform.position = spot.position;
            _obstacleView.Rearm();
            SetColliderEnabled(true);

            // Оригинал на появлении играет именно клип ПОКОЯ: препятствие возникает сразу,
            // анимации прорастания в цикле нет (MovableObstacle.SHOW_ANIMATION_NAME = "zuzuIdle").
            // Без этого вызова аниматор после SetActive(true) стартует со своего дефолтного
            // состояния — а это как раз прорастание, и появление затягивается на его длину.
            PlayAnimation(_idleAnimation);

            _cycleRunning = false;
            _onRelocated?.Invoke();
        }

        /// <summary>
        /// Уход под землю. Объект при этом НЕ гасится — как в оригинале, где
        /// MovableObstacle.SetActive(false) лишь выключал коллайдер и запускал клип. Невидимым
        /// препятствие делает сама анимация: zuzuHide уводит тело в масштаб 0.
        ///
        /// Гасить нельзя по двум причинам, обе всплыли на практике:
        ///   • состояния аниматора идут с Write Defaults, и при каждом включении объекта аниматор
        ///     заново запоминает текущую позу как дефолтную. После zuzuHide это масштаб 0, а клип
        ///     покоя масштаб не анимирует — препятствие возвращалось невидимым;
        ///   • у эффекта копания стоит Play On Awake, и включение объекта каждый раз заново
        ///     выстреливало партиклы.
        ///
        /// Чтобы объект не гасился, у data-SO выключен DeactivateAfterFinalInteraction. Ноду графа
        /// движок в этом режиме не освобождает — это делает ReleaseGraphNode выше по циклу.
        /// </summary>
        private async UniTaskVoid PlayHide()
        {
            if (string.IsNullOrEmpty(_hideAnimation))
            {
                return;
            }

            _hiding = true;

            try
            {
                SetColliderEnabled(false);

                if (!PlayAnimation(_hideAnimation))
                {
                    return;
                }

                var duration = _hideAnimationDuration > 0f
                    ? _hideAnimationDuration
                    : GetClipLength(_hideAnimation);

                if (duration > 0f)
                {
                    await UniTask.Delay(
                            System.TimeSpan.FromSeconds(duration),
                            cancellationToken: this.GetCancellationTokenOnDestroy())
                        .SuppressCancellationThrow();
                }
            }
            finally
            {
                _hiding = false;
            }
        }

        private bool PlayAnimation(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            var animator = ResolveAnimator();
            if (animator == null)
            {
                return false;
            }

            animator.Play(stateName, 0, 0f);
            return true;
        }

        private Animator ResolveAnimator()
        {
            if (_animator == null && _obstacleView != null)
            {
                _animator = _obstacleView.GetComponentInChildren<Animator>(true);
            }

            return _animator;
        }

        private float GetClipLength(string clipName)
        {
            var controller = ResolveAnimator()?.runtimeAnimatorController;
            if (controller == null)
            {
                return 0f;
            }

            foreach (var clip in controller.animationClips)
            {
                if (clip != null && clip.name == clipName)
                {
                    return clip.length;
                }
            }

            return 0f;
        }

        private void SetColliderEnabled(bool value)
        {
            if (_collider == null && _obstacleView != null)
            {
                _collider = _obstacleView.GetComponent<Collider>();
            }

            if (_collider != null)
            {
                _collider.enabled = value;
            }
        }

        /// <summary>
        /// Гейт «все гномы простаивают» — аналог условия оригинала
        /// (actionsInProgress.Count == 0 &amp;&amp; actionsQueue.Count == 0).
        /// Возвращает true, если ожидание отменено уничтожением объекта.
        /// </summary>
        private async UniTask<bool> WaitUntilUnitsIdle(System.Threading.CancellationToken token)
        {
            var elapsed = 0f;

            while (!AreAllUnitsIdle())
            {
                if (_idleGateTimeout > 0f && elapsed >= _idleGateTimeout)
                {
                    Debug.LogWarning($"[RelocatingObstacle] {name}: юниты не освободились за " +
                                     $"{_idleGateTimeout:0} сек — выхожу по предохранителю. " +
                                     "Похоже, кто-то залип в работе.", this);
                    return false;
                }

                elapsed += Time.deltaTime;

                var cancelled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (cancelled)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreAllUnitsIdle()
        {
            var units = _obstacleView.Controller?.GetMovableObjectController()?.GetUnits();
            if (units == null)
            {
                return true;
            }

            foreach (var unit in units)
            {
                if (unit != null && unit.State.Value != UnitState.Idle)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Случайная свободная точка. Если свободных нет — ждём, как решили: препятствие
        /// просто не вылезает, пока место не освободится.
        /// </summary>
        private async UniTask<Transform> PickFreeSpot(System.Threading.CancellationToken token)
        {
            var free = new List<Transform>();
            var attempts = 0;

            while (true)
            {
                free.Clear();

                foreach (var spot in _spots)
                {
                    if (spot != null && !IsSpotOccupied(spot.position))
                    {
                        free.Add(spot);
                    }
                }

                if (free.Count > 0)
                {
                    return free[Random.Range(0, free.Count)];
                }

                attempts++;
                if (attempts == 10)
                {
                    Debug.LogWarning($"[RelocatingObstacle] {name}: все точки заняты, жду освобождения.", this);
                }

                var cancelled = await UniTask.Delay(
                        System.TimeSpan.FromSeconds(_spotRetryInterval), cancellationToken: token)
                    .SuppressCancellationThrow();

                if (cancelled)
                {
                    return null;
                }
            }
        }

        private bool IsSpotOccupied(Vector3 position)
        {
            var staticObjects = _obstacleView.Controller?.GetStaticObjectController()?.StaticObjects;
            if (staticObjects == null)
            {
                return false;
            }

            var sqrRadius = _spotOccupiedRadius * _spotOccupiedRadius;

            foreach (var view in staticObjects)
            {
                if (view == null || view == _obstacleView || !view.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (((Vector2)(view.transform.position - position)).sqrMagnitude <= sqrRadius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
