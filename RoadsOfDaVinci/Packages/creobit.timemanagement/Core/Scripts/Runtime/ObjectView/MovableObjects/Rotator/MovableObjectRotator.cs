using System;
using Pathfinding;
using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Rotator
{
    public class MovableObjectRotator : IDisposable
    {
        private IDisposable _updateObserver;
        
        private readonly float _rotationSpeed;

        private readonly IAstarAI _astarAI;
        
        private readonly IMovableObject _movableObject;
        
        private readonly Transform _modelTransform;

        private Quaternion _initialRotation;

        public MovableObjectRotator(float rotationSpeed, IAstarAI astarAI, IMovableObject movableObject, Transform modelTransform)
        {
            _rotationSpeed = rotationSpeed;
            _astarAI = astarAI;
            _movableObject = movableObject;
            _modelTransform = modelTransform;
        }

        public void Initialize()
        {
            _movableObject.StartMove += OnStartMove; 
            _movableObject.EndMove += OnEndMove;
            
            _initialRotation = _modelTransform.rotation;
        }

        public void Dispose()
        {
            _movableObject.StartMove -= OnStartMove;
            _movableObject.EndMove -= OnEndMove;
            
            _updateObserver?.Dispose();
        }

        private void Rotate()
        {
            if (_modelTransform == null) return;
            
            var pathIsReached = _astarAI.reachedDestination || _astarAI.reachedEndOfPath;
            
            var pointPosition = pathIsReached ? _astarAI.destination : _astarAI.steeringTarget;
            
            //Получаем направление движения юнита
            var direction = pointPosition - _modelTransform.position;

            //Если движения нет, то и поворачиваться будет некуда. А следовательно, и не нужно
            if (direction.sqrMagnitude < 0.001f) return;
            
            //Получаем угол направления движения в плоскости XY
            var angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            
            //Устанавливаем, чтобы поворот происходил вокруг оси самого юнита
            var angleAxis = Quaternion.AngleAxis(angle, Vector3.up);

            //Учитывая изначальный наклон юнита, создаём новый поворот по направлению движения
            var rotation = _initialRotation * angleAxis;
            
            _modelTransform.rotation = Quaternion.Lerp
                (_modelTransform.rotation, rotation, Time.deltaTime * _rotationSpeed);
        }

        private void OnStartMove()
        {
            _updateObserver = Observable
                .EveryUpdate()
                .Subscribe(_ => Rotate());
        }

        private void OnEndMove(MovableObjectView movableObjectView)
        {
            _updateObserver?.Dispose();
        }
    }
}
