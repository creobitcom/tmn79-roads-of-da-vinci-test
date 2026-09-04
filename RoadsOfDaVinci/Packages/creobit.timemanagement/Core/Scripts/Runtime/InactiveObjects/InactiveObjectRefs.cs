using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.StandingPoint;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using Pathfinding;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects
{
    public class InactiveObjectRefs : MonoBehaviour, ILevelLoadUnit
    {
        public bool State;
        public bool ObjectSwitcher;
        public bool NodeSwitcher;

        public readonly ReactiveCommand OnStateChanged = new();

        public Collider mainCollider;
        public SpriteRenderer lightSprite;

        public List<IReactToActions> IncludedObjects = new();
        public List<GraphNode> IncludedNodes = new();

        [Inject]
        private IInactiveObjectController _inactiveObjectController;

        [Title("Light Transfer")]

        [field: SerializeField]
        public bool IsTransferable { get; private set; }

        [field: SerializeField]
        [ShowIf(nameof(IsTransferable))]
        public StandingPointView LinkedStandingPoint { get; private set; }

        [field: SerializeField]
        [ShowIf(nameof(IsTransferable))]
        public float LightExpandDuration { get; private set; } = 0.5f;
        [field: SerializeField]
        [ShowIf(nameof(IsTransferable))]
        public float CarrierLightScale { get; private set; } = 1.6f;

        [Title("Evacuation Settings")]
        [SerializeField] private BubbleSequenceSO _evacuationBubbleSequence;
        public BubbleSequenceSO EvacuationBubbleSequence => _evacuationBubbleSequence;

        [Inject]
        private IInactiveAreaTransferController _transferController;

        public bool IsExternallyAnimated { get; set; }

        public float Radius => mainCollider is SphereCollider sphere ? sphere.radius : 0f;

        private Vector3 _baseLightScale = Vector3.one;
        private float _currentMask;
        private CancellationTokenSource _maskCts;

        public UniTask Load()
        {
            InitObjects();

            if (lightSprite != null)
            {
                var radius = Radius;

                if (radius > 0f)
                {
                    var lightSize = radius / 5f * 1.6f;
                    _baseLightScale = new Vector3(lightSize, lightSize, lightSize);
                    lightSprite.transform.localScale = _baseLightScale;
                }
                else
                {
                    _baseLightScale = lightSprite.transform.localScale;
                }
            }

            _inactiveObjectController.AddInactiveObject(this);

            if (IsTransferable && LinkedStandingPoint != null)
            {
                _transferController?.RegisterTransferable(this);
            }

            return UniTask.CompletedTask;
        }

        public void InitObjects()
        {
            IncludedObjects = InactiveObjectController.GetOverlappingColliders(mainCollider)
                .Select(col2d => col2d?.gameObject.GetComponent<IReactToActions>())
                .Where(obj => obj != null)
                .ToList();

            IncludedNodes = InactiveObjectController.GetOverlappingColliders(mainCollider)
                .Where(obj => obj != null)
                .Select(col2d => AstarPath.active.GetNearest(col2d.gameObject.transform.position).node)
                .Where(obj => obj != null)
                .ToList();
        }

        public void SetState(bool state)
        {
            State = state;
            OnStateChanged?.Execute(Unit.Default);
        }

        public void ChangeRadius(float radius)
        {
            ((SphereCollider)mainCollider).radius = radius;
            var lightSize = radius / 5 * 1.6f;
            _baseLightScale = new Vector3(lightSize, lightSize, lightSize);
            lightSprite.transform.localScale = _baseLightScale;
            InitObjects();
            OnStateChanged?.Execute(Unit.Default);
        }

        public void ChangeRadiusTemporary(float toSize, float changeSizeTime)
        {
            _inactiveObjectController.ChangeRadiusTemporary(this, toSize, changeSizeTime);
        }

        public void PlayMaskTransition(float target, float duration, float overshoot)
        {
            if (lightSprite == null)
            {
                return;
            }

            StopMaskTransition();

            if (IsExternallyAnimated || duration <= 0f || Mathf.Approximately(_currentMask, target))
            {
                ApplyMask(target);
                return;
            }

            _maskCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            AnimateMask(target, duration, overshoot, _maskCts.Token).Forget();
        }

        private async UniTaskVoid AnimateMask(float target, float duration, float overshoot, CancellationToken token)
        {
            var from = _currentMask;
            var elapsed = 0f;

            lightSprite.gameObject.SetActive(true);

            try
            {
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    var progress = Mathf.Clamp01(elapsed / duration);
                    var eased = progress * progress * (3f - 2f * progress);

                    SetMaskValue(Mathf.Lerp(from, target, eased));
                    lightSprite.transform.localScale = _baseLightScale * (1f + overshoot * Mathf.Sin(progress * Mathf.PI));

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            ApplyMask(target);
        }

        private void ApplyMask(float mask)
        {
            SetMaskValue(mask);
            lightSprite.transform.localScale = _baseLightScale;
            lightSprite.gameObject.SetActive(mask > 0.001f);
        }

        private void SetMaskValue(float mask)
        {
            _currentMask = mask;
            lightSprite.color = new Color(1f, 1f, 1f, mask);
        }

        private void StopMaskTransition()
        {
            if (_maskCts == null)
            {
                return;
            }

            _maskCts.Cancel();
            _maskCts.Dispose();
            _maskCts = null;
        }

        public UniTask Dispose()
        {
            StopMaskTransition();

            return UniTask.CompletedTask;
        }
    }
}
