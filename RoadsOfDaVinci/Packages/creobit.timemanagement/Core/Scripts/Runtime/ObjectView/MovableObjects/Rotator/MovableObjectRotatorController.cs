using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Rotator
{
    public class MovableObjectRotatorController
    {
        private readonly MovableObjectController _movableObjectController;
 
        public MovableObjectRotatorController(MovableObjectController movableObjectController)
        {
            _movableObjectController = movableObjectController;
        }

        public void Load()
        {
            _movableObjectController.MovableUnitAdded += SetupUnit;
        }

        public void Dispose()
        {
            _movableObjectController.MovableUnitAdded -= SetupUnit;
        }

        private void SetupUnit(MovableObjectView unit)
        {
            unit.StartMove += OnStartMove; 
            unit.EndMove += OnEndMove;
            
            unit.initialRotation = unit.modelTransform.rotation;
        }

        public void Rotate(MovableObjectView unit)
        {
            if (unit.modelTransform == null) return;

            // While movement is locked (e.g. a Humorist stun), an external owner controls facing
            // (turning the unit toward its attacker/target). Don't fight it with path rotation.
            if (unit.IsMovementLocked) return;

            var pathIsReached = unit.AStarAI.reachedDestination || unit.AStarAI.reachedEndOfPath;
            var pointPosition = pathIsReached ? unit.AStarAI.destination : unit.AStarAI.steeringTarget;
            
            //Получаем направление движения юнита
            var direction = pointPosition - unit.modelTransform.position;

            //Если движения нет, то и поворачиваться будет некуда. А следовательно, и не нужно
            if (direction.sqrMagnitude < 0.001f) return;
            
            //Получаем угол направления движения в плоскости XY
            var angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            
            //Устанавливаем, чтобы поворот происходил вокруг оси самого юнита
            var angleAxis = Quaternion.AngleAxis(angle, Vector3.up);

            //Учитывая изначальный наклон юнита, создаём новый поворот по направлению движения
            var rotation = unit.initialRotation * angleAxis;
            
            unit.modelTransform.rotation = Quaternion.Lerp(unit.modelTransform.rotation, rotation, 
                Time.deltaTime * unit.movableObjectDataSo.RotatorRotationSpeed);
        }
        
        private void RotateToTarget(MovableObjectView unit)
        {
            if (unit.modelTransform == null) return;
            
            var pointPosition = unit.AStarAI.destination;
            
            //Получаем направление движения юнита
            var direction = pointPosition - unit.modelTransform.position;
            
            //Получаем угол направления движения в плоскости XY
            var angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            
            //Устанавливаем, чтобы поворот происходил вокруг оси самого юнита
            var angleAxis = Quaternion.AngleAxis(angle, Vector3.up);

            //Учитывая изначальный наклон юнита, создаём новый поворот по направлению движения
            var rotation = unit.initialRotation * angleAxis;

            unit.modelTransform.rotation = rotation;
        }

        private void OnStartMove(MovableObjectView unit)
        {
            unit.UpdateObserver.Dispose();
            unit.UpdateObserver = new DisposableBag();
            
            Observable
                .EveryUpdate()
                .Subscribe(_ => Rotate(unit))
                .AddTo(ref unit.UpdateObserver);;
        }

        private void OnEndMove(MovableObjectView unit)
        {
            unit.UpdateObserver.Dispose();
        }

        private bool RotatorIsUsing(MovableObjectView unit)
        {
            return unit.movableObjectDataSo != null && unit.movableObjectDataSo.UseRotator;
        }
    }
}
