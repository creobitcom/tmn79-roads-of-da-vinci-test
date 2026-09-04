using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production.Repair;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using Pathfinding;
using R3;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    public class StaticObjectView : ObjectView
    {
        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [SerializeField] public UltEvent OnBlockPathHighlight;
 
        [FoldoutGroup(RuntimeConstants.FoldoutNames.PathSettings)]
        [SerializeField] public UltEvent OnVisualReset;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent<MovableObjectView> OnSpecialUnitsEndBuild;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] private UltEvent _onStealed;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] public UltEvent<MovableObjectView> OnSpecialUnitsEndInteract;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public List<MovableObjectView> SpecialUnits { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] public List<UltEvent> AlternativeEndInteracts;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [SerializeField] public List<UltEvent> AlternativePrimaryActions;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.BaseData)]
        [field: SerializeField] public bool IsBase { get; private set; }

        [field: ShowIf(nameof(IsBase))]
        [field: SerializeField] public BaseData BaseData { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ProductionInfo)]
        [field: ToggleLeft]
        [field: Tooltip("Determines if the object can produce other ObjectView")]
        [field: SerializeField] public bool CanProduceObjects { get; private set; }

        [FoldoutGroup(RuntimeConstants.FoldoutNames.ProductionInfo)]
        [ShowIf(nameof(CanProduceObjects))] public ProductionData productionData;

        [FoldoutGroup(RuntimeConstants.FoldoutNames.ProductionInfo)]
        [ShowIf(nameof(CanProduceObjects))]
        [SerializeField]
        [LabelText("Working Animation")]
        private ProductionWorkingAnimationSettings _productionWorkingAnimation = new();

        [FoldoutGroup(RuntimeConstants.FoldoutNames.ProductionInfo)]
        [ShowIf(nameof(CanProduceObjects))]
        [SerializeField]
        [LabelText("Production Progress View")]
        [Tooltip("Optional circular progress view. Leave empty to keep production progress hidden.")]
        private MovableObjectTaskView _productionProgressView;

        /// <summary>
        /// Картинка «что даст действие» для карточки тултипа (рамка сбоку).
        /// Лежит на ВЬЮХЕ, а не в TooltipSettings: TooltipSettings — часть ObjectDataSO,
        /// один ассет на все раскопки во всех уровнях, а настройка нужна на каждом уровне своя.
        /// Здесь же значение сериализуется на инстансе и уезжает в префаб уровня.
        /// Пусто — блок рамки в карточке не показывается.
        /// </summary>
        [FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [PreviewField]
        [SerializeField] public Sprite tooltipPreviewIcon;

        [SerializeField] private StaticObjectDataSO _dataSO;
        
        private IObjectResolver _resolver;
        

        public bool NeedStopBrake => _repairStationView != null && _repairStationView.gameObject.activeSelf;
        public bool IsBroke => _currentBreakableView! != null && _currentBreakableView.gameObject.activeInHierarchy;
        public bool IsRepaired { get; set; }
        public bool NeedBreak
        {
            get => !NeedStopBrake && _needBreak;
            set
            {
                if (!NeedStopBrake)
                {
                    _needBreak = value;
                }
            }
        }

        public override ObjectDataSO ObjectDataSO => _dataSO;

        // Реально активирована ли поломка для ЭТОГО объекта (дизайнерский чекбокс productionData.CanBeBroken
        // ДА и на уровне нашёлся тег ремонтной мастерской) — раньше это же смысловое значение по ошибке
        // писалось прямо в CanBeBroken (см. BreakableController.Initialize), затирая то, что стоит в инспекторе.
        public bool breakableActivated;
        public ObjectView _repairStationView;
        public ObjectView _currentBreakableView;
        // Раньше кэшировались как общие поля BreakableController — один на весь уровень, а не на объект,
        // так что второе ломающееся здание на уровне портило коллайдеры первого. Теперь per-instance.
        public Collider breakCocCollider;
        public Collider breakObjectCollider;
        // Коллайдер САМОГО objectView (текущего активного грейда) — гарантирован ObjectView через
        // [RequireComponent(typeof(Collider))]. Именно он чаще всего реально ловит клик "почини" —
        // breakCocCollider/breakObjectCollider не всегда тот же физический коллайдер, что стоит на
        // активном грейде, и клик по сломанному зданию проваливался в обычное взаимодействие/апгрейд.
        public Collider breakOwnCollider;
        // Не readonly: SubscribeBreakable диспоузит и пересоздаёт контейнер заново при каждой
        // (пере)подписке. CompositeDisposable после Dispose() навсегда мёртв — AddTo на уже
        // задиспоуженный контейнер сразу же диспоузит саму подписку, поэтому reuse через
        // readonly-поле невозможен, нужен свежий экземпляр.
        public CompositeDisposable BreakableDisposable = new();
        public ushort currentProductionIntervalId;
        public float productionSpeed = 1f;
        public MovableObjectTaskView ProductionProgressView => _productionProgressView;

        // Аналогично breakableActivated — реально активирована ли болезнь для ЭТОГО объекта.
        public bool diseaseActivated;
        public int currentDiseasedUnits;
        // Тот же баг, что и с брейкейблом: раньше это были общие поля DiseasableController на весь
        // уровень — второе больное здание портило состояние первого. Теперь per-instance.
        public BaseUnitsStateView diseaseUnitsView;
        public Collider diseaseCocCollider;
        // Тот же случай, что и breakOwnCollider: клик по активному грейду ловит его собственный
        // коллайдер, не обязательно тот же, что diseaseCocCollider (коллайдер родителя COC).
        public Collider diseaseOwnCollider;

        public bool IsDiseased => BaseData != null && BaseData.DiseasableView != null &&
                                   BaseData.DiseasableView.gameObject.activeInHierarchy;

        // Заполняется владельцем, когда ЭТОТ объект используется как чужой BreakableView/DiseasableView
        // оверлей (см. BreakableController.Initialize / DiseasableController.InitializeReferences).
        // У оверлея своего CanBeBroken/CanBeDiseased не настроено. Сейчас используется только внутри
        // контроллеров при подписке OnEndInteract → owner.RepairProduction()/HealBase(); оставлено
        // публичным как документирующая связь на случай, если понадобится другим системам (например UI).
        public StaticObjectView overlayOwner;

        private bool _needBreak;
        private ProductionWorkingAnimator _productionWorkingAnimator;

        public event Action<StaticObjectView> OnRepair;
        public event Action<StaticObjectView> OnHeal;
        public event Action<StaticObjectView> OnStartProduction;
        public event Action<StaticObjectView> OnStopProduction;
        public event Action<StaticObjectView, float> OnSetProductionSpeed;
        public event Action<StaticObjectView> OnDisableProduction;

        [Inject]
        private void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public override UniTask Load()
        {
            ObjectViewController.GetStaticObjectController().AddStaticObject(this);
            
            InitRequiredObjects();
            InitGraphNode();
            InitGridBlocking();
            
            return base.Load();
        }

        private void OnDestroy()
        {
            _productionWorkingAnimator?.Dispose();

            if (IsBase)
            {
                foreach (var unit in SpecialUnits)
                {
                    if (unit != null)
                        Destroy(unit.gameObject);
                }
            }
            
            BreakableDisposable.Dispose();
        }

        private void OnDisable()
        {
            _productionWorkingAnimator?.ResetImmediately();
            HideProductionProgress();
        }

        /// <summary>
        /// Setup object as home point: create units to desired units count.
        /// </summary>
        // Where this base's units spawn from and return home to. Defaults to the object pivot, but
        // when the base belongs to a ComplexObject with an interaction anchor (e.g. a unit camp that
        // upgrades itself) the units live at that anchor instead of the building centre. Otherwise a
        // home unit upgrading its own base would spawn on, and interact from, the middle of the
        // building — looking like it stands on the roof.
        public Vector3 BaseStandPosition
        {
            get
            {
                if (BaseData != null && BaseData.UseOwnPivotAsUnitHome)
                {
                    return transform.position;
                }

                var anchor = GetComponentInParent<ComplexObject>()?.InteractionAnchor;
                return anchor != null ? anchor.position : transform.position;
            }
        }

        public void SetupBase()
        {
            if (!IsBase)
                return;

            var newMovableCount = BaseData.MaxUnitCount - SpecialUnits.Count;

            var rootObject = GameObject.FindWithTag("MovableObjectsRoot");

            var nearestNodePosition = (Vector3)AstarPath.active.GetNearest(BaseStandPosition, NNConstraint.Default).node.position;

            for (var i = 0; i < newMovableCount; i++)
            {
                var movableObject = Instantiate(BaseData.UnitPrefab, rootObject?.transform);
                movableObject.transform.position = nearestNodePosition;
                _resolver.Inject(movableObject);
                movableObject.SetBasement(this);
                movableObject.gameObject.SetActive(false);
                movableObject.gameObject.SetActive(!movableObject.UnitHideInBase);
                SpecialUnits.Add(movableObject);
                movableObject.State.Value = UnitState.Idle;
            }
        }

        protected override void OnInteractionEnd()
        {
            if (_dataSO.UnitTypeCount.Count > 0)
            {
                foreach (var unit in CurrentTask.Units)
                {
                    OnSpecialUnitsEndInteract?.Invoke(unit);
                }
            }

            base.OnInteractionEnd();
        }
        
        protected override void InvokeAlternativeEvent(int index)
        {
            if (AlternativeEndInteracts != null && index >= 0 && index < AlternativeEndInteracts.Count)
            {
                AlternativeEndInteracts[index]?.Invoke();
            }
        }

        protected override void InvokeAlternativePrimaryAction(int index)
        {
            if (AlternativePrimaryActions != null && index >= 0 && index < AlternativePrimaryActions.Count)
            {
                AlternativePrimaryActions[index]?.Invoke();
            }
        }
        

        /// <summary>
        /// Лечит владельца (см. overlayOwner). Не вызывается по клику — почина/лечение теперь
        /// полноценная задача с юнитом: BreakableController/DiseasableController подписывают это
        /// на OnEndInteract объекта BreakableView/DiseasableView (юнит реально доработал), а клик
        /// по нему идёт стандартным путём регистрации задачи, без перехвата OnPrimaryAction.
        /// </summary>
        public void HealBase()
        {
            OnHeal?.Invoke(this);
        }

        private void InitGraphNode()
        {
            if (!_useInteractionGraphNode)
            {
                interactionPosition = transform.position;
                return;
            }

            interactionPosition = _useNearestInteractionGraphNode
                ? transform.position
                : _interactionGraphNodeTransform.position;

            var constraint = NNConstraint.None;
            constraint.graphMask = (1 << 0); // is first graph (point). todo fix to name cast, but nоt today(
            _interactionGraphNode = AstarPath.active.GetNearest(interactionPosition, constraint).node;

            _interactionGraphNode.Blocked += ObjectDataSO.BlocksPath ? 1 : 0;
            isBlocking = ObjectDataSO.BlocksPath;

            ConvertUnits();

            _interactionGraphNode.Penalty += _defaultNodePenalty;
            if (ObjectDataSO.BlocksPath) _interactionGraphNode.Penalty += _blockedNodePenalty;

            _interactionGraphNode.Dirty = true;
        }
        

        private void InitGridBlocking()
        {
            if (!BlocksGridArea)
            {
                return;
            }

            GridAreaBlock.Apply(GetGridBlockBounds());
        }

        private void ConvertUnits()
        {
            _blockedNodePenalty *= 1000;
            _defaultNodePenalty *= 1000;
        }
        
        private void InitRequiredObjects()
        {
            if (_requiredObjectsToInteract.Count <= 0)
                return;

            if (_requiredObjectsToInteract.All(x => x.interactionStarted || x.resourcesSubtracted))
            {
                Interact().Forget();
            }
            else
            {
                foreach (var requiredObject in _requiredObjectsToInteract)
                {
                    requiredObject.onEndInteract.AddListener(() => TryInteractAfterRequiredObject(requiredObject));
                }
            }
        }
        

        private void TryInteractAfterRequiredObject(ObjectView requiredObject)
        {
            _requiredObjectsToInteract.Remove(requiredObject);

            if (_requiredObjectsToInteract.Count == 0)
            {
                Interact().Forget();
            }
        }

        public ResourceAmount[] Steal()
        {
            gameObject.SetActive(false);
            _onStealed?.Invoke();

            UnBlockInteractionGraphNode();

            return ObjectDataSO.OutputResources;
        }

        public void BlockInteractionGraphNode()
        {
            if (!_useInteractionGraphNode) return;
            // Idempotent per object: a single StaticObjectView contributes at most one block to
            // its node. isBlocking is the authoritative "I am blocking right now" flag so that
            // UnBlock can safely no-op when we are not (see UnBlockInteractionGraphNode).
            if (isBlocking) return;
            isBlocking = true;
            _interactionGraphNode.Blocked++;
            _interactionGraphNode.Penalty += _blockedNodePenalty;
        }

        public void UnBlockInteractionGraphNode()
        {
            GridAreaBlock.Release();

            if (!_useInteractionGraphNode)
            {
                gameObject.SetActive(false);
                AstarPath.active.UpdateGraphs(GetComponent<Collider>().bounds);
                return;
            }
            // Idempotent: only lower Blocked/Penalty if this object is actually contributing a
            // block right now. Without this, a second unblock (rapid re-click on an obstacle, or
            // a later ApplyFinalInteractionEffects) would decrement Blocked again and could free
            // a shared node while another obstacle still sits on it. isBlocking mirrors exactly
            // what InitGraphNode / BlockInteractionGraphNode raised, so the decrement is balanced.
            if (!isBlocking) return;
            isBlocking = false;
            _interactionGraphNode.Blocked--;
            int penalty = (int)(_interactionGraphNode.Penalty - _blockedNodePenalty);
            _interactionGraphNode.Penalty = penalty >= 0 ? (uint)penalty : 0;
        }

        #region Production
        public void StartProductionWorkingAnimation()
        {
            _productionWorkingAnimator ??= new ProductionWorkingAnimator(
                this,
                _productionWorkingAnimation,
                () => productionSpeed);
            _productionWorkingAnimator.Start();
        }

        public void PauseProductionWorkingAnimation()
        {
            _productionWorkingAnimator?.Pause();
        }

        public void ResumeProductionWorkingAnimation()
        {
            _productionWorkingAnimator?.Resume();
        }

        public void StopProductionWorkingAnimation()
        {
            _productionWorkingAnimator?.Stop();
        }

        public void StopProductionVisuals()
        {
            StopProductionWorkingAnimation();
            HideProductionProgress();
        }

        private void HideProductionProgress()
        {
            if (_productionProgressView == null)
                return;

            _productionProgressView.Report(0f, false);
            _productionProgressView.gameObject.SetActive(false);
        }

        public void DisableProduction()
        {
            OnDisableProduction?.Invoke(this);
        }
        
        /// <summary>
        /// Set production speed modify.
        /// </summary>
        /// <param name="speedModify">Modifier.</param>
        public void SetProductionSpeedModify(float speedModify)
        {
            if (!CanProduceObjects)
            {
                return;
            }

            OnSetProductionSpeed?.Invoke(this, speedModify);
        }
        
        public void RepairProduction()
        {
            OnRepair?.Invoke(this);
        }

        public void StartProduction()
        {
            OnStartProduction?.Invoke(this);
        }

        public void StopProduction()
        {
            OnStopProduction?.Invoke(this);
        }
        #endregion

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            Gizmos.color = Color.blue;

            Gizmos.DrawWireSphere(transform.position, _dataSO.InteractionOffset);
        }
#endif
    }
}
