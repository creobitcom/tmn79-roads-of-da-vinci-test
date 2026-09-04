using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Transport;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Cysharp.Threading.Tasks;
using Pathfinding;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Funicular
{
    public class LiftController : MonoBehaviour
    {
        private enum PassengerPhase
        {
            /// <summary>Стоит у станции и ждёт кабину. Задачи получать может.</summary>
            Waiting,

            /// <summary>Внутри кабины. Команд не принимает, отпустить можно только на станции.</summary>
            Riding
        }

        /// <summary>Намерение юнита пересечь пролёт. Unknown = путь ещё не готов, решение отложено.</summary>
        private enum CrossingIntent
        {
            Unknown,
            Crosses,
            DoesNotCross
        }

        private sealed class Passenger
        {
            public MovableObjectView Unit;
            public MovableObjectTransportHold Hold;
            public Transform Target;
            public PassengerPhase Phase;
            public bool Released;
        }

        [SerializeField] private NodeLink _linkNode;

        [SerializeField] private Animator _cabinAnimator;
        [SerializeField] private Transform _graphPointA;
        [SerializeField] private Transform _graphPointB;
        [SerializeField] private Transform _cabinSeat;

        [SerializeField] private float _captureRadius = 1f;

        [Tooltip("Разворот пассажира на время ожидания и поездки. 180 = лицом к игроку.")]
        [SerializeField] private float _passengerLookAngle = MovableObjectTransportHold.LookAtCameraAngle;

        [Tooltip("Имя стейта покоя в аниматоре юнита. Animator.Play матчит имя точно, " +
                 "и в разных проектах оно пишется по-разному (Idle / idle).")]
        [SerializeField] private string _passengerIdleState = RuntimeConstants.AnimationStates.Idle;

        [Tooltip("Во что обходится поездка при выборе исполнителя, в единицах расстояния. " +
                 "Кандидату с другой стороны пролёта эта надбавка прибавляется к дистанции до цели, " +
                 "иначе пролёт для подбора бесплатен и задачу регулярно получает юнит не с той стороны. " +
                 "Значение должно заметно превышать типичные расстояния на уровне.")]
        [SerializeField] private float _crossingCostPenalty = 20f;

        [SerializeField] private bool _isStationABroken = true;
        [SerializeField] private bool _isStationBBroken = true;

        [SerializeField] private List<GameplayTagSO> _ignoredTags = new();
        [SerializeField] private float repairedCost = 0.1f;

        [Tooltip("Диагностика захвата, посадки и высадки. Пишется с тегом [LIFT] — ищи по нему в логе. " +
                 "Сыплет каждый кадр, пока у станции стоит незахваченный юнит: включать только на отлов бага.")]
        [SerializeField] private bool _debugCapture;

        private MovableObjectController _unitController;
        private IPauseController _pauseController;
        private CancellationTokenSource _cancellationTokenSource;

        private bool _isProcessing;
        private bool _isAnimationCompleted;
        private bool _isAtA = true;
        private bool _isPaused;

        // Единственный источник правды по пассажирам. Раньше их было два — HashSet «в перевозке»
        // и Queue на посадку, — и они расходились на любом нештатном выходе, оставляя юнита
        // замороженным навсегда.
        private readonly Dictionary<MovableObjectView, Passenger> _passengers = new();
        private readonly Queue<Passenger> _boardingQueue = new();
        private readonly List<Passenger> _releaseBuffer = new();
        private readonly List<Vector3> _remainingPathBuffer = new();
        private readonly Dictionary<MovableObjectView, (Vector3 destination, Transform station)> _lastRides = new();

        private bool _linkSegmentRegistered;
        private Vector3 _registeredLinkStart;
        private Vector3 _registeredLinkEnd;

        public bool IsFullyRepaired => !_isStationABroken && !_isStationBBroken;

        [Inject]
        public void Construct(IObjectViewController objectViewController, IPauseController pauseController)
        {
            _unitController = objectViewController.GetMovableObjectController();
            _pauseController = pauseController;
        }

        private void Awake()
        {
            _cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        }

        /// <summary>Диагностика под галкой Debug Capture. Ищи в логе по тегу [LIFT].</summary>
        private void Log(string message)
        {
            if (!_debugCapture)
            {
                return;
            }

            Debug.Log($"[LIFT] {name}: {message}");
        }

        /// <summary>Компактный портрет юнита: всё, что влияет на его поведение у лифта.</summary>
        private static string Describe(MovableObjectView unit)
        {
            if (unit == null)
            {
                return "<null>";
            }

            var destination = unit.AStarAI != null ? unit.AStarAI.destination : Vector3.zero;

            return $"{unit.name}(state={unit.State.Value}" +
                   $"{(unit.IsTransportSealed ? ",SEALED" : string.Empty)}" +
                   $"{(unit.IsHeldByTransport ? ",held" : string.Empty)}" +
                   $"{(unit.IsMovementLocked ? ",locked" : string.Empty)}" +
                   $",pos={unit.transform.position.x:F1};{unit.transform.position.y:F1}" +
                   $",dst={destination.x:F1};{destination.y:F1}" +
                   $",task={(unit.CurrentRunningTask != null ? unit.CurrentRunningTask.TaskId.ToString() : "-")})";
        }

        private async void Start()
        {
            if (_pauseController != null)
            {
                _pauseController.IsPaused.Subscribe(isPaused =>
                {
                    _isPaused = isPaused;
                    if (_cabinAnimator != null) _cabinAnimator.speed = isPaused ? 0f : 1f;
                }).AddTo(this);
            }

            await UniTask.WaitUntil(() => AstarPath.active != null && !AstarPath.active.isScanning,
                cancellationToken: _cancellationTokenSource.Token);

            UpdateLiftState();
            RegisterLinkSegment();
        }

        private void RegisterLinkSegment()
        {
            if (_linkSegmentRegistered || _linkNode == null || _linkNode.end == null || AstarPath.active == null)
            {
                return;
            }

            // Units must never cross the lift span on foot: AITMNPath's long-segment
            // teleport hack would instantly "jump" them over the gap whenever a path
            // goes through the link while the unit is not in the cabin.
            //
            // Регистрируются позиции НОД графа, а не трансформов: в путь A* кладёт позиции нод,
            // и сравнение с трансформами промахивалось, стоило ноде оказаться дальше допуска.
            // Промах = длинносегментный телепорт перекидывает юнита через пролёт пешком.
            var startNode = AstarPath.active.GetNearest(_linkNode.transform.position).node;
            var endNode = AstarPath.active.GetNearest(_linkNode.end.position).node;

            if (startNode == null || endNode == null)
            {
                return;
            }

            _registeredLinkStart = (Vector3)startNode.position;
            _registeredLinkEnd = (Vector3)endNode.position;

            TransportSpans.Register(_registeredLinkStart, _registeredLinkEnd, _crossingCostPenalty);
            _linkSegmentRegistered = true;
        }

        private void OnDestroy()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }

            if (_linkSegmentRegistered)
            {
                TransportSpans.Unregister(_registeredLinkStart, _registeredLinkEnd);
                _linkSegmentRegistered = false;
            }

            // Уничтожение лифта посреди поездки не должно оставить юнита запертым:
            // без этого он остаётся с canMove = false, нулевой скоростью и чужой анимацией.
            _releaseBuffer.Clear();
            _releaseBuffer.AddRange(_passengers.Values);

            foreach (var passenger in _releaseBuffer)
            {
                ReleasePassenger(passenger);
            }

            _releaseBuffer.Clear();
            _passengers.Clear();
            _boardingQueue.Clear();
            _lastRides.Clear();
        }

        public void RepairStationA()
        {
            _isStationABroken = false;
            UpdateLiftState();
        }

        public void RepairStationB()
        {
            _isStationBBroken = false;
            UpdateLiftState();
        }

        public void RepairAll()
        {
            _isStationABroken = false;
            _isStationBBroken = false;
            UpdateLiftState();
        }

        private void UpdateLiftState()
        {
            if (AstarPath.active == null) return;

            var isWorkable = IsFullyRepaired;

            if (_linkNode != null)
            {
                _linkNode.costFactor = repairedCost;

                AstarPath.active.AddWorkItem(new AstarWorkItem(ctx =>
                {
                    var nodeA = AstarPath.active.GetNearest(_graphPointA.position).node;
                    var nodeB = AstarPath.active.GetNearest(_graphPointB.position).node;

                    if (isWorkable)
                    {
                        if (nodeA != null)
                        {
                            nodeA.Blocked = -1;
                            nodeA.Walkable = true;
                            nodeA.Penalty = 0;
                        }

                        if (nodeB != null)
                        {
                            nodeB.Blocked = -1;
                            nodeB.Walkable = true;
                            nodeB.Penalty = 0;
                        }

                        _linkNode.enabled = false;
                        _linkNode.enabled = true;

                        ctx.QueueFloodFill();
                    }
                    else
                    {
                        _linkNode.enabled = false;
                    }
                }));

                AstarPath.active.FlushWorkItems();

                var guoA = new GraphUpdateObject(new Bounds(_graphPointA.position, Vector3.one * 4f))
                    { updatePhysics = true };
                var guoB = new GraphUpdateObject(new Bounds(_graphPointB.position, Vector3.one * 4f))
                    { updatePhysics = true };

                AstarPath.active.UpdateGraphs(guoA);
                AstarPath.active.UpdateGraphs(guoB);

                AstarPath.active.FlushGraphUpdates();
            }
        }

        private void Update()
        {
            if (_isPaused || !IsFullyRepaired || _unitController == null) return;

            RevalidateWaitingPassengers();
            CaptureCandidates();
        }

        /// <summary>
        /// Ждущему юниту разрешено получать задачи. Поэтому каждый кадр проверяем, ведёт ли
        /// его актуальный маршрут по-прежнему через этот пролёт: цель за лифтом — стоит и ждёт
        /// кабину, цель на своей стороне — отпускаем сразу, уходит пешком.
        /// </summary>
        private void RevalidateWaitingPassengers()
        {
            if (_passengers.Count == 0) return;

            _releaseBuffer.Clear();

            foreach (var passenger in _passengers.Values)
            {
                // Едущего не трогаем: он физически в кабине, высадить его можно только на станции.
                if (passenger.Phase != PassengerPhase.Waiting) continue;

                var unit = passenger.Unit;

                if (unit == null || !unit.gameObject.activeInHierarchy)
                {
                    _releaseBuffer.Add(passenger);
                    continue;
                }

                // Отпускаем только по уверенному «нет». Unknown — это окно между Stop() и новым
                // MoveTo (уход домой после задачи), там пути просто ещё нет; отпускать в нём
                // значило бы дёргать юнита туда-сюда каждый кадр.
                if (EvaluateCrossing(unit, passenger.Target) == CrossingIntent.DoesNotCross)
                {
                    _releaseBuffer.Add(passenger);
                }
            }

            foreach (var passenger in _releaseBuffer)
            {
                Log($"RELEASE-WAITING {Describe(passenger.Unit)}: " +
                                   "маршрут больше не через лифт (или юнит пропал)");

                ReleasePassenger(passenger);
            }

            _releaseBuffer.Clear();
        }

        private void CaptureCandidates()
        {
            foreach (var unit in _unitController.GetUnits())
            {
                if (unit == null || _passengers.ContainsKey(unit)) continue;

                if (!CanUseLift(unit)) continue;

                var distanceToA = Vector3.Distance(unit.transform.position, _graphPointA.position);
                var distanceToB = Vector3.Distance(unit.transform.position, _graphPointB.position);

                var otherStation = distanceToA < distanceToB ? _graphPointB : _graphPointA;
                var closestDistance = Mathf.Min(distanceToA, distanceToB);

                if (closestDistance >= _captureRadius) continue;

                var intent = EvaluateCrossing(unit, otherStation);

                if (intent == CrossingIntent.Crosses)
                {
                    TryCapture(unit, otherStation);
                }
                else if (_debugCapture)
                {
                    // Диагностика: юнит стоит на станции, но в кабину не сел.
                    var ai = unit.AStarAI;
                    Log($"NOT-CAPTURED {Describe(unit)} " +
                                       $"dist={closestDistance:F2}<r={_captureRadius:F2} intent={intent} " +
                                       $"pathPending={ai?.pathPending} hasPath={ai?.hasPath}");
                }
            }
        }

        private bool CanUseLift(MovableObjectView unit)
        {
            if (_ignoredTags == null || _ignoredTags.Count == 0) return true;
            if (unit == null || unit.MovableObjectDataSO == null) return true;

            var objectTags = unit.MovableObjectDataSO.ObjectTypeTags;
            if (objectTags != null)
            {
                foreach (var tag in objectTags)
                {
                    if (_ignoredTags.Contains(tag)) return false;
                }
            }

            var baseTags = unit.MovableObjectDataSO.BaseTypeTags;
            if (baseTags != null)
            {
                foreach (var tag in baseTags)
                {
                    if (_ignoredTags.Contains(tag)) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Прежний IsWantsToCross, но с явным третьим исходом. Unknown вместо false там, где
        /// пути ещё нет: без него «нет пути» было неотличимо от «маршрут не через лифт»,
        /// и решение принималось на пустом месте.
        /// </summary>
        private CrossingIntent EvaluateCrossing(MovableObjectView unit, Transform otherSide)
        {
            var ai = unit.AStarAI;

            if (ai == null || ai.pathPending || !ai.hasPath)
            {
                return CrossingIntent.Unknown;
            }

            ai.GetRemainingPath(_remainingPathBuffer, out var stale);

            if (stale)
            {
                return CrossingIntent.Unknown;
            }

            // Primary rule: the unit's actual route passes through the other station.
            foreach (var pathPoint in _remainingPathBuffer)
            {
                if (Vector3.Distance(pathPoint, otherSide.position) < _captureRadius)
                {
                    return CrossingIntent.Crosses;
                }
            }

            // Fallback rule: the route ends on the other side of the span even though it
            // avoids the station nodes. Levels can contain stray graph connections running
            // parallel to the lift link; a path routed over one of those must still put the
            // unit into the cabin — otherwise it slides across the gap on foot.
            var pathEnd = _remainingPathBuffer.Count > 0
                ? _remainingPathBuffer[_remainingPathBuffer.Count - 1]
                : ai.destination;
            var thisSide = otherSide == _graphPointA ? _graphPointB : _graphPointA;

            // Anti-ping-pong: the straight-line fallback may ferry a unit once per
            // destination. If this unit already rode and ARRIVED at this station with the
            // same destination, only the route-based primary rule may send it back —
            // otherwise objects whose straight-line distance favors the far station keep
            // the unit riding forever without ever walking to them.
            if (_lastRides.TryGetValue(unit, out var lastRide)
                && lastRide.station == thisSide
                && (lastRide.destination - ai.destination).sqrMagnitude < 0.25f)
            {
                return CrossingIntent.DoesNotCross;
            }

            return Vector3.Distance(pathEnd, otherSide.position)
                   < Vector3.Distance(pathEnd, thisSide.position) - 0.05f
                ? CrossingIntent.Crosses
                : CrossingIntent.DoesNotCross;
        }

        private void TryCapture(MovableObjectView unit, Transform destination)
        {
            // Юнита уже кто-то держит (стан Humorist) — пропускаем кадр. Никаких «наполовину
            // захвачен»: либо взяли всё сразу, либо не трогали вовсе.
            var hold = MovableObjectTransportHold.TryAcquire(unit, this, _passengerLookAngle, _passengerIdleState);

            if (hold == null)
            {
                return;
            }

            var passenger = new Passenger
            {
                Unit = unit,
                Hold = hold,
                Target = destination,
                Phase = PassengerPhase.Waiting
            };

            _passengers.Add(unit, passenger);
            _boardingQueue.Enqueue(passenger);

            Log($"CAPTURE {Describe(unit)} -> {destination.name} " +
                               $"(в очереди {_boardingQueue.Count})");

            if (!_isProcessing)
            {
                CabinLoopAsync(_cancellationTokenSource.Token).Forget();
            }
        }

        private void ReleasePassenger(Passenger passenger, Vector3? landingPosition = null)
        {
            if (passenger == null || passenger.Released) return;

            passenger.Released = true;

            if (landingPosition.HasValue)
            {
                passenger.Hold.ReleaseAt(landingPosition.Value);
            }
            else
            {
                passenger.Hold.Release();
            }

            // ReferenceEquals, а НЕ обычное сравнение с null: у уничтоженного юнита Unity-оператор
            // == даёт true, ключ бы не удалился, и запись висела бы в словаре вечно —
            // RevalidateWaitingPassengers складывал бы её в буфер каждый кадр без всякого эффекта.
            // Управляемая ссылка при этом жива и как ключ работает нормально.
            if (!ReferenceEquals(passenger.Unit, null))
            {
                _passengers.Remove(passenger.Unit);
            }
        }

        private async UniTaskVoid CabinLoopAsync(CancellationToken cancellationToken)
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                while (_boardingQueue.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var passenger = _boardingQueue.Dequeue();

                    // Пассажира могли отпустить, пока он стоял в очереди: перенаправили задачей
                    // на эту же сторону, юнит уничтожен, лифт сломали.
                    if (passenger.Released || passenger.Unit == null || !passenger.Unit.gameObject.activeInHierarchy)
                    {
                        ReleasePassenger(passenger);
                        continue;
                    }

                    // Везём только по подтверждённому намерению. Unknown (пути ещё нет) —
                    // отпускаем: неоправданную поездку обосновать нечем, а если юнит
                    // действительно собирался на ту сторону, Update поймает его тем же кадром,
                    // он стоит внутри радиуса захвата.
                    if (EvaluateCrossing(passenger.Unit, passenger.Target) != CrossingIntent.Crosses)
                    {
                        Log($"DEQUEUE-RELEASE {Describe(passenger.Unit)}: " +
                                           "на посадке намерение ехать не подтвердилось");

                        ReleasePassenger(passenger);
                        continue;
                    }

                    Log($"BOARD {Describe(passenger.Unit)} -> {passenger.Target.name}");

                    await RideAsync(passenger, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LiftController] Cabin loop failed: {exception}");
            }
            finally
            {
                // Ни один пассажир не остаётся замороженным, чем бы ни кончился цикл.
                while (_boardingQueue.Count > 0)
                {
                    ReleasePassenger(_boardingQueue.Dequeue());
                }

                _isProcessing = false;
            }
        }

        private async UniTask RideAsync(Passenger passenger, CancellationToken cancellationToken)
        {
            var isGoingToA = passenger.Target == _graphPointA;

            // Кабина не на той стороне — сначала подаём её пустой. Пассажир всё это время
            // ещё Waiting: если ему за это время дадут задачу на его стороне,
            // RevalidateWaitingPassengers отпустит его, и мы выйдем по проверке ниже.
            if (_isAtA == isGoingToA)
            {
                await MoveAnimationAsync(null, !isGoingToA, cancellationToken);

                if (passenger.Released || passenger.Unit == null)
                {
                    ReleasePassenger(passenger);
                    return;
                }
            }

            // С этого момента юнит физически в кабине: задачи ему больше не выдаются.
            passenger.Phase = PassengerPhase.Riding;
            passenger.Hold.SetSealed(true);
            passenger.Hold.SetPosition(_cabinSeat.position);
            passenger.Hold.SetFacing(_passengerLookAngle);

            await MoveAnimationAsync(passenger, isGoingToA, cancellationToken);

            if (passenger.Unit == null)
            {
                ReleasePassenger(passenger);
                return;
            }

            // Высадка: короткое проскальзывание из кабины на станцию. Анимация проигрывается
            // через удержание — берётся актуальная беговая (с поклажей или без), а не
            // безусловный Run, и владение при этом не теряется.
            passenger.Hold.PlayAnimation(passenger.Unit.CurrentRunAnimationState);

            var elapsed = 0f;
            const float smoothTime = 0.15f;
            var startPosition = passenger.Unit.transform.position;
            var endPosition = passenger.Target.position;

            while (elapsed < smoothTime && !cancellationToken.IsCancellationRequested)
            {
                if (passenger.Unit == null) break;

                if (_isPaused) await UniTask.WaitWhile(() => _isPaused, cancellationToken: cancellationToken);

                elapsed += Time.deltaTime;
                passenger.Hold.SetPosition(Vector3.Lerp(startPosition, endPosition, elapsed / smoothTime));

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (passenger.Unit != null && passenger.Unit.AStarAI != null)
            {
                // Запоминаем состоявшуюся поездку, чтобы запасное правило EvaluateCrossing
                // не отправило юнита обратно на ту же цель.
                _lastRides[passenger.Unit] = (passenger.Unit.AStarAI.destination, passenger.Target);
            }

            Log($"LAND {Describe(passenger.Unit)} на {passenger.Target.name}");

            // Отпускание с высадкой: единственный телепорт за всю перевозку. Внутри он
            // возвращает агент, ставит юнита на станцию, перезапрашивает путь к неизменной
            // цели и восстанавливает анимацию, скорость и право получать задачи.
            ReleasePassenger(passenger, passenger.Target.position);
        }

        private async UniTask MoveAnimationAsync(Passenger passenger, bool isGoingToA,
            CancellationToken cancellationToken)
        {
            _isAnimationCompleted = false;
            _cabinAnimator.SetTrigger(isGoingToA ? "GoToA" : "GoToB");
            _isAtA = isGoingToA;

            var safetyTimeout = 20f;

            while (!_isAnimationCompleted && !cancellationToken.IsCancellationRequested)
            {
                if (_isPaused) await UniTask.WaitWhile(() => _isPaused, cancellationToken: cancellationToken);

                if (passenger != null && passenger.Unit != null)
                {
                    passenger.Hold.SetPosition(_cabinSeat.position);
                }

                safetyTimeout -= Time.deltaTime;

                if (safetyTimeout <= 0f)
                {
                    // The cabin animation event never arrived (missed OnReachedDestination on
                    // rapid consecutive triggers). Without this bail-out _isProcessing stays
                    // true forever and every unit captured afterwards is frozen at a station.
                    Debug.LogWarning("[LiftController] Cabin animation completion event was not received, forcing completion.");
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public void OnReachedDestination()
        {
            _isAnimationCompleted = true;
        }

#if UNITY_EDITOR
        [Button("Calculate Optimal Capture Radius", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void CalculateCaptureRadius()
        {
            if (_graphPointA == null || _graphPointB == null)
            {
                Debug.LogWarning("Сначала назначьте Graph Point A и Graph Point B!");
                return;
            }

            float distance = Vector3.Distance(_graphPointA.position, _graphPointB.position);

            _captureRadius = distance * 0.4f;

            if (_captureRadius < 0.1f)
                _captureRadius = 0.1f;

            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log(
                $"<b>[LiftController]</b> Дистанция между станциями: {distance:F2} м. Оптимальный Capture Radius установлен на: <b>{_captureRadius:F2}</b>");
        }
#endif
    }
}
