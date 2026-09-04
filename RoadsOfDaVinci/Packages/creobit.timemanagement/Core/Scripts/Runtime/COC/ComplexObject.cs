using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;


#if TIME_MANAGER_INACTIVE_OBJECT
using _8floor.TimeManagement.Extensions.Inactive.Core.Settings;
#endif
using Cysharp.Threading.Tasks;
using ObservableCollections;
using R3;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using VContainer;

#if TIME_MANAGER_INACTIVE_OBJECT
using _8floor.TimeManagement.Extensions.Inactive.Core.Controller;
using _8floor.TimeManagement.Extensions.Inactive.Core.Settings;
#endif

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC
{
    [RequireComponent(typeof(Collider))]
    public class  ComplexObject : MonoBehaviour, ITaskObject // todo restore build settings. For all objects(
    {
#if TIME_MANAGER_INACTIVE_OBJECT
		[Inject]
        private IInactiveObjectController _inactiveObjectController;
#endif
        [FoldoutGroup(RuntimeConstants.FoldoutNames.ComplexObjectControllerSettings)]
        [SerializeField] public TransitionData[] transitionStateData;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.ComplexObjectControllerSettings)]
        [SerializeField] public StaticObjectView initialObjectView;

        // Optional point a unit walks to and stands at when interacting with this object
        // (building/upgrading). Defaults to the object pivot when not set. Needed for large
        // objects (e.g. unit camps that upgrade themselves) where the pivot sits inside the
        // footprint, so a worker targeting the pivot would stand on top of the building.
        [FoldoutGroup(RuntimeConstants.FoldoutNames.ComplexObjectControllerSettings)]
        [SerializeField] private Transform _interactionAnchor;

        // How far apart simultaneous units fan out around the anchor. Multiplies the per-unit
        // spread offset; raise it if units overlap on this object, lower it if they drift into
        // decor. 1 = the default spacing used by non-anchored objects. Only used when
        // _interactionAnchor is set.
        [FoldoutGroup(RuntimeConstants.FoldoutNames.ComplexObjectControllerSettings)]
        [ShowIf("@_interactionAnchor != null")]
        [SerializeField] private float _interactionAnchorSpread = 2f;

        public Transform InteractionAnchor => _interactionAnchor;
        public float InteractionAnchorSpread => _interactionAnchorSpread;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.UpgradeMark)]
        [SerializeField]
        public bool showUpgradeMark;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.UpgradeMark)]
        [ShowIf("@_showUpgradeMark")]
        [SerializeField]
        public SpriteRenderer upgradeMarkObject;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.UpgradeMark)]
        [ShowIf("@_showUpgradeMark")]
        [SerializeField]
        public Sprite upgradeMarkEnable;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.UpgradeMark)]
        [ShowIf("@_showUpgradeMark")]
        [SerializeField]
        public Sprite upgradeMarkDisable;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UniqueBadges)]
        [field: SerializeField] public UniqueBadgesData UniqueBadgesData { get; private set; }
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onPrimaryAction;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onSecondaryActionStart;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onSecondaryActionEnd;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] public UltEvent onRegisterTask;
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ComplexObjectControllerSettings)]
        [field: SerializeField] public bool UseBuildFirst { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Effects)]
        [SerializeField] private GameObject _buildingEffect;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Effects)]
        [SerializeField] private GameObject _buildingEndedEffect;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField] public TooltipSettings TooltipSettings { get; set; }

        public Vector3 BadgePosition => transform.position + UniqueBadgesData.TaskBadgeOffset;
        public float InteractionOffset => transitionStateData[currentStateIndex].BuildingSettings.InteractionOffset;
        public ObjectViewInteractionType InteractionType => 
            transitionStateData[currentStateIndex].BuildingSettings.InteractionType;
        public float InteractionTime =>
            transitionStateData[currentStateIndex].GameplayIntervalGeneralParameters.DurationSeconds;
        public bool IsUsing => isUsing;
        public bool IsInactiveBlocked { get; set; } = false;
        public Vector3 Position => _interactionAnchor != null ? _interactionAnchor.position : transform.position;
        
        public StaticObjectView CurrentObjectView
        {
            get => (currentObjectView.productionData.SpawnedCollectibleObject != null 
                    && currentObjectView.productionData.SpawnedCollectibleObject.gameObject.activeInHierarchy)
                ? currentObjectView.productionData.SpawnedCollectibleObject
                : currentObjectView;
            
            set => currentObjectView = value;
        }
        public MovableObjectTask CurrentTask { get; set; }
        public short Active { get; set; }
        public short ActiveOnStart { get; set; }
        public bool CanReactToPrimaryAction { get; } = true;
        public bool AlwaysReactToPrimaryAction { get; set; }
        public bool CanReactToSecondaryAction { get; } = true;
        public bool IsFree { get; private set; } = true;

        public MovableObjectTaskView CurrentMovableObjectTaskView { get; set; }
        public float InteractionSpeed { get; set; } = 1f;
        
        private IComplexObjectProvider _complexObjectProvider;
        private IComplexObjectController _complexObjectController;
        
        public readonly Dictionary<ObjectView.ObjectView, int> StateIndexes = new ();
        public readonly Dictionary<CocActionType, CocAction> CocActions = new ();
        public readonly ReactiveProperty<CocActionData> ActionData = new();
        public int currentStateIndex;
        public bool buildingComplete;
        public ushort currentIntervalId;
        public readonly List<UnitTypeCount> UnitsCameToCoc = new();
        public bool buildingStarted;
        public bool isUsing;
        // Re-entry guard for ComplexObjectBuildingController.ChangeState: held for the whole
        // check-then-act (resource check -> path await -> subtract -> register) so two rapid
        // build triggers can't both pass the resource check and double-charge / double-register.
        public bool transitionInProgress;
        public StaticObjectView currentObjectView;
        public StaticObjectView nextObjectView;
        public ResourceAmount[] spentResources;
        public GameObject currentBuildingEffect;
        public CancellationTokenSource CancellationTokenSource;
        
        public event Action<ComplexObject> OnShowTooltip;

        /// <summary>
        /// Курсор ушёл с объекта. Аргумент — чтобы подписчик отличал «увели курсор с МЕНЯ»
        /// от «увели с соседнего объекта». См. ObjectView.OnHideTooltip.
        /// </summary>
        public event Action<ComplexObject> OnHideTooltip;

        /// <summary>
        /// У COC изменилась стадия/доступность — завязанный на него UI надо пересобрать.
        /// Аналог ObjectView.OnInteractionStateChanged: у COC своего OnEndInteract нет,
        /// поэтому раньше строитель гасил тултип руками.
        /// </summary>
        public event Action<ComplexObject> OnInteractionStateChanged;
        public event Action<ComplexObject> OnNotResources;
        public event Action<ComplexObject> OnNotPath;
        public event Action<ComplexObject, MovableObjectView> OnInteract;
        public event Action<ComplexObject> OnBuild;
        public event Action<ComplexObject> OnDestroy;
        public event Action<ComplexObject> OnIntervalStart;
        public event Action<ComplexObject> OnTryBuild;
        public event Action<ComplexObject, CocActionType> OnPerformAction;
        public event Action<ComplexObject, StaticObjectView, int> OnIntervalEnd;

        [Inject]
        private void Construct(
            IComplexObjectProvider complexObjectProvider,
            IComplexObjectController complexObjectController)
        {
            _complexObjectController =  complexObjectController;
            _complexObjectProvider = complexObjectProvider;
        }
        
        public UniTask Load()
        {
#if TIME_MANAGER_INACTIVE_OBJECT
			_inactiveObjectController.AddInactiveObject(this);
#endif
            _complexObjectController.AddObjectView(this);
            _complexObjectProvider.AddCoc(this);

            Initialize();
            
            return UniTask.CompletedTask;
        }

        public UniTask Dispose()
        {
#if TIME_MANAGER_INACTIVE_OBJECT
			_inactiveObjectController.RemoveInactiveObject(this);
#endif
            if (currentBuildingEffect != null) Destroy(currentBuildingEffect);
            
            return UniTask.CompletedTask;
        }

        private void Initialize()
        {
            CurrentObjectView = initialObjectView;
        }
        
        public async UniTask Interact(MovableObjectView unit = null)
        {
            OnInteract?.Invoke(this, unit);
        }

        public void IntervalStarted()
        {
            if (_buildingEffect != null) 
            {
                currentBuildingEffect = Instantiate(_buildingEffect, transform.position, Quaternion.identity);
            }
            
            OnIntervalStart?.Invoke(this);
        }

        public void IntervalCompleted(StaticObjectView objectView, int nextStateIndex)
        {
            Destroy(currentBuildingEffect);
            
            OnIntervalEnd?.Invoke(this, objectView, nextStateIndex);
        }

        public UniTask<bool> CanUse()
        {
            return UniTask.FromResult(CanBuildObject());
        }

        public void PerformAction(CocActionType cocActionType)
        {
            OnPerformAction?.Invoke(this, cocActionType);
        }

        public void InteractWithActiveObject()
        {
            onRegisterTask?.Invoke();
            
            if (!CurrentObjectView.gameObject.activeSelf
                && CurrentObjectView.productionData.SpawnedCollectibleObject != null)
            {
                CurrentObjectView.productionData.SpawnedCollectibleObject.OnPrimaryAction();
            }
            else
            {
                CurrentObjectView.OnPrimaryAction();
            }
        }
        /// <summary>
        /// Default primary action to COC. If only build action available -- register/cancel build task,
        /// otherwise it opens the action selection panel.
        /// </summary>
        public void TryBuild()
        {
            OnTryBuild?.Invoke(this);
        }

        public void BuildObject()
        {
            OnBuild?.Invoke(this);
        }
        
        public async UniTask DestroyObject()
        {
            OnDestroy?.Invoke(this);
        }

        public void OnPrimaryAction()
        {
            _onPrimaryAction?.Invoke();

            if (currentStateIndex >= 0 && currentStateIndex < transitionStateData.Length)
            {
                transitionStateData[currentStateIndex].OnPrimaryAction?.Invoke();
            }
        }

        public void OnSecondaryActionStart()
        {
            _onSecondaryActionStart?.Invoke();
        }

        public void OnSecondaryActionEnd()
        {
            _onSecondaryActionEnd?.Invoke();
        }

        public ResourceAmount[] GetResourceCost()
        {
            return transitionStateData[currentStateIndex].CostsData;
        }

        public bool CanBuildObject()
        {
            return currentStateIndex < transitionStateData.Length - 1
                   && transitionStateData[currentStateIndex].TransitionTo != null;
        }

        public bool CanDestroyObject()
        {
            return transitionStateData[currentStateIndex].BuildingSettings.CanBeReturnedToInitialState;
        }
        
        public void ShowTooltip()
        {
            OnShowTooltip?.Invoke(this);
        }

        public void HideTooltip()
        {
            OnHideTooltip?.Invoke(this);
        }

        public void NotifyNotResources()
        {
            OnNotResources?.Invoke(this);
        }

        public void NotifyNoPath()
        {
            OnNotPath?.Invoke(this);
        }

        /// <summary>
        /// Сообщить наружу, что стадия/доступность COC изменились и завязанный на него UI
        /// надо пересобрать.
        /// </summary>
        public void NotifyInteractionStateChanged()
        {
            OnInteractionStateChanged?.Invoke(this);
        }

        /// <summary>
        /// Стройка заказана — ресурсы за неё уже списаны, стадия ещё не сменилась.
        /// На это время тултип прячется, иначе он покажет «не хватает на стадию 2», хотя
        /// стадия 2 в этот момент и строится. См. ObjectView.InteractionPending.
        /// </summary>
        public bool InteractionPending { get; private set; }

        public void SetInteractionPending(bool value)
        {
            if (InteractionPending == value)
            {
                return;
            }

            InteractionPending = value;

            NotifyInteractionStateChanged();
        }

#if TIME_MANAGER_INACTIVE_OBJECT
		public void SetInactiveSettings(InactiveObjectSettings inactiveObjectSettings)
        {
            
        }
#endif
    }
}