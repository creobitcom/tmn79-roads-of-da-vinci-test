using System;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Herding
{
    ///
    /// <summary>
    /// «Объект, который юнит уводит в загон» (GG6: овца → овчарня).
    ///
    /// Ожидаемая структура префаба — компонент лежит на КОРНЕ, который сам по себе не ObjectView:
    ///
    ///   Root                       ← этот компонент, остаётся активным всегда
    ///   ├── Obstacle               ← StaticObjectView: BlocksPath, CanInteract,
    ///   │                            UnitTypeCount = [тег юнита : 1], InteractionsAmount = 1,
    ///   │                            DeactivateAfterFinalInteraction = true,
    ///   │                            ReturnMovableObjectsToHomePoint = false
    ///   └── Walker                 ← MovableObjectView, ВЫКЛЮЧЕН, ActivationByConditions = true
    ///
    /// Walker обязан лежать РЯДОМ с Obstacle, а не внутри него: Obstacle после финального
    /// взаимодействия гасит себя (SetActive(false)), и вложенный Walker уже не включить.
    ///
    /// Логика: пока нет ни одного активного <see cref="HerdSpot"/> — клик по препятствию
    /// заблокирован. Как только загон готов, клик разрешается, приходит юнит нужного типа,
    /// отрабатывает взаимодействие, препятствие гаснет (и разблокирует ноду графа штатным
    /// UnBlockInteractionGraphNode из шаблона Obstacle), после чего Walker и юнит идут к загону.
    /// По прибытии юнит отпускается и штатно уходит домой.
    /// </summary>
    [DisallowMultipleComponent]
    public class HerdableObject : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField, Tooltip("Кликабельное препятствие. Пусто — ищется в детях.")]
        private StaticObjectView _obstacleView;

        [SerializeField, Tooltip("Ходячая версия объекта. Выключена до старта перегона.")]
        private MovableObjectView _walker;

        [Header("Rules")]
        [SerializeField, Tooltip("Блокировать клик по препятствию, пока нет доступного загона.")]
        private bool _requireSpotToInteract = true;

        [SerializeField, Tooltip("Юнит идёт к загону вместе с объектом, а не сразу домой.")]
        private bool _escortByUnit = true;

        [SerializeField, Tooltip("Спрятать объект по прибытии в загон. Выкл — останется стоять.")]
        private bool _hideWalkerOnArrival;

        [SerializeField, Min(0f), Tooltip("Задержка перед стартом объекта, сек.")]
        private float _walkerStartDelay;

        [SerializeField, Min(0f), Tooltip("Предохранитель: сколько ждать прибытия, сек. 0 — ждать всегда. " +
                                          "Тикает в реальном времени, включая паузу.")]
        private float _moveTimeout;

        [Header("Events")]
        [SerializeField, Tooltip("Появился доступный загон — клик разблокирован.")]
        private UltEvent _onSpotBecameAvailable;

        [SerializeField, Tooltip("Юнит закончил работу, объект пошёл в загон.")]
        private UltEvent _onHerdStarted;

        [SerializeField, Tooltip("Объект дошёл до загона. Сюда вешается зачёт таски: сопровождающего " +
                                 "не ждём, чтобы залипший юнит не сделал уровень непроходимым.")]
        private UltEvent _onHerdArrived;

        [SerializeField, Tooltip("Сопровождающий отпущен и пошёл домой.")]
        private UltEvent _onEscortFinished;

        private bool _isHerding;
        private bool _finished;
        private bool _subscribed;
        private bool _lastSpotAvailable;
        private bool _gateInitialized;

        private void Reset()
        {
            _obstacleView = GetComponentInChildren<StaticObjectView>(true);
        }

        private void Awake()
        {
            if (_obstacleView == null)
            {
                _obstacleView = GetComponentInChildren<StaticObjectView>(true);
            }

            if (_obstacleView == null)
            {
                Debug.LogError($"[Herding] {name}: не задан StaticObjectView препятствия.", this);
                enabled = false;
                return;
            }

            if (_walker == null)
            {
                Debug.LogError($"[Herding] {name}: не задан Walker (MovableObjectView).", this);
                enabled = false;
                return;
            }

            if (_walker.transform.IsChildOf(_obstacleView.transform))
            {
                Debug.LogError($"[Herding] {name}: Walker вложен в препятствие. Препятствие гасит себя после " +
                               "взаимодействия, и включить Walker уже не получится — вынеси его на уровень выше.",
                    this);
            }

            if (!_walker.ActivationByConditions)
            {
                Debug.LogError($"[Herding] {_walker.name}: включи ActivationByConditions на MovableObjectView. " +
                               "Иначе контроллер юнитов сам покажет объект на старте уровня.", _walker);
            }

            _walker.gameObject.SetActive(false);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (!_requireSpotToInteract || _finished || _isHerding || _obstacleView == null) return;

            var available = HerdSpot.HasAvailable();

            // Сравниваем с фактическим состоянием вьюшки, а не с кэшем: ObjectView выставляет
            // CanReactToPrimaryAction из SO в своём Load(), который может отработать позже нашего Awake.
            if (_obstacleView.CanReactToPrimaryAction != available)
            {
                _obstacleView.SetPrimaryActionState(available);
            }

            // На первом кадре только запоминаем состояние: событие — про появление загона по ходу
            // уровня, а не про то, что он уже был готов на старте.
            if (_gateInitialized && available && !_lastSpotAvailable)
            {
                _onSpotBecameAvailable?.Invoke();
            }

            _lastSpotAvailable = available;
            _gateInitialized = true;
        }

        private void Subscribe()
        {
            if (_subscribed || _obstacleView == null) return;

            _obstacleView.OnSpecialUnitsEndInteract ??= new UltEvent<MovableObjectView>();
            _obstacleView.OnSpecialUnitsEndInteract.DynamicCalls += OnUnitFinishedInteraction;

            _obstacleView.onEndInteract ??= new UltEvent();
            _obstacleView.onEndInteract.DynamicCalls += OnInteractionFinished;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _obstacleView == null) return;

            if (_obstacleView.OnSpecialUnitsEndInteract != null)
            {
                _obstacleView.OnSpecialUnitsEndInteract.DynamicCalls -= OnUnitFinishedInteraction;
            }

            if (_obstacleView.onEndInteract != null)
            {
                _obstacleView.onEndInteract.DynamicCalls -= OnInteractionFinished;
            }

            _subscribed = false;
        }

        /// <summary>
        /// Приходит из StaticObjectView.OnInteractionEnd по каждому отработавшему юниту — раньше,
        /// чем юнита отпустят домой.
        /// </summary>
        private void OnUnitFinishedInteraction(MovableObjectView unit) => StartHerding(unit);

        /// <summary>
        /// Фолбэк для объектов без UnitTypeCount (кликнули — и всё, юнит не нужен). Срабатывает
        /// после OnSpecialUnitsEndInteract, поэтому двойного старта не будет.
        /// </summary>
        private void OnInteractionFinished() => StartHerding(null);

        private void StartHerding(MovableObjectView unit)
        {
            if (_isHerding || _finished) return;

            _isHerding = true;
            HerdAsync(unit).Forget();
        }

        private async UniTaskVoid HerdAsync(MovableObjectView unit)
        {
            var token = this.GetCancellationTokenOnDestroy();

            try
            {
                // Даём базовому OnInteractionEnd доиграть: там объект гасится, нода графа
                // разблокируется, задача юнита завершается и он становится Idle.
                await UniTask.NextFrame(token);

                var origin = _walker != null ? _walker.transform.position : transform.position;
                var spot = HerdSpot.FindNearestAvailable(origin);

                if (spot == null)
                {
                    Debug.LogWarning($"[Herding] {name}: нет доступного HerdSpot — вести некуда.", this);
                    _isHerding = false;
                    return;
                }

                spot.Occupy();
                _finished = true;

                var escortUnit = _escortByUnit ? unit : null;
                if (escortUnit != null)
                {
                    HoldUnit(escortUnit);
                }

                _onHerdStarted?.Invoke();

                if (_walkerStartDelay > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_walkerStartDelay), cancellationToken: token);
                }

                var walkerTask = UniTask.CompletedTask;
                if (_walker != null)
                {
                    _walker.SetActivationState(true);
                    walkerTask = MoveAndWait(_walker, spot.ArrivalPoint, _moveTimeout, token, OnWalkerArrived);
                }

                // Юнит возвращает себе видимость сразу по своему прибытию, не дожидаясь объекта:
                // MovableObjectController на EndMove без CurrentTaskObject прячет юнита с
                // UnitHideInBase, и до общего WhenAll пастух стоял бы у загона невидимым.
                var unitTask = escortUnit != null
                    ? MoveAndWait(escortUnit, spot.EscortPoint, _moveTimeout, token, RestoreVisibility)
                    : UniTask.CompletedTask;

                await UniTask.WhenAll(walkerTask, unitTask);

                if (escortUnit != null)
                {
                    ReleaseUnit(escortUnit);
                    _onEscortFinished?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // уровень выгружается — ничего делать не надо
            }
            finally
            {
                _isHerding = false;
            }
        }

        /// <summary>
        /// Объект дошёл до загона. Момент, по которому засчитывается выполнение — специально не
        /// ждём сопровождающего: если юнит где-то залипнет, таска всё равно закроется.
        /// </summary>
        private void OnWalkerArrived(MovableObjectView walker)
        {
            if (_hideWalkerOnArrival)
            {
                walker.SetActivationState(false);
            }

            _onHerdArrived?.Invoke();
        }

        /// <summary>Забираем юнита себе, чтобы контроллер не увёл его домой и не выдал новую задачу.</summary>
        private static void HoldUnit(MovableObjectView unit)
        {
            unit.returnHomeTokenSource?.Cancel();
            unit.State.Value = UnitState.Work;

            // Ненулевой Offset заставит контроллер после прибытия телепортировать юнита в сторону
            // (MoveUnitOutOfPath) — при сопровождении это лишнее.
            unit.Offset = Vector2.zero;
        }

        /// <summary>
        /// Контроллер на EndMove без CurrentTaskObject прячет юнита с UnitHideInBase (у пастуха он
        /// включён). Возвращаем видимость тем же кадром, иначе юнит пропадает у загона.
        /// </summary>
        private static void RestoreVisibility(MovableObjectView unit)
        {
            if (!unit.ActivationByConditions && !unit.gameObject.activeSelf)
            {
                unit.SetActivationState(true);
            }
        }

        private static void ReleaseUnit(MovableObjectView unit)
        {
            RestoreVisibility(unit);

            // Idle → контроллер сам отправит его домой через штатную задержку.
            unit.State.Value = UnitState.Idle;
        }

        private static async UniTask MoveAndWait(
            MovableObjectView view,
            Transform target,
            float timeout,
            CancellationToken token,
            Action<MovableObjectView> onArrived = null)
        {
            var arrived = false;

            void OnEndMove(MovableObjectView v)
            {
                if (arrived) return;

                arrived = true;
                onArrived?.Invoke(v);
            }

            view.EndMove += OnEndMove;

            try
            {
                view.MoveTo(target);

                if (timeout > 0f)
                {
                    var deadline = Time.unscaledTime + timeout;
                    await UniTask.WaitUntil(() => arrived || Time.unscaledTime >= deadline,
                        cancellationToken: token);
                }
                else
                {
                    await UniTask.WaitUntil(() => arrived, cancellationToken: token);
                }
            }
            finally
            {
                view.EndMove -= OnEndMove;
            }
        }
    }
}
