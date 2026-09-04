#if TK2D
using UnityEngine;
using DG.Tweening;

namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(tk2dCamera))]
    [AddComponentMenu("Creobit/EditionsUpgrade/PinchToZoomCamera2DTK")]
    public class PinchToZoomCamera2DTK : MonoBehaviour
    {
        [SerializeField]
        private BoundingBox2D m_BoundingBox;

        [SerializeField]
        private float m_MinCameraSize = 300f;

        [Header("Move and Zoom Settings")]

        [SerializeField]
        private float m_Duration = 0.035f;

        [SerializeField]
        private Ease m_Ease = Ease.Linear;

        [Header("Swipe Settings")]

        [SerializeField]
        private float m_MinSwipeDistance = 30.0f;

        [SerializeField]
        private float m_MaxSwipeDetectTime = 0.15f;

        [SerializeField]
        private float m_SwipeTime = 0.9f;

        [SerializeField]
        private Ease m_SwipeEase = Ease.OutQuint;

        private tk2dCamera m_CurrentCamera;
        private Bounds m_CameraBounds;
        private float m_PinchMultiplier;
        private float m_MaxCameraSize = 1f;

        private Bounds m_WorldBounds;

        private float m_SwipeStartTime;
        private Vector2 m_SwipeStartPosition;
        private Tweener m_SwipeTween;

		private bool m_IsZoomUsed = false;

        #region MonoBehaviour

        //=====================================================================================

        void Start()
        {
#if !UNITY_ANDROID || !UNITY_IOS
			//Destroy(this);
#endif

            if (m_BoundingBox == null)
            {
                Debug.LogError("Bounding Box for Camera not found!");
                enabled = false;
            }

            DOTween.Init();

            m_WorldBounds = m_BoundingBox.BoundingBox;

            m_CurrentCamera = GetComponent<tk2dCamera>();

            m_CameraBounds = GetCameraBounds(m_CurrentCamera, m_CurrentCamera.transform.position);
            m_MaxCameraSize = m_CurrentCamera.CameraSettings.orthographicSize;
            m_CurrentCamera.transform.position = m_BoundingBox.transform.position;

            m_PinchMultiplier = (m_MaxCameraSize - m_MinCameraSize) * 0.01f;
        }

        void Update()
        {
            if (Input.touchCount == 0)
            {
                return;
            }

            if (Input.touchCount == 1)
            {
                SwipeGesture(Input.GetTouch(0));
            }

            if (Input.touchCount == 2)
            {
                PinchGesture(Input.GetTouch(0), Input.GetTouch(1));
            }
        }

        #endregion

        #region Methods

        private void SwipeGesture(Touch finger)
        {
            switch (finger.phase)
            {
                case TouchPhase.Began:
                    m_SwipeStartTime = Time.time;
                    m_SwipeStartPosition = finger.position;
                    if (m_SwipeTween != null)
                    {
                        if (m_SwipeTween.IsPlaying())
                        {
                            m_SwipeTween.Kill();
                        }
                    }
                    break;

                case TouchPhase.Ended:
                    if (((Time.time - m_SwipeStartTime) < m_MaxSwipeDetectTime) && (finger.position - m_SwipeStartPosition).magnitude > m_MinSwipeDistance)
                    {
                        Vector2 direction = finger.position - m_SwipeStartPosition;
                        direction.Normalize();

                        direction = -direction;

                        Vector3 finalCameraPosition = new Vector3(
                            direction.x * m_WorldBounds.size.x,
                            direction.y * m_WorldBounds.size.y,
                            m_WorldBounds.size.z);

                        finalCameraPosition = ClampCameraPosition(finalCameraPosition);

                        m_SwipeTween = m_CurrentCamera.transform.
                            DOMove(finalCameraPosition, m_SwipeTime).
                            SetEase(m_SwipeEase).
                            OnUpdate(() => { MoveCamera(Vector3.zero); });
                    }
                    break;

                case TouchPhase.Moved:
                    if ((Time.time - m_SwipeStartTime) > m_MaxSwipeDetectTime)
                    {
                        MoveCamera(finger.deltaPosition);
                    }
                    break;
            }
        }

        private void PinchGesture(Touch firstFinger, Touch secondFinger)
        {
            if (firstFinger.phase == TouchPhase.Moved && secondFinger.phase == TouchPhase.Moved)
            {
                Vector2 firstPrevPos = firstFinger.position - firstFinger.deltaPosition;
                Vector2 secondPrevPos = secondFinger.position - secondFinger.deltaPosition;

                float prevTouchDelta = (firstPrevPos - secondPrevPos).magnitude;
                float touchDelta = (firstFinger.position - secondFinger.position).magnitude;

#if UNITY_ANDROID
                m_PinchMultiplier = (m_CurrentCamera.CameraSettings.orthographicSize + m_MinCameraSize) * 0.01f;
#elif UNITY_IOS
                m_PinchMultiplier = (m_CurrentCamera.CameraSettings.orthographicSize - m_MinCameraSize) * 0.01f;
#endif

				m_PinchMultiplier = Mathf.Max(m_PinchMultiplier, 2f);

                ZoomCamera((prevTouchDelta - touchDelta) * m_PinchMultiplier, m_Duration);
            }
        }

        private float GetMaxCameraSize(tk2dCamera camera, Bounds boundingBox)
        {
            return (boundingBox.size.x / camera.ScreenCamera.aspect / 2f);
        }

        private Bounds GetCameraBounds(tk2dCamera targetCamera, Vector3 center)
        {
            float height = targetCamera.CameraSettings.orthographicSize * 2f;
            Vector2 size = new Vector2(height * targetCamera.ScreenCamera.aspect, height);

            return new Bounds(center, size);
        }

        private void MoveCamera(Vector3 cameraOffset)
        {
            float moveMultiplier;
            moveMultiplier = (m_CurrentCamera.CameraSettings.orthographicSize + m_MinCameraSize) * 0.01f;

            moveMultiplier = Mathf.Max(moveMultiplier, 2f);

            Vector3 newCamPosition = m_CurrentCamera.transform.position - (cameraOffset * moveMultiplier);

            m_CameraBounds = GetCameraBounds(m_CurrentCamera, newCamPosition);

            newCamPosition = ClampCameraPosition(newCamPosition);

            if(cameraOffset == Vector3.zero)
            {
                m_CurrentCamera.transform.position = newCamPosition;
            }
            else
            {
                m_CurrentCamera.transform.DOMove(newCamPosition, m_Duration).SetEase(m_Ease);
            }
        }

        private void ZoomCamera(float zoomFactor, float duration, TweenCallback onComplete = null)
        {
            float finalCameraSize = m_CurrentCamera.CameraSettings.orthographicSize + zoomFactor;

            finalCameraSize = Mathf.Clamp(finalCameraSize, m_MinCameraSize, m_MaxCameraSize);

            DOTween.To(
                () => m_CurrentCamera.CameraSettings.orthographicSize,
                (size) => m_CurrentCamera.CameraSettings.orthographicSize = size,
                finalCameraSize,
                duration).
                SetEase(m_Ease).
                OnUpdate(() => { MoveCamera(Vector3.zero); }).
                OnComplete(onComplete);

			if (m_IsZoomUsed == false) 
			{
				m_IsZoomUsed = true;
			}
        }

        private Vector3 ClampCameraPosition(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, m_WorldBounds.min.x + m_CameraBounds.extents.x, m_WorldBounds.max.x - m_CameraBounds.extents.x),
                Mathf.Clamp(position.y, m_WorldBounds.min.y + m_CameraBounds.extents.y, m_WorldBounds.max.y - m_CameraBounds.extents.y));
        }

        public void FullZoomIn(float duration, TweenCallback onCompleteCallback)
        {
            ZoomCamera(-m_MaxCameraSize, duration, onCompleteCallback);
        }

        public void FullZoomOut(float duration, TweenCallback onCompleteCallback)
        {
            ZoomCamera(m_MaxCameraSize, duration, onCompleteCallback);
        }

#endregion

    }
}
#endif