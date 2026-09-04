using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.Environments;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.StandingPoint;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Audio;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Pathfinding;
using R3;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public class MovableObjectView : ObjectView
    {
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onStartMove;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onEndMove;
        
        [SerializeField] private SortingGroup _sortingGroup;
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _speedBoosterEffect;
        [SerializeField] public MovableObjectDataSO movableObjectDataSo;
        [SerializeField] public MovableObjectTaskView movableObjectTaskView;
        [SerializeField] public SpriteRenderer resourceIcon;
        [SerializeField] public Transform modelTransform;
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ActivationConditions)]
        [field: SerializeField] public bool ActivationByConditions { get; private set; }
        
        private readonly ReactiveProperty<UnitState> _state = new();
        private readonly Dictionary<ResourceBaseSO, int> _unitInventory = new();
        private readonly HashSet<Environments.AccelerationZone> _activeAccelerationZones = new();
        private readonly HashSet<object> _movementLockOwners = new();
        private float _defaultInteractionOffset;
        private string _currentAnimation;
        private string _currentRunAnimation;
        private object _temporaryAnimationOwner;
        private int _animationBeforeTemporaryStateHash;
        private float _animationBeforeTemporaryNormalizedTime;
        private string _temporaryAnimationState;
        private int _pausedTemporaryStateHash;
        private float _pausedTemporaryNormalizedTime;
        private float _animatorSpeedBeforeTemporaryPause = 1f;
        
        public GameObject stealArea;
        public IDisposable TriggerObservable;
        public DisposableBag UpdateObserver;
        public Quaternion initialRotation;
        public IAstarAI AStarAI;
        public ITaskObject CurrentTaskObject;
        public Vector3 currentDestination;

        // True when a MoveTo targets (almost) the current position ("stay in place" moves like
        // Stealing / WaitFireman that target the unit itself). Purely animation: suppresses the
        // run/idle swap so it doesn't walk-in-place and doesn't override the steal "picking_up".
        // Movement/EndMove logic is left untouched.
        private bool _skipMoveAnimation;
        public float delayToMoveHome;
        public int delayBeforeMoveToHome;
        public IMovableObject MovableObject;
        public MovableObjectTask CurrentRunningTask = null;
        
        public CancellationTokenSource returnHomeTokenSource { get; set; }
        public StaticObjectView CurrentBasement { get; set; }
        public float CurrentSpeed { get; private set; }
        public Vector2 Offset { get; set; }
        public bool WasLoaded { get; private set; }
        public bool HasReachedTaskPoint { get; private set; }
        public bool IsMovementLocked => _movementLockOwners.Count > 0;

        /// <summary>
        /// Уровень пройден: юнит замер в Idle до конца сцены. См. <see cref="FreezeForLevelEnd"/>.
        /// </summary>
        public bool IsLevelEndFrozen { get; private set; }

        public StandingPointView CurrentStandingPoint { get; set; }
        public bool UnitHideInBase => MovableObjectDataSO.UnitHideInBase;

        // Whether the base panel currently counts this unit as being at home. The panel keeps
        // its free/busy number as a running +1/-1 total, so every "went out" must be matched by
        // exactly one "came back". Homecomings are reported more than once (see CheckUnitState),
        // and this flag is what makes those reports idempotent. Units are born at their base.
        public bool IsCountedInBase { get; set; } = true;
        public Dictionary<ResourceBaseSO, int> UnitInventory => _unitInventory;
        private string CurrentRunAnimation => _currentRunAnimation ?? RuntimeConstants.AnimationStates.Run;

        /// <summary>
        /// Актуальная беговая анимация юнита (с поклажей или без). Нужна транспорту: он
        /// проигрывает её на высадке через удержание, вместо безусловного Run, который
        /// терял RunBag у юнита с ресурсом.
        /// </summary>
        public string CurrentRunAnimationState => CurrentRunAnimation;
        public override ObjectDataSO ObjectDataSO => movableObjectDataSo;
        public MovableObjectDataSO MovableObjectDataSO => movableObjectDataSo;
        public SortingGroup SortingGroup => _sortingGroup;
        public ReactiveProperty<UnitState> State => _state;

        public event Action<MovableObjectView> StartMove;
        public event Action<MovableObjectView> EndMove;
        public event Action<MovableObjectView> OnEndInteract;
        public event Action<MovableObjectView> OnCancelTask;
        public event Action<bool> MovementLockStateChanged;
        
        public override UniTask Load()
        {
            if (WasLoaded)
                return UniTask.CompletedTask;

            AStarAI = GetComponent<IAstarAI>();

            GetComponent<AStarPathfindingBridge>().Load(this);

            MovableObject = GetComponent<IMovableObject>();
            MovableObject.StartMove += OnStartMovableObjectMove;
            MovableObject.EndMove += OnEndMovableObjectMove;

            SetDataValues();
            SetSpeed(CurrentSpeed);
            
            WasLoaded = true;

            return base.Load();
        }
        
        private void OnEnable()
        {
            // Every SetActive(false)->SetActive(true) cycle (unit hidden in base, model hidden
            // while working inside a building) resets the Animator to its default state and drops
            // any pending triggers, so a trigger set around the activation frame is silently lost
            // and the unit keeps gliding without its animation. Re-apply the logical current
            // animation at the moment the unit becomes visible.
            //
            // On the very first Instantiate this runs before Load: _currentAnimation is null and
            // the method exits without touching anything. Pause and temporary animations own the
            // Animator at their moments — PlayAnimation defers to them by itself.
            if (string.IsNullOrEmpty(_currentAnimation) || _animator == null)
                return;

            PlayAnimation(_currentAnimation);
        }

        private void OnDestroy()
        {
            MovableObject.StartMove -= OnStartMovableObjectMove;
            MovableObject.EndMove -= OnEndMovableObjectMove;

            returnHomeTokenSource?.Cancel();
            _activeAccelerationZones.Clear();
            _movementLockOwners.Clear();
        }

        [Inject]
        public void Construct(IObjectViewController objectViewController,
            GameplaySceneReferences gameplaySceneReferences,
            IAudioService audioController)
        {
            objectViewController.GetMovableObjectController().AddUnit(this);

            delayToMoveHome = gameplaySceneReferences.GameplaySettings.DelayAfterTask;
            AudioController = audioController;
        }

        private void SetDataValues()
        {
            CurrentSpeed = movableObjectDataSo.Speed;
            InteractionSpeed = movableObjectDataSo.InteractionSpeed;
        }
        
        public void MoveToFinishPoint(Transform finishPoint)
        {
            HasReachedTaskPoint = false;
            SetActivationState(true);

            MovableObject.MoveTo(finishPoint.position);
        }

        public void MoveTo(Vector3 target, ITaskObject objectView, float interactionOffset = 0f)
        {
            // Skip the move only when one to this object is genuinely in flight. A bare
            // equality check also swallowed moves for stationary units left with a stale
            // CurrentTaskObject (task completed/cancelled without EndUnitInteract), which
            // froze them and their new task forever.
            if (CurrentTaskObject == objectView
                && AStarAI != null && (AStarAI.hasPath || AStarAI.pathPending))
            {
                return;
            }

            SetActivationState(true);

            HasReachedTaskPoint = false;
            currentDestination = target;
            CurrentTaskObject = objectView;

            AStarAI.EndReachedDistance = interactionOffset;

            _skipMoveAnimation = Vector3.Distance(transform.position, target) <= 0.05f;

            MovableObject.MoveTo(target);
        }

        public void MoveTo(Transform target)
        {
            SetActivationState(true);

            HasReachedTaskPoint = false;
            currentDestination = target.position;
            CurrentTaskObject = null;

            AStarAI.EndReachedDistance = 0.5f;

            _skipMoveAnimation = Vector3.Distance(transform.position, target.position) <= 0.05f;

            MovableObject.MoveTo(target.position);
        }

        public void SetBasement(StaticObjectView basement)
        {
            Load();
            CurrentBasement = basement;
        }

        public void CancelTask()
        {
            HasReachedTaskPoint = false;
            MovableObject?.Stop().Forget();
            CurrentRunningTask = null;
            Offset = Vector2.zero;

            OnCancelTask?.Invoke(this);

            if (CurrentStandingPoint)
            {
                CurrentStandingPoint.FreePoint();
                CurrentStandingPoint = null;
            }
            
            if (movableObjectTaskView != null)
            {
                movableObjectTaskView.gameObject.SetActive(false);
            }
            
            EndUnitInteract();
        }

        public void SetActivationState(bool activated)
        {
            gameObject.SetActive(activated);
        }

        private void Update()
        {
            // A movable object can itself be a task target (e.g. a fireman dispatched to an enemy).
            // The task pathfinder routes to interactionPosition, which only StaticObjectView assigns —
            // on a MovableObjectView it would stay (0,0), so every path resolved to world origin and
            // failed with "no path". Keep it synced to the live position so paths reach the object.
            interactionPosition = transform.position;
        }

        public override void Pause()
        {
            if (_temporaryAnimationOwner != null && _animator != null)
            {
                var state = _animator.GetCurrentAnimatorStateInfo(0);
                _pausedTemporaryStateHash = state.fullPathHash;
                _pausedTemporaryNormalizedTime = state.normalizedTime;
                _animatorSpeedBeforeTemporaryPause = _animator.speed;
                _animator.speed = 0f;
            }
            else
            {
                PlayAnimation(RuntimeConstants.AnimationStates.Idle, false);
            }

            base.Pause();
            SetSpeed(0f, false);
        }

        public override void Unpause()
        {
            base.Unpause();
            SetSpeed(CurrentSpeed, false);

            if (_temporaryAnimationOwner != null && _animator != null)
            {
                if (_pausedTemporaryStateHash != 0)
                {
                    _animator.Play(_pausedTemporaryStateHash, 0, _pausedTemporaryNormalizedTime);
                    _animator.speed = _animatorSpeedBeforeTemporaryPause;
                    _pausedTemporaryStateHash = 0;
                    _pausedTemporaryNormalizedTime = 0f;
                    _animatorSpeedBeforeTemporaryPause = 1f;
                }
                else
                {
                    _animator.Play(_temporaryAnimationState);
                }
            }
            else
            {
                PlayAnimation(_currentAnimation);
            }
        }

        private void OnEndMovableObjectMove(MovableObjectView movableObject)
        {
            HasReachedTaskPoint = CurrentTaskObject != null;

            // For "stay in place" moves don't force idle here — it would override an animation
            // started right after (e.g. steal "picking_up"). Events still fire normally.
            if (!_skipMoveAnimation)
            {
                _animator.ResetTrigger(CurrentRunAnimation);

                PlayAnimation(RuntimeConstants.AnimationStates.Idle);
            }

            _onEndMove?.Invoke();
            EndMove?.Invoke(this);
        }

        private void OnStartMovableObjectMove()
        {
            HasReachedTaskPoint = false;

            // "Stay in place" move: go idle instead of run (stops any leftover run animation).
            // A following explicit animation (steal "picking_up") will override it.
            if (_skipMoveAnimation)
            {
                _animator.ResetTrigger(CurrentRunAnimation);
                PlayAnimation(RuntimeConstants.AnimationStates.Idle);
            }
            else
            {
                PlayAnimation(CurrentRunAnimation);
            }

            _onStartMove?.Invoke();
            StartMove?.Invoke(this);
        }

        public void StartUnitInteract()
        {
            _onStartInteract?.Invoke();
        }

        public void EndUnitInteract(bool forcedHome = false)
        {
            HasReachedTaskPoint = false;

            if (!forcedHome)
            {
                PlayAnimation(RuntimeConstants.AnimationStates.Idle);
            }

            CurrentTaskObject = null;
            
            onEndInteract?.Invoke();
            OnEndInteract?.Invoke(this);
        }
        
        /// <summary>
        /// Set MovableObject speed.
        /// </summary>
        public void SetCurrentSpeed() => SetSpeed(CurrentSpeed, false);

        /// <summary>
        /// Set MovableObject speed.
        /// </summary>
        /// <param name="speed">Speed value.</param>
        /// <param name="updateCurrent">Should currentSpeed be updated</param>
        public void SetSpeed(float speed, bool updateCurrent = true)
        {
            if (updateCurrent) CurrentSpeed = speed;

            
            var finalSpeed = speed;
            
            if (speed > 0.01f)
            {
                var maxZoneMultiplier = 0f;
                var isInActiveZone = false;
        
                foreach (var zone in _activeAccelerationZones)
                {
                    if (zone.State && (!isInActiveZone || zone.SpeedMultiplier > maxZoneMultiplier))
                    {
                        maxZoneMultiplier = zone.SpeedMultiplier;
                        isInActiveZone = true;
                    }
                }

                var zoneAdditiveSpeed = isInActiveZone ? (movableObjectDataSo.Speed * maxZoneMultiplier) : 0f;
        
                finalSpeed = speed + zoneAdditiveSpeed;
        
                finalSpeed = Mathf.Max(0.1f, finalSpeed); 
            }
            
            if (_isPaused || IsMovementLocked)
            {
                finalSpeed = 0f;
            }

            MovableObject.SetSpeed(finalSpeed);
            PlayAnimation(_currentAnimation);
        }

        /// <summary>
        /// Locally stops this object without clearing its path or current task.
        /// Every owner must release its own lock before movement resumes.
        /// </summary>
        public bool TryAcquireMovementLock(object owner)
        {
            if (owner == null || !_movementLockOwners.Add(owner))
            {
                return false;
            }

            SetSpeed(CurrentSpeed, false);
            MovementLockStateChanged?.Invoke(true);
            return true;
        }

        public bool HasMovementLock(object owner)
        {
            return owner != null && _movementLockOwners.Contains(owner);
        }

        public void ReleaseMovementLock(object owner)
        {
            if (owner == null || !_movementLockOwners.Remove(owner))
            {
                return;
            }

            SetSpeed(CurrentSpeed, false);
            MovementLockStateChanged?.Invoke(IsMovementLocked);
        }

        #region Transport hold

        // Юнита может держать только один транспорт за раз — он физически в одной кабине.
        // Поэтому владелец один, без набора: попытка второго захвата честно проваливается,
        // а не создаёт двух хозяев анимации с разъехавшимся восстановлением.
        private object _transportOwner;
        private bool _transportSealed;

        /// <summary>
        /// Юнита физически везёт транспорт: снаружи его нельзя двигать, телепортировать
        /// и считать «дошедшим» по позиции.
        /// </summary>
        public bool IsHeldByTransport => _transportOwner != null;

        /// <summary>
        /// Юнит внутри едущей кабины: новые задачи ему не выдаются. Пока он только ждёт
        /// транспорт, флаг false — такой юнит остаётся обычным свободным рабочим.
        /// </summary>
        public bool IsTransportSealed => _transportSealed;

        /// <summary>
        /// Печать снята или поставлена. Обязательное событие, а не просто флаг: пока юнит
        /// запечатан, его не видят ни GetFreeUnits, ни TryBeginTask, и выданная за это время
        /// задача остаётся в очереди без исполнителей. Сама очередь перезапускается только по
        /// СМЕНЕ State, которой тут нет — юнит как был Idle, так и остался. Слушатель обязан
        /// прогнать разбор очереди руками.
        /// </summary>
        public event Action<MovableObjectView> TransportSealChanged;

        public bool TryBeginTransportHold(object owner)
        {
            if (owner == null || _transportOwner != null)
            {
                return false;
            }

            _transportOwner = owner;
            _transportSealed = false;

            return true;
        }

        public void SetTransportSealed(object owner, bool isSealed)
        {
            if (!ReferenceEquals(_transportOwner, owner) || _transportSealed == isSealed)
            {
                return;
            }

            _transportSealed = isSealed;

            TransportSealChanged?.Invoke(this);
        }

        public void EndTransportHold(object owner)
        {
            if (!ReferenceEquals(_transportOwner, owner))
            {
                return;
            }

            var wasSealed = _transportSealed;

            _transportOwner = null;
            _transportSealed = false;

            if (wasSealed)
            {
                TransportSealChanged?.Invoke(this);
            }
        }

        #endregion

        #region Level end freeze

        /// <summary>
        /// Уровень пройден: юнит встаёт в Idle и больше не двигается до конца сцены.
        /// </summary>
        /// <remarks>
        /// Отдельное состояние, а не <see cref="Pause"/>, потому что пауза обратима и общая:
        /// <c>IPauseController</c> — один bool без владельцев, и Resume любого другого хозяина
        /// (пузырь, катсцена), закрывшегося уже после победы, снимал паузу победы вместе со
        /// своей. Юниты при этом восстанавливали ДОпаузную анимацию (<c>Pause</c> ставит Idle с
        /// <c>updateCurrent: false</c>, то есть <see cref="_currentAnimation"/> оставался Run/Axe)
        /// — отсюда «бежит на месте» и «доигрывает удар» на экране победы.
        /// </remarks>
        public void FreezeForLevelEnd()
        {
            if (IsLevelEndFrozen)
            {
                return;
            }

            IsLevelEndFrozen = true;

            // Временную анимацию (удар юмориста, поездка в кабине) отбираем силой: пока она жива,
            // Pause замораживает КАДР удара вместо Idle, а PlayAnimation до аниматора не доходит.
            ForceEndTemporaryAnimation();

            // updateCurrent намеренно по умолчанию (true): восстанавливать после финиша нечего,
            // и любой поздний Unpause/OnEnable должен вернуть именно Idle.
            PlayAnimation(RuntimeConstants.AnimationStates.Idle);

            // Именно лок движения, а не сырые canMove/isStopped: он гасит скорость через SetSpeed,
            // уводит ротатор (тот сам выходит по IsMovementLocked) и не трогает путь, поэтому
            // AITMNPath не телепортит юнита в конец сегмента.
            TryAcquireMovementLock(this);
        }

        /// <summary>
        /// Снимает заморозку финиша. Нужна только на перезагрузке уровня: если юнита когда-нибудь
        /// начнут переиспользовать вместо пересоздания, замороженный останется недвижимым навсегда.
        /// </summary>
        public void UnfreezeAfterLevelEnd()
        {
            if (!IsLevelEndFrozen)
            {
                return;
            }

            IsLevelEndFrozen = false;

            ReleaseMovementLock(this);
        }

        /// <summary>
        /// Сбрасывает владение временной анимацией без ключа владельца. Владелец после этого
        /// просто теряет управление: все его методы сверяются по ReferenceEquals и тихо выходят.
        /// </summary>
        private void ForceEndTemporaryAnimation()
        {
            if (_temporaryAnimationOwner == null)
            {
                return;
            }

            _temporaryAnimationOwner = null;
            _temporaryAnimationState = null;

            // Pause мог оставить аниматор на нулевой скорости, замороженным на кадре удара.
            if (_animator != null && _pausedTemporaryStateHash != 0)
            {
                _animator.speed = _animatorSpeedBeforeTemporaryPause;
            }

            _pausedTemporaryStateHash = 0;
            _pausedTemporaryNormalizedTime = 0f;
            _animatorSpeedBeforeTemporaryPause = 1f;
            _animationBeforeTemporaryStateHash = 0;
            _animationBeforeTemporaryNormalizedTime = 0f;
        }

        #endregion

        /// <summary>
        /// Temporarily overrides the current Animator state and remembers the exact state/time
        /// that must be restored afterwards. It does not change the logical current animation.
        /// </summary>
        public bool TryBeginTemporaryAnimation(object owner, string stateName)
        {
            if (owner == null || _animator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            if (_temporaryAnimationOwner != null && !ReferenceEquals(_temporaryAnimationOwner, owner))
            {
                return false;
            }

            if (_temporaryAnimationOwner == null)
            {
                var currentState = _animator.GetCurrentAnimatorStateInfo(0);
                _animationBeforeTemporaryStateHash = currentState.fullPathHash;
                _animationBeforeTemporaryNormalizedTime = currentState.normalizedTime;
                _temporaryAnimationOwner = owner;
            }

            return PlayTemporaryAnimation(owner, stateName);
        }

        public bool PlayTemporaryAnimation(object owner, string stateName)
        {
            if (!ReferenceEquals(_temporaryAnimationOwner, owner) || _animator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            _temporaryAnimationState = stateName;
            if (!_isPaused)
            {
                _animator.Play(stateName);
            }

            return true;
        }

        public void EndTemporaryAnimation(object owner)
        {
            if (!ReferenceEquals(_temporaryAnimationOwner, owner))
            {
                return;
            }

            _temporaryAnimationOwner = null;
            _temporaryAnimationState = null;
            _pausedTemporaryStateHash = 0;
            _pausedTemporaryNormalizedTime = 0f;
            _animatorSpeedBeforeTemporaryPause = 1f;

            if (!_isPaused && _animator != null && _animationBeforeTemporaryStateHash != 0)
            {
                _animator.Play(_animationBeforeTemporaryStateHash, 0, _animationBeforeTemporaryNormalizedTime);
            }

            _animationBeforeTemporaryStateHash = 0;
            _animationBeforeTemporaryNormalizedTime = 0f;
        }

        /// <summary>
        /// Set animation as current and play it.
        /// </summary>
        /// <param name="name">Animation name.</param>
        /// <param name="updateCurrent">Should currentAnimation be updated</param>
        public void PlayAnimation(string name, bool updateCurrent = true)
        {
            // Log.Gameplay.Info("Animator : PlayAnimation");

            // Уровень пройден — до аниматора доходит только Idle. Всё остальное (Run от
            // MoveTo/MoveToHomePointAfterDelay, удар от подхваченной задачи) отсекается ЦЕЛИКОМ,
            // вместе со сбросом триггеров: сброс снял бы уже выставленный Idle у юнита, который
            // ещё не успел его проиграть, и тот так и остался бы бежать на месте.
            if (IsLevelEndFrozen && name != RuntimeConstants.AnimationStates.Idle)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_currentAnimation))
            {
                var prev = ResolveAnimatorParam(_currentAnimation);
                if (prev != null) _animator.ResetTrigger(prev);
            }

            if (updateCurrent)
            {
                _currentAnimation = name;
            }

            // Заморозка финиша игнорирует паузу: сюда доходит только Idle (см. гейт выше), а
            // поставить его надо даже под уже поднятой глобальной паузой победы — иначе юнит,
            // чей аниматор в этот момент держала временная анимация, останется в кадре удара.
            if ((_isPaused && !IsLevelEndFrozen) || _temporaryAnimationOwner != null)
                return;

            if (!string.IsNullOrEmpty(name))
            {
                var resolved = ResolveAnimatorParam(name);
                if (resolved != null) _animator.SetTrigger(resolved);
            }

            if (name is nameof(ObjectViewInteractionType.Pickaxe)
                or nameof(ObjectViewInteractionType.Hammer)
                or nameof(ObjectViewInteractionType.Axe)
                or nameof(ObjectViewInteractionType.Take))
            {
                _animator.speed = InteractionSpeed;
            }
            else if (name is RuntimeConstants.AnimationStates.Run or RuntimeConstants.AnimationStates.RunBag)
            {
                var animationSpeed = CurrentSpeed - movableObjectDataSo.Speed;
                animationSpeed = animationSpeed == 0 ? 1 : animationSpeed / 3f;
                _animator.speed = animationSpeed;
            }
            else
            {
                _animator.speed = 1f;
            }
        }

        private Dictionary<string, string> _animatorParamLookup;

        /// <summary>
        /// Resolves an animator parameter name ignoring case, so e.g. "Idle" matches a parameter
        /// named "idle". Returns null if there is no such parameter (avoids Unity warnings).
        /// </summary>
        private string ResolveAnimatorParam(string name)
        {
            if (string.IsNullOrEmpty(name) || _animator == null)
                return null;

            // An Animator that has never been active for a frame (units spawn hidden in base and
            // get SetActive(false) in the spawn frame) reports an EMPTY parameters array. Caching
            // that empty result permanently killed every animation trigger of this unit for its
            // whole life ("T-pose gliding" on the first cold launch). An empty lookup is therefore
            // never treated as valid — rebuild until the animator is initialized and yields
            // its real parameter list.
            if (_animatorParamLookup == null || _animatorParamLookup.Count == 0)
            {
                _animatorParamLookup ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _animatorParamLookup.Clear();
                foreach (var p in _animator.parameters)
                    _animatorParamLookup[p.name] = p.name;
            }

            return _animatorParamLookup.TryGetValue(name, out var actual) ? actual : null;
        }

        /// <summary>
        /// Set run animation as current.
        /// </summary>
        /// <param name="name">Run animation name.</param>
        public void SetRunAnimation(string name)
        {
            _currentRunAnimation = name;
        }

        /// <summary>
        /// Reset run animation to default.
        /// </summary>
        public void ResetRunAnimation()
        {
            _currentRunAnimation = null;
        }

        public void EnableSpeedBoosterEffect(bool enable)
        {
            if(_speedBoosterEffect != null)
            {
                _speedBoosterEffect.SetActive(enable);
            }
        }

        public void SetBoosterInteractionSpeed(float interactionSpeed)
        {
            InteractionSpeed = interactionSpeed;
            if (CurrentTaskObject != null)
            {
                CurrentTaskObject.InteractionSpeed = interactionSpeed;
                PlayAnimation(_currentAnimation);
            }
        }
        
        public void AddAccelerationZone(AccelerationZone zone)
        {
            _activeAccelerationZones.Add(zone);
            SetSpeed(CurrentSpeed, false); 
        }

        public void RemoveAccelerationZone(AccelerationZone zone)
        {
            _activeAccelerationZones.Remove(zone);
            SetSpeed(CurrentSpeed, false);
        }
    }
}
