using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.GridPathBlocking;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Audio;
using Creobit.Logger;

#if TIME_MANAGER_INACTIVE_OBJECT
using _8floor.TimeManagement.Extensions.Inactive.Core.Settings;
#endif
using Cysharp.Threading.Tasks;
using Pathfinding;
using R3;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using VContainer;

#if TIME_MANAGER_INACTIVE_OBJECT
#endif

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    [RequireComponent(typeof(Collider))]
    public abstract class ObjectView : MonoBehaviour, ITaskObject, IPausable
#if TIME_MANAGER_INACTIVE_OBJECT
        ,ICanBeInactive
#endif
    {
        
        #region EXTENSIONS_INACTIVE
#if TIME_MANAGER_INACTIVE_OBJECT
        private InactiveObjectSettings _inactiveObjectSettings;
        [field: SerializeField, ReadOnly]
        public short Active { get; set; }
        public void SetInactiveSettings(InactiveObjectSettings inactiveObjectSettings)
        {
            _inactiveObjectSettings = inactiveObjectSettings;
        }
        private bool IsActive()
        {
            return Active >= 0;
        }
#endif
        #endregion
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onPrimaryAction;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onSecondaryActionStart;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onSecondaryActionEnd;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] protected UltEvent _onStartInteract;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] public UltEvent onEndInteract;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onInteractAmountEnd;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] public UltEvent onRegisterTask;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent<ObjectView> _onDestroyedAfterFinalInteraction;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onPause;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onUnpause;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [SerializeField] protected bool _useInteractionGraphNode;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [SerializeField] protected Vector2 _gridBlockSize;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [SerializeField] protected Vector2 _gridBlockOffset;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [SerializeField]
        public Vector3 localBaseTooltipOffset;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [SerializeField]
        public Vector3 localResourcesTooltipOffset;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [SerializeField] public Vector3 localPathTooltipOffset;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [SerializeField] public Vector2 alternativeUIOffsetOverride = Vector2.zero;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [SerializeField] public bool overrideAlternativeUIScale = false;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [ShowIf(nameof(overrideAlternativeUIScale))]
        [SerializeField] public float alternativeUIScaleOverride = 1f;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [ShowIf("@_useInteractionGraphNode")]
        [SerializeField] protected uint _defaultNodePenalty;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [ShowIf("@_useInteractionGraphNode && ObjectDataSO.BlocksPath")]
        [SerializeField] protected uint _blockedNodePenalty;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [ShowIf(nameof(_useInteractionGraphNode))]
        [SerializeField] protected bool _useNearestInteractionGraphNode;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [HideIf("@_useNearestInteractionGraphNode || !_useInteractionGraphNode")]
        [SerializeField] protected Transform _interactionGraphNodeTransform;
        
        private const float GridBlockDepth = 50f;

        protected IAudioService AudioController;
        private IPauseController _pauseController;
        private IGameResourcesSystem _gameResourcesSystem;

        public CancellationTokenSource _cancellationTokenSource = new();
        public bool showTooltip;

        public virtual ObjectDataSO ObjectDataSO { get; }
        public MovableObjectTaskView CurrentMovableObjectTaskView { get; set; }
        public MovableObjectTask CurrentTask { get; set; }
        public Action<ObjectView> DestroyedAfterFinalInteraction { get; internal set; }
        public ObjectViewInteractionType InteractionType { get => ObjectDataSO.InteractionType; set => InteractionType = value; }
        public float InteractionSpeed { get; set; } = 1f;
        public int ActiveAlternativeIndex { get; set; } = -1;

        /// <summary>
        /// По объекту зарегистрирована задача — ресурсы за неё УЖЕ списаны, — а взаимодействие
        /// ещё не закончилось. На это время тултип объекта прячется: он отвечает на вопрос
        /// «что будет, если нажму», а ответ сейчас — «уже идёт». Без этого он пересобирается
        /// по изменению ресурсов и красит стоимость красным «не хватает», хотя плата внесена
        /// и юнит уже в пути — на складе её просто больше нет.
        ///
        /// Снимается по КОНЦУ ВЗАИМОДЕЙСТВИЯ, а не по завершению задачи: юнит после работы
        /// ещё идёт домой (а с добычей — и вовсе долго), но объект к этому моменту уже свободен,
        /// по нему можно кликать снова, и тултип обязан вернуться.
        /// </summary>
        public bool InteractionPending { get; private set; }
        public bool isBlocking { get; set; }
        public bool IsUsing { get; set; }
        public bool IsInactiveBlocked { get; set; } = false;
        public bool CanReactToPrimaryAction { get; private set; }
        public bool AlwaysReactToPrimaryAction { get; set; }
        public bool CanReactToSecondaryAction { get; private set; }
        public bool CanInteract { get; set; }
        
        private List<MovableObjectView> _unitsInObjectView = new();
        
        public GraphNode InteractionGraphNode => _interactionGraphNode;
        public UniqueBadgesData UniqueBadgesData => ObjectDataSO.UniqueBadgesData;
        public Vector3 Position => transform.position;
        public Vector3 BadgePosition => Position + UniqueBadgesData.TaskBadgeOffset;
        public float InteractionOffset => ObjectDataSO.InteractionOffset;
        public float InteractionTime => GetCurrentInteractionTime();

        public bool IsFree { get; private set; } = true;

        public bool IsPaused => _isPaused;

        public List<ObjectView> _requiredObjectsToInteract = new();
        public readonly List<UnitTypeCount> CurrentUnitInteractCount = new();
        public Vector2 interactionPosition;
        public bool interactionStarted;
        public bool resourcesSubtracted;
        public int isRegisteringTask;
        
        protected GraphNode _interactionGraphNode;
        protected readonly GridAreaBlock GridAreaBlock = new();
        protected bool _isPaused;
        private readonly List<MovableObjectView> _unitsCameToObject = new();
        private List<int> _alternativeCounters = new();
        private bool _interactionAudioPlaying;
        private int _currentInteractionsAmount;
        private bool _allAlternativesBlocked = false;
        
        protected IObjectViewController ObjectViewController;
        
        public event Action<ObjectView> OnShowTooltip;

        /// <summary>
        /// Курсор ушёл с объекта. Аргумент добавлен намеренно: подписчику (тултипу) нужно
        /// отличать «увели курсор с МЕНЯ» от «увели с соседнего объекта» — без этого он гасил
        /// подсказку по чужому событию и не мог её вернуть.
        /// </summary>
        public event Action<ObjectView> OnHideTooltip;

        public event Action<ObjectView, MovableObjectView> OnRegisterTask;
        public event Action<ObjectView> OnStartInteract;
        public event Action<ObjectView> OnEndInteract;

        /// <summary>
        /// «Пора выдать выходные ресурсы»: стреляет ДО освобождения юнитов, пока
        /// <see cref="CurrentTask"/> ещё жива и получателю есть куда положить груз.
        ///
        /// Отдельно от <see cref="OnEndInteract"/>, потому что тот обязан оставаться ПОСЛЕ
        /// EndInteractEventInvoke и ApplyFinalInteractionEffects: на этот порядок завязаны
        /// RelocatingObstacleService (определяет расчистку по снятому CanInteract) и
        /// перерисовка бейджей (должна видеть уже обновлённый список задач).
        ///
        /// Освобождение юнита синхронно доводит задачу до RunHome, а если юниту идти некуда
        /// (объект вплотную к базе — MoveToHomePoint уходит в ветку «уже дома»), то и до
        /// CompleteTask, который обнуляет CurrentTask. Выдавать после этого уже некому.
        /// </summary>
        public event Action<ObjectView> OnProvideOutputResources;

        /// <summary>
        /// «У объекта изменилось то, что показывают тултип и панель выбора альтернативы»:
        /// потрачена альтернатива, сбросился ActiveAlternativeIndex, объект стал недоступен.
        ///
        /// Отдельно от <see cref="OnEndInteract"/>, потому что тот стреляет ПОСРЕДИ мутаций
        /// (ApplyFinalInteractionEffects уже отработал, а ResetAlternative — ещё нет), и его
        /// подписчики видят объект в промежуточном состоянии. Это событие идёт последним,
        /// когда все поля уже согласованы.
        /// </summary>
        public event Action<ObjectView> OnInteractionStateChanged;


        [Inject]
        private void Construct(
            IPauseController pauseController,
            IGameResourcesSystem gameResourcesSystem,
            IAudioService audioController,
            IObjectViewController objectViewController)
        {
            ObjectViewController  = objectViewController;
            _pauseController = pauseController;
            _gameResourcesSystem = gameResourcesSystem;
            AudioController = audioController;
        }

        public virtual UniTask Load()
        {
            ObjectViewController.AddObjectView(this);
            CanReactToPrimaryAction = ObjectDataSO.CanReactToPrimaryAction;
            CanReactToSecondaryAction = ObjectDataSO.CanReactToSecondaryAction;
            CanInteract = ObjectDataSO.CanInteract;
            
            CanInteract = ObjectDataSO.CanInteract;
            showTooltip = ObjectDataSO.ShowTooltip;

            _pauseController.IsPaused.Subscribe(OnIsPausedChanged).AddTo(this);
            
            _currentInteractionsAmount = 0;
            _alternativeCounters.Clear();
            _allAlternativesBlocked = false;
            foreach (var alt in ObjectDataSO.AlternativeInteractions)
            {
                _alternativeCounters.Add(0);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Dispose()
        {
            _cancellationTokenSource.Cancel();

            return UniTask.CompletedTask;
        }

        public void SetPrimaryActionState(bool canReact)
        {
            CanReactToPrimaryAction = canReact;
        }

        public void SetSecondaryActionState(bool canReact)
        {
            CanReactToSecondaryAction = canReact;
        }

        public virtual async UniTask Interact(MovableObjectView unit = null)
        {
#if TIME_MANAGER_INACTIVE_OBJECT
            if (!IsActive() && _inactiveObjectSettings.BlockInteraction)
            {
                return;
            }
#endif
            while (interactionStarted)
            {
                // The object is busy with a running interaction (another task won the race
                // this frame). Wait for it to finish and re-evaluate instead of dropping the
                // unit: a silently dropped unit stays in Work at the object forever.
                var destroyed = await UniTask.WaitUntil(() => !interactionStarted,
                        cancellationToken: this.GetCancellationTokenOnDestroy())
                    .SuppressCancellationThrow();

                if (destroyed)
                {
                    return;
                }
            }

            // The unit's task may have been cancelled or redirected while it waited:
            // its arrival is void then, don't register it into the interaction lists.
            if (unit != null && !ReferenceEquals(unit.CurrentTaskObject, this))
            {
                return;
            }

            if (!CanInteract)
            {
                ReleaseArrivedUnits(unit);
                return;
            }

            if (GetCurrentUnitTypeCount().Count == 0)
            {
                await StartInteract();
                return;
            }

            _unitsInObjectView.Add(unit);
            
            if (CurrentUnitInteractCount.Count == 0)
            {
                foreach (var typeCount in GetCurrentUnitTypeCount())
                {
                    var typeCountCopy = new UnitTypeCount
                    {
                        count = typeCount.count,
                        tagMode = typeCount.tagMode,
                        unitType = typeCount.unitType
                    };
                    typeCountCopy.count = 0;
                    CurrentUnitInteractCount.Add(typeCountCopy);
                }
            }
            
            if (!_unitsCameToObject.Contains(unit))
                _unitsCameToObject.Add(unit);

            foreach (var unitTypeCount in CurrentUnitInteractCount)
            {
                unitTypeCount.count = _unitsCameToObject
                    .Count(unitCame => unitTypeCount.unitType
                        .All(unitTag => unitCame.MovableObjectDataSO.ObjectTypeTags
                            .Contains(unitTag)));
            }

            var canInteract = true;

            for (var index = 0; index < GetCurrentUnitTypeCount().Count; index++)
            {
                var typeCountOrigin = GetCurrentUnitTypeCount()[index];
                var typeCountCurrent = CurrentUnitInteractCount[index];

                if (typeCountCurrent.count < typeCountOrigin.count)
                {
                    canInteract = false;
                    break;
                }
            }

            if (canInteract)
            {
                await StartInteract();
            }
        }

        public void SetIsFree(bool state)
        {
            IsFree = state;
        }

        /// <summary>
        /// Releases the arriving unit and any units already registered and waiting when the
        /// object can no longer be interacted with (consumed/deactivated while they walked).
        /// Without this they keep State == Work forever and their tasks never complete.
        /// </summary>
        private void ReleaseArrivedUnits(MovableObjectView arrivedUnit)
        {
            foreach (var waitingUnit in _unitsInObjectView.ToList())
            {
                if (waitingUnit != null && waitingUnit != arrivedUnit)
                {
                    waitingUnit.EndUnitInteract();
                }
            }

            _unitsInObjectView.Clear();
            _unitsCameToObject.Clear();
            CurrentUnitInteractCount.Clear();

            if (arrivedUnit != null)
            {
                arrivedUnit.EndUnitInteract();
            }
        }

        /// <summary>
        /// Resets transient interaction state (busy flags and per-interaction unit arrival lists).
        /// Must be called whenever an interaction is cancelled, otherwise the object stays
        /// "busy" forever and units of the next task wait near it indefinitely.
        /// </summary>
        public void ResetInteractionState()
        {
            interactionStarted = false;
            IsUsing = false;
            InteractionPending = false;
            SetIsFree(true);
            _unitsInObjectView.Clear();
            _unitsCameToObject.Clear();
            CurrentUnitInteractCount.Clear();

            if (CurrentMovableObjectTaskView != null)
            {
                CurrentMovableObjectTaskView.gameObject.SetActive(false);
                CurrentMovableObjectTaskView = null;
            }
        }
        
        private async UniTask StartInteract()
        {
            if (interactionStarted)
                return;

            SetIsFree(false);
            
            interactionStarted = true;
           // _currentInteractionsAmount++; to end interact
            
            foreach (var unit in CurrentTask.Units)
            {
                if (InteractionTime > 0.01f)
                {
                    unit.PlayAnimation(InteractionType.ToString());
                }
            }

            OnInteractionStart();
            if (CurrentTask.Units.Any(x => x.ObjectDataSO.ObjectTypeTags.Any(y=>y is ModifiedGameplayTagSO)))
            {
                var modifiedGameplayTag =
                    CurrentTask.Units.Select(x => x.ObjectDataSO.ObjectTypeTags.First(y => y is ModifiedGameplayTagSO))
                        .First() as ModifiedGameplayTagSO;

                if (modifiedGameplayTag!=null)
                {
                    await AwaitInteractionTask(InteractionTime * modifiedGameplayTag.InteractionModificator);
                    return;
                }
            }

            await AwaitInteractionTask(ObjectDataSO.InteractionTime);
        }

        /// <summary>
        /// Try register task on object.
        /// </summary>
        /// <returns>True if register successful, otherwise false.</returns>
        public void TryRegisterTask()
        {
            TryInvokeRegisterTask(null);
        }

        public void TryRegisterTask(MovableObjectView specificUnit)
        {
            TryInvokeRegisterTask(specificUnit);
        }

        private void TryInvokeRegisterTask(MovableObjectView specificUnit)
        {
            var ownsRegistrationLock = Interlocked.CompareExchange(ref isRegisteringTask, 1, 0) == 0;
            if (!ownsRegistrationLock && CurrentTask == null)
            {
                return;
            }

            if (OnRegisterTask == null)
            {
                if (ownsRegistrationLock)
                {
                    Interlocked.Exchange(ref isRegisteringTask, 0);
                }

                return;
            }

            try
            {
                OnRegisterTask.Invoke(this, specificUnit);
            }
            catch
            {
                if (ownsRegistrationLock)
                {
                    Interlocked.Exchange(ref isRegisteringTask, 0);
                }

                throw;
            }
        }

        /// <summary>
        /// Check if is enough resources for interact
        /// </summary>
        /// <returns>True is is enough resources, otherwise false.</returns>
        public bool IsEnoughResources()
        {
            if (!GetCurrentInteractionNeedInputResources() ||
                _gameResourcesSystem.IsEnoughResources(GetCurrentInputResources()))
            {
                return true;
            }

            Log.Gameplay.Info("Not enough resources");
            return false;
        }

        public virtual void OnPrimaryAction()
        {
#if TIME_MANAGER_INACTIVE_OBJECT
            if (!IsActive() && _inactiveObjectSettings.BlockPrimaryAction)
            {
                return;
            }
#endif
            _onPrimaryAction?.Invoke();
            Log.Gameplay.Info("Click " + name);
        }

        public virtual void OnSecondaryActionStart()
        {
#if TIME_MANAGER_INACTIVE_OBJECT
            if (!IsActive() && _inactiveObjectSettings.BlockSecondaryAction)
            {
                return;
            }
#endif
            _onSecondaryActionStart?.Invoke();
            Log.Gameplay.Info("HoverStart " + name);
        }

        public virtual void OnSecondaryActionEnd()
        {
            // The input system keeps a plain interface reference to the hovered object and
            // calls this on cursor move even after the object was destroyed — that spammed
            // MissingReferenceException on every frame and buried real errors.
            if (!this)
            {
                return;
            }

#if TIME_MANAGER_INACTIVE_OBJECT
            if (!IsActive() && _inactiveObjectSettings.BlockSecondaryAction)
            {
                return;
            }
#endif
            _onSecondaryActionEnd?.Invoke();
            Log.Gameplay.Info("HoverEnd " + name);
        }

        public virtual void Pause()
        {
            Log.Gameplay.Info($"Pause {name}");
            _isPaused = true;
            _onPause?.Invoke();
        }

        public virtual void Unpause()
        {
            Log.Gameplay.Info($"Unpause {name}");
            _isPaused = false;
            _onUnpause.Invoke();
        }

        private async UniTask AwaitInteractionTask(float duration)
        {
            if (GetCurrentUnitTypeCount().Count > 0)
            {
                try
                {
                    _cancellationTokenSource = new CancellationTokenSource();

                    float startTime = Time.time;
                    float progress = 0;

                    while (progress < 1f)
                    {
                        if (_cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }
                        if (_isPaused)
                        {
                            float pauseStart = Time.time;
                            await UniTask.WaitWhile(() => _isPaused);
                            startTime += Time.time - pauseStart;
                        }
                        
                        float timePassed = (Time.time - startTime) * InteractionSpeed;
                        progress = Mathf.Clamp01(timePassed / duration);
                        
                        if (CurrentMovableObjectTaskView && ObjectDataSO.UseTaskView)
                        {
                            CurrentMovableObjectTaskView.Report(progress,duration>0.01f);
                        }

                        PlayInteractionSFX().Forget();
                        await UniTask.Yield(PlayerLoopTiming.Update, _cancellationTokenSource.Token);
                    }

                    if (CurrentMovableObjectTaskView)
                    {
                        CurrentMovableObjectTaskView.gameObject.SetActive(false);

                        CurrentMovableObjectTaskView = null;
                    }
                }
                catch (OperationCanceledException)
                {
                    _cancellationTokenSource = new CancellationTokenSource();

                    // Cancelled mid-interaction: without a reset the object keeps IsFree == false
                    // and interactionStarted == true forever, so every unit sent here afterwards
                    // waits at WaitUntil(IsFree) and never interacts.
                    ResetInteractionState();

                    return;
                }
            }

            interactionStarted = false;

            OnInteractionEnd();
        }

        public async UniTask PlayInteractionSFX()
        {
            if (!ObjectDataSO.Interacting) return;
            if (_interactionAudioPlaying) return;

            AudioController.PlaySfx(ObjectDataSO.Interacting);

            _interactionAudioPlaying = true;

            var timeToWait = ObjectDataSO.Interacting.length;

            // Guard against zero/negative multipliers (e.g. InteractingFrequencyMultiplier = 0 in data),
            // which produce Infinity and crash UniTask.Delay with a negative TimeSpan.
            if (ObjectDataSO.InteractingFrequencyMultiplier > 0f)
                timeToWait /= ObjectDataSO.InteractingFrequencyMultiplier;

            if (InteractionSpeed > 0f)
                timeToWait /= InteractionSpeed;

            if (timeToWait > 0f && !float.IsInfinity(timeToWait) && !float.IsNaN(timeToWait))
                await UniTask.WaitForSeconds(timeToWait);

            _interactionAudioPlaying = false;
        }

        protected virtual void OnInteractionEnd()
        {
            IsUsing = false;
            SetIsFree(true);

            if (ActiveAlternativeIndex >= 0)
            {
                _alternativeCounters[ActiveAlternativeIndex]++;
                
                var currentAlt = ObjectDataSO.AlternativeInteractions[ActiveAlternativeIndex];
                if (currentAlt.DeactivateAllOthers && IsFinalInteraction())
                {
                    _allAlternativesBlocked = true;
                }
            }
            else
            {
                _currentInteractionsAmount++;
            }
            

            AudioController.PlaySfx(ObjectDataSO.InteractionEnd);

            // Груз юнитам выдаём ДО их освобождения: EndInteractEventInvoke ниже синхронно
            // доводит задачу до RunHome, а для объекта вплотную к базе — сразу до CompleteTask,
            // который обнуляет CurrentTask. Выданное после этого просто теряется.
            OnProvideOutputResources?.Invoke(this);

            EndInteractEventInvoke();
            
            bool isCurrentTaskFinished = IsFinalInteraction(); 
            
            bool shouldDeactivateObject = (isCurrentTaskFinished && ObjectDataSO.DeactivateAfterFinalInteraction) 
                                          || !HasAnyAvailableAlternatives();

            ApplyFinalInteractionEffects(shouldDeactivateObject);
    
            OnEndInteract?.Invoke(this);

            if (ActiveAlternativeIndex >= 0)
            {
                InvokeAlternativeEvent(ActiveAlternativeIndex);
            }
    
            if (isCurrentTaskFinished)
            {
                ResetAlternative();
            }

            // Взаимодействие закончилось — объект снова свободен, тултипу пора вернуться.
            // Не ждём завершения задачи: юнит после работы ещё идёт домой, но кликать
            // по объекту уже можно.
            InteractionPending = false;

            // Последним: к этому моменту счётчики альтернатив, CanInteract и ActiveAlternativeIndex
            // уже приведены в согласованное состояние, и подписчик может честно пересобрать UI.
            NotifyInteractionStateChanged();
        }

        /// <summary>
        /// Сообщить наружу, что состояние объекта изменилось и завязанный на него UI
        /// (тултип, панель выбора альтернативы) надо пересобрать. Публичный, потому что
        /// менять состояние объекта могут и снаружи: COC при достройке стадии, сервисы кражи,
        /// уровневые UltEvent'ы.
        /// </summary>
        public void NotifyInteractionStateChanged()
        {
            OnInteractionStateChanged?.Invoke(this);
        }

        /// <summary>
        /// Отметить, что задача по объекту зарегистрирована (или что этот этап закончился).
        /// Сразу сообщает наружу: тултип должен уйти с экрана в момент клика, а не когда
        /// что-нибудь постороннее дёрнет обновление.
        /// </summary>
        public void SetInteractionPending(bool value)
        {
            if (InteractionPending == value)
            {
                return;
            }

            InteractionPending = value;

            NotifyInteractionStateChanged();
        }

        public bool IsFinalInteraction() 
        {
            if (ActiveAlternativeIndex >= 0 && ActiveAlternativeIndex < _alternativeCounters.Count)
            {
                var alt = ObjectDataSO.AlternativeInteractions[ActiveAlternativeIndex];
                return _alternativeCounters[ActiveAlternativeIndex] >= alt.InteractionsAmount;
            }

            if (ObjectDataSO.AlternativeInteractions.Count > 0)
            {
                return !HasAnyAvailableAlternatives();
            }

            return ObjectDataSO.InteractionsAmount != 0 && _currentInteractionsAmount >= ObjectDataSO.InteractionsAmount;
        }

        public void ShowTooltip()
        {
            OnShowTooltip?.Invoke(this);        
        }

        public void HideTooltip()
        {
            OnHideTooltip?.Invoke(this);
        }

        private void OnIsPausedChanged(bool isPaused)
        {
            if (isPaused)
            {
                Pause();
            }
            else
            {
                Unpause();
            }
        }

        private void OnInteractionStart()
        {
            _onStartInteract?.Invoke();
            OnStartInteract?.Invoke(this);
        }

        private void EndInteractEventInvoke()
        {
            if (GetCurrentUnitTypeCount().Count > 0)
            {
                foreach (var taskUnit in _unitsInObjectView)
                {
                    if (ObjectDataSO.MovableObjectHideOnInteract)
                    {
                        taskUnit.modelTransform.gameObject.SetActive(true);
                    }
                    taskUnit.EndUnitInteract(ObjectDataSO.InteractionHaveOutputResources);
                }

                _unitsInObjectView.Clear();
                // Units must physically arrive again for the next interaction. A stale arrival
                // list makes the next interaction start while its units are still on the way
                // (and breaks the count when the active alternative changes).
                _unitsCameToObject.Clear();
                CurrentUnitInteractCount.Clear();
            }
            
            onEndInteract?.Invoke();
        }

        private void ApplyFinalInteractionEffects(bool isFinalInteraction)
        {
            if (!isFinalInteraction)
            {
                return;
            }

            _onInteractAmountEnd?.Invoke();

            if (ObjectDataSO.DeactivateAfterFinalInteraction)
            {
                _onDestroyedAfterFinalInteraction?.Invoke(this);
                DestroyedAfterFinalInteraction?.Invoke(this);

                // Keep the graph node's Blocked counter and Penalty in sync: InitGraphNode
                // raised BOTH together, so both must drop together here — otherwise the node
                // stays Blocked>=0 (task validation treats it as a closed road) while its
                // Penalty is cleared (the units' seeker treats it as open), and the two
                // pathfinders disagree ("unit walks a road the game still shows as blocked").
                // Guarded by isBlocking so it runs at most once: if the object was already
                // unblocked (e.g. _onPrimaryAction -> UnBlockInteractionGraphNode), we must
                // NOT subtract the penalty a second time (that double-subtract is what left
                // shared nodes desynced).
                GridAreaBlock.Release();

                if (_useInteractionGraphNode && isBlocking)
                {
                    isBlocking = false;
                    _interactionGraphNode.Blocked--;
                    var penalty = (int)(_interactionGraphNode.Penalty - _blockedNodePenalty);
                    _interactionGraphNode.Penalty = penalty >= 0 ? (uint)penalty : 0;
                }

                _currentInteractionsAmount = 0;
                CanInteract = false;
                gameObject.SetActive(false);
                return; 
            }

            if (ObjectDataSO.AlternativeInteractions.Count > 0)
            {
                if (!HasAnyAvailableAlternatives())
                {
                    CanInteract = false;
                }
            }
            else
            {
                CanInteract = false;
            }
        }

        public void ResetAlternative() => ActiveAlternativeIndex = -1;

        public void SelectAlternative(int index)
        {
            ActiveAlternativeIndex = index;

            InvokeAlternativePrimaryAction(index);
        }

        /// <summary>
        /// Сбрасывает счётчик выполненных взаимодействий — объект снова можно использовать.
        ///
        /// Нужен тем объектам, которые переиспользуются БЕЗ гашения: движок обнуляет счётчик
        /// только в ветке DeactivateAfterFinalInteraction, а пока IsFinalInteraction() истинно,
        /// ObjectViewController отказывает в регистрации новой задачи, и объект перестаёт
        /// кликаться, хотя выглядит рабочим. Повторяет ту же часть сброса, что делает Load().
        ///
        /// Существующее поведение не затрагивает: метод новый, сам себя никто не вызывает.
        /// </summary>
        public void ResetInteractionsCount()
        {
            _currentInteractionsAmount = 0;
            _allAlternativesBlocked = false;

            _alternativeCounters.Clear();

            foreach (var alternative in ObjectDataSO.AlternativeInteractions)
            {
                _alternativeCounters.Add(0);
            }
        }
            
            
        public ResourceAmount[] GetCurrentInputResources()
        {
            if (ActiveAlternativeIndex >= 0 && ActiveAlternativeIndex < ObjectDataSO.AlternativeInteractions.Count)
                return ObjectDataSO.AlternativeInteractions[ActiveAlternativeIndex].InputResources ?? Array.Empty<ResourceAmount>();
            return ObjectDataSO.InputResources ?? Array.Empty<ResourceAmount>();
        }

        public List<UnitTypeCount> GetCurrentUnitTypeCount()
        {
            if (ActiveAlternativeIndex >= 0 && ActiveAlternativeIndex < ObjectDataSO.AlternativeInteractions.Count)
                return ObjectDataSO.AlternativeInteractions[ActiveAlternativeIndex].UnitTypeCount ?? new List<UnitTypeCount>();
            return ObjectDataSO.UnitTypeCount ?? new List<UnitTypeCount>();
        }

        public float GetCurrentInteractionTime()
        {
            if (ActiveAlternativeIndex >= 0 && ActiveAlternativeIndex < ObjectDataSO.AlternativeInteractions.Count)
                return ObjectDataSO.AlternativeInteractions[ActiveAlternativeIndex].InteractionTime;
            return ObjectDataSO.InteractionTime;
        }

        public bool GetCurrentInteractionNeedInputResources()
        {
            if (ActiveAlternativeIndex >= 0 && ActiveAlternativeIndex < ObjectDataSO.AlternativeInteractions.Count)
                return ObjectDataSO.AlternativeInteractions[ActiveAlternativeIndex].InputResources != null && 
                       ObjectDataSO.AlternativeInteractions[ActiveAlternativeIndex].InputResources.Length > 0;
            return ObjectDataSO.InteractionNeedInputResources;
        }
        
        public bool CanUseAlternative(int index)
        {
            if (_allAlternativesBlocked) return false;
            
            if (index < 0 || index >= ObjectDataSO.AlternativeInteractions.Count) return false;

            // Счётчики создаются в Initialize по ИСХОДНОМУ ObjectDataSO и при подмене _dataSO
            // (уровни делают её через UltEvent на onEndInteract) не пересоздаются. Если в новом
            // ассете альтернатив больше, чем было в старом, индекс выходит за список счётчиков.
            // Такая альтернатива ещё ни разу не использовалась, значит она доступна.
            if (index >= _alternativeCounters.Count) return true;

            var alt = ObjectDataSO.AlternativeInteractions[index];
            return alt.InteractionsAmount == 0 || _alternativeCounters[index] < alt.InteractionsAmount;
        }

        public bool HasAnyAvailableAlternatives()
        {
            if (ObjectDataSO.AlternativeInteractions.Count == 0) return !IsFinalInteraction();
    
            for (int i = 0; i < ObjectDataSO.AlternativeInteractions.Count; i++)
            {
                if (CanUseAlternative(i)) return true;
            }
            return false;
        }
        
        protected virtual void InvokeAlternativeEvent(int index) { }

        protected virtual void InvokeAlternativePrimaryAction(int index) { }
        
        public bool BlocksGridArea => _gridBlockSize.x > 0f && _gridBlockSize.y > 0f;

        public Bounds GetGridBlockBounds()
        {
            var center = transform.position + new Vector3(_gridBlockOffset.x, _gridBlockOffset.y, 0f);

            return new Bounds(center, new Vector3(_gridBlockSize.x, _gridBlockSize.y, GridBlockDepth));
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            var basePosition = BadgePosition;

            var bottomLeft = basePosition + (Vector3.down + Vector3.left) * 0.5f;
            var bottomRight = basePosition + (Vector3.down + Vector3.right) * 0.5f;
            var topLeft = basePosition + (Vector3.up + Vector3.left) * 0.5f;
            var topRight = basePosition + (Vector3.up + Vector3.right) * 0.5f;

            Gizmos.color = Color.red;

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomLeft, topLeft);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topLeft, topRight);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!BlocksGridArea)
            {
                return;
            }

            var blockBounds = GetGridBlockBounds();
            var blockCenter = new Vector3(blockBounds.center.x, blockBounds.center.y, transform.position.z);
            var blockSize = new Vector3(blockBounds.size.x, blockBounds.size.y, 0f);

            Gizmos.color = new Color(1f, 0.78f, 0.12f, 0.25f);
            Gizmos.DrawCube(blockCenter, blockSize);

            Gizmos.color = new Color(1f, 0.78f, 0.12f, 0.9f);
            Gizmos.DrawWireCube(blockCenter, blockSize);
        }
#endif
    }
}
