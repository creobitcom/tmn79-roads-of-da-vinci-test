using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.StandingPoint
{
    [RequireComponent(typeof(BoxCollider))]
    public class StandingPointView : StaticObjectView
    {
        [SerializeField] private float _lookAngle = 180f;

        [SerializeField] private InactiveObjectRefs _linkedInactiveArea;

        public InactiveObjectRefs LinkedInactiveArea => _linkedInactiveArea;

        [Inject] private IInactiveAreaTransferController _transferController;
        
        public MovableObjectView OccupyingUnit { get; private set; }
        public bool IsOccupied => OccupyingUnit != null;

        private void OnDestroy()
        {
            OccupyingUnit = null;
        }

        public override UniTask Dispose()
        {
            FreePoint();
            return base.Dispose();
        }

        public override void OnPrimaryAction()
        {
            if (_linkedInactiveArea != null && _transferController != null)
            {
                if (_transferController.TryStartTransfer(this))
                {
                    return;
                }
            }

            if (IsOccupied || IsUsing || isRegisteringTask > 0)
            {
                return;
            }

            base.OnPrimaryAction();
        }

        public void TriggerNormalPrimaryAction()
        {
            base.OnPrimaryAction();
        }

        protected override void OnInteractionEnd()
        {
            var unit = CurrentTask?.Units?.FirstOrDefault();
            if (unit != null)
            {
                OccupyingUnit = unit;
                unit.CurrentStandingPoint = this;

                unit.PlayAnimation(RuntimeConstants.AnimationStates.Idle);

                unit.modelTransform.rotation =
                    unit.initialRotation * Quaternion.AngleAxis(_lookAngle, Vector3.up);

                if (ObjectDataSO.MovableObjectHideOnInteract)
                    unit.modelTransform.gameObject.SetActive(false);
            }

            base.OnInteractionEnd();
        }

        public void FreePoint()
        {
            OccupyingUnit = null;
        }
    }
}