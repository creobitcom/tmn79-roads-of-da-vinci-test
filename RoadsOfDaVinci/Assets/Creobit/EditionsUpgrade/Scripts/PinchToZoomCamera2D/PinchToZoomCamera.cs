using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/Zoom/PinchToZoomCamera")]
    public class PinchToZoomCamera : MonoBehaviour
    {
#if TK2D
        [SerializeField] private tk2dCamera _tk2dCamera;
#else
        [SerializeField] private Camera _camera;
#endif
        [SerializeField] private float _zoomOutMin = 450f;
        [SerializeField] private float _zoomOutMax = 768f;
        [SerializeField] private float _scrollSpeed = 200f;
        [SerializeField] private float _zoomSpeed = 0.8f;
        [SerializeField, Range(0,2f)] private float Duration = 0.08f;

#region PauseCheck
        [Header("Pause")]
        [SerializeField] private GameObject _shadowObject;

        private bool _hasShadowObject;
        private bool _isPaused;

#endregion
        
        
        
        private Vector2 _center;
        private Vector2 _maxCameraMinXY;
        private Vector2 _maxCameraMaxXY;
        
        private float _maxCameraHeight;
        private float _maxCameraWidth;
        private Vector3 _startTouch;
        private float _minX, _maxX, _minY, _maxY;
        
        private float SwipeStartTime;
        private Vector2 SwipeStartPosition;
        private Tweener SwipeTween;
        private float MaxSwipeDetectTime = 0.15f;
        private float SwipeTime = 0.9f;
        private Ease SwipeEase = Ease.OutQuint;
        private Ease Ease = Ease.Linear;
        private float MinSwipeDistance = 30.0f;
        
        private void Start()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            enabled = false;
#endif

#if UNITY_ANDROID || UNITY_IOS
           enabled = true;
#endif
            
#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
           enabled = true;
#endif
            
            if (_shadowObject != null)
                _hasShadowObject = true;
#if TK2D
            _tk2dCamera = GetComponent<tk2dCamera>();
            if (_tk2dCamera == null)
            {
                DestroyImmediate(this);
                return;
            }

            _maxCameraHeight = 2f * _zoomOutMax;
            _maxCameraWidth = _maxCameraHeight * _tk2dCamera.ScreenCamera.aspect;
            var halfWidth = _maxCameraWidth / 2f;
            var halfHeight = _maxCameraHeight / 2f;
            _center = new Vector2(_tk2dCamera.ScreenCamera.transform.position.x, _tk2dCamera.ScreenCamera.transform.position.y);
#else 
            _camera ??= GetComponent<Camera>();

            if (_camera == null)
            {
                DestroyImmediate(this);
                return;
            }
            _maxCameraHeight = 2f * _zoomOutMax;
            _maxCameraWidth = _maxCameraHeight * _camera.aspect;
            var halfWidth = _maxCameraWidth / 2f;
            var halfHeight = _maxCameraHeight / 2f;
            _center = new Vector2(_camera.transform.position.x, _camera.transform.position.y);
#endif
            
            _maxCameraMinXY = new Vector2(_center.x - halfWidth, _center.y - halfHeight);
            _maxCameraMaxXY = new Vector2(_center.x + halfWidth, _center.y + halfHeight);
            
            CalculateCameraBounds();
        }

        private void Update()
        {
            if (_hasShadowObject)
                SetZoomPause(_shadowObject.activeInHierarchy);
            if (_isPaused)
                return;
#if UNITY_ANDROID || UNITY_IOS

        if (Input.GetMouseButtonDown(0))
        {
#if TK2D
            _startTouch = _tk2dCamera.ScreenCamera.ScreenToWorldPoint(Input.mousePosition);
#else
            _startTouch = _camera.ScreenToWorldPoint(Input.mousePosition);
#endif
        }

        if (Input.touchCount == 1)
        {
            SwipeGesture(Input.GetTouch(0));
        }

        if (Input.touchCount >= 2)
        {
            var firstTouch = Input.GetTouch(0);
            var secondTouch = Input.GetTouch(1);

            var firstTouchSavedPos = firstTouch.position - firstTouch.deltaPosition;
            var secondTouchSavedPos = secondTouch.position - secondTouch.deltaPosition;

            var savedMagnitude = (firstTouchSavedPos - secondTouchSavedPos).magnitude;
            var currentMagnitude = (firstTouch.position - secondTouch.position).magnitude;

            var difference = currentMagnitude - savedMagnitude;
            Zoom(difference*_zoomSpeed);
        }
        
#endif
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                Zoom(Input.GetAxis("Mouse ScrollWheel") * 1000f);
            }
#endif
        }
        
        private void Zoom(float zoomScale)
        {
            
#if TK2D
            float newOrthographicSize = Mathf.Clamp(_tk2dCamera.CameraSettings.orthographicSize - zoomScale, _zoomOutMin, _zoomOutMax);
            _tk2dCamera.CameraSettings.orthographicSize = newOrthographicSize;
#else
            float newOrthographicSize = Mathf.Clamp(_camera.orthographicSize - zoomScale, _zoomOutMin, _zoomOutMax);
            _camera.orthographicSize = newOrthographicSize;
#endif
            
            CalculateCameraBounds();
        }

        private void CalculateCameraBounds()
        {
            
#if TK2D
            var cameraTransform = _tk2dCamera.transform;
            var cameraPosition = cameraTransform.position;
            
            var cameraHeight = 2f * _tk2dCamera.CameraSettings.orthographicSize;
            var cameraWidth = cameraHeight * _tk2dCamera.ScreenCamera.aspect;
#else
            var cameraTransform = _camera.transform;
            var cameraPosition = cameraTransform.position;
            
            var cameraHeight = 2f * _camera.orthographicSize;
            var cameraWidth = cameraHeight * _camera.aspect;
#endif
            

            var halfWidth = cameraWidth / 2f;
            var halfHeight = cameraHeight / 2f;

            var minXYCurrent = new Vector2(_center.x - halfWidth, _center.y - halfHeight);
            var maxXYCurrent = new Vector2(_center.x + halfWidth, _center.y + halfHeight);

            if (Math.Abs(minXYCurrent.x) > Math.Abs(_maxCameraMinXY.x))
            {
                minXYCurrent = new Vector2(_maxCameraMinXY.x, minXYCurrent.y);
                cameraPosition = new Vector3(_center.x, cameraPosition.y,cameraPosition.z);
                cameraTransform.position = cameraPosition;
            }
            
            if (Math.Abs(maxXYCurrent.x) > Math.Abs(_maxCameraMaxXY.x))
            {
                maxXYCurrent = new Vector2(_maxCameraMaxXY.x, maxXYCurrent.y);
                cameraPosition = new Vector3(_center.x, cameraPosition.y,cameraPosition.z);
                cameraTransform.position = cameraPosition;
            }
            
            if (Math.Abs(minXYCurrent.y) > Math.Abs(_maxCameraMinXY.y))
            {
                minXYCurrent = new Vector2(minXYCurrent.x, _maxCameraMinXY.y);
                cameraPosition = new Vector3(cameraPosition.x, _center.y, cameraPosition.z);
                cameraTransform.position = cameraPosition;
            }
            
            if (Math.Abs(maxXYCurrent.y) > Math.Abs(_maxCameraMaxXY.y))
            {
                maxXYCurrent = new Vector2(maxXYCurrent.x, _maxCameraMaxXY.y);
                cameraPosition = new Vector3(cameraPosition.x, _center.y, cameraPosition.z);
                cameraTransform.position = cameraPosition;
            }
            
            _minX = (Math.Abs(_maxCameraMinXY.x) - Math.Abs(minXYCurrent.x)) * NegativeValueFix(_maxCameraMinXY.x); 
            _maxX = (Math.Abs(_maxCameraMaxXY.x) - Math.Abs(maxXYCurrent.x)) * NegativeValueFix(_maxCameraMaxXY.x);
            _minY = (Math.Abs(_maxCameraMinXY.y) - Math.Abs(minXYCurrent.y)) * NegativeValueFix(_maxCameraMinXY.y);
            _maxY = (Math.Abs(_maxCameraMaxXY.y) - Math.Abs(maxXYCurrent.y)) * NegativeValueFix(_maxCameraMaxXY.y);

#if TK2D
            _tk2dCamera.transform.position = GetNewPosition(Vector3.zero, Vector3.zero);
#else
            _camera.transform.position = GetNewPosition(Vector3.zero, Vector3.zero);
#endif
            
        }

        private int NegativeValueFix(float value)
        {
            return value>=0 ? 1 : -1;
        }
        
        private void SwipeGesture(Touch finger)
        {
            switch (finger.phase)
            {
                case TouchPhase.Began:
                    SwipeStartTime = Time.time;
                    SwipeStartPosition = finger.position;
                    if (SwipeTween != null)
                    {
                        if (SwipeTween.IsPlaying())
                        {
                            SwipeTween.Kill();
                        }
                    }
                    break;

                case TouchPhase.Ended:
                    if (((Time.time - SwipeStartTime) < MaxSwipeDetectTime) && (finger.position - SwipeStartPosition).magnitude > MinSwipeDistance)
                    {
                        Vector2 direction = finger.position - SwipeStartPosition;
                        direction.Normalize();

                        SwipeTween =
#if TK2D
                            _tk2dCamera.transform.
#else
                            _camera.transform.
#endif
                            DOMove(GetNewNegativePosition(SwipeStartPosition, finger.position), SwipeTime).
                            SetEase(SwipeEase).
                            OnUpdate(() => { MoveCamera(Vector3.zero); });
                    }
                    break;

                case TouchPhase.Moved:
                    if ((Time.time - SwipeStartTime) > MaxSwipeDetectTime)
                    {
                        MoveCamera(finger.deltaPosition);
                    }
                    break;
            }
        }
        
        private void MoveCamera(Vector3 cameraOffset)
        {
#if TK2D
            float moveMultiplier;
            moveMultiplier = (_tk2dCamera.CameraSettings.orthographicSize + _scrollSpeed) * 0.01f;

            moveMultiplier = Mathf.Max(moveMultiplier, 2f);

            Vector3 newCamPosition = _tk2dCamera.transform.position - (cameraOffset * moveMultiplier);

            newCamPosition.x = Mathf.Clamp(newCamPosition.x, _minX, _maxX);
            newCamPosition.y = Mathf.Clamp(newCamPosition.y, _minY, _maxY);
            
            if(cameraOffset == Vector3.zero)
            {
                _tk2dCamera.transform.position = newCamPosition;
            }
            else
            {
                _tk2dCamera.transform.DOMove(newCamPosition, Duration).SetEase(Ease);
            }
#else
            float moveMultiplier;
            moveMultiplier = (_camera.orthographicSize + _scrollSpeed) * 0.01f;

            moveMultiplier = Mathf.Max(moveMultiplier, 2f);

            Vector3 newCamPosition = _camera.transform.position - (cameraOffset * moveMultiplier);

            newCamPosition.x = Mathf.Clamp(newCamPosition.x, _minX, _maxX);
            newCamPosition.y = Mathf.Clamp(newCamPosition.y, _minY, _maxY);
            
            if(cameraOffset == Vector3.zero)
            {
                _camera.transform.position = newCamPosition;
            }
            else
            {
                _camera.transform.DOMove(newCamPosition, Duration).SetEase(Ease);
            }
#endif
            
        }

        private Vector3 GetNewPosition(Vector3 startTouch, Vector3 secondTouch)
        {
            var direction = startTouch - secondTouch;
#if TK2D
            var newPosition = _tk2dCamera.transform.position + direction;
#else
            var newPosition = _camera.transform.position + direction;
#endif
            
            newPosition.x = Mathf.Clamp(newPosition.x, _minX, _maxX);
            newPosition.y = Mathf.Clamp(newPosition.y, _minY, _maxY);
            return newPosition;
        }
        
        private Vector3 GetNewNegativePosition(Vector3 startTouch, Vector3 secondTouch)
        {
            var direction = startTouch - secondTouch;
#if TK2D
            var newPosition = _tk2dCamera.transform.position - direction;
#else
            var newPosition = _camera.transform.position - direction;
#endif
            
            newPosition.x = Mathf.Clamp(newPosition.x, _minX, _maxX);
            newPosition.y = Mathf.Clamp(newPosition.y, _minY, _maxY);
            return newPosition;
        }

        public void SetZoomPause(bool value)
        {
            _isPaused = value;
        }
    }
}