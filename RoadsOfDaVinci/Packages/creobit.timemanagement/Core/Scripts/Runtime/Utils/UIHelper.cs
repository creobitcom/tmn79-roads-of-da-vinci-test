using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils
{
    public static class UIHelper
    {
        /// <summary>
        /// True when the pointer is over UI that owns the click, so it must not reach the
        /// world behind it: either UI that handles the pointer (a button), or any graphic
        /// the player can actually see (a panel background).
        /// </summary>
        /// <remarks>
        /// Invisible, handler-less graphics are deliberately treated as click-through. The
        /// meta map is world-space sprites, and `Meta.unity` covers it with `ScrollZone` — a
        /// fully transparent raycast-target Image with no handler, left over from a scroll
        /// mechanism that is no longer wired up. A plain "is the pointer over any UI" test
        /// hits it and would kill every map click and drag.
        /// </remarks>
        public static bool IsPointerOverBlockingUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                return false;
            }

            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            foreach (var raycastResult in raycastResults)
            {
                var target = raycastResult.gameObject;

                if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null
                    || ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) != null
                    || ExecuteEvents.GetEventHandler<IDragHandler>(target) != null)
                {
                    return true;
                }

                // A visible graphic is something the player perceives as solid UI — the
                // bottom panel's background, for instance. Pressing it is aimed at the panel,
                // not at the map underneath, so it must not drag the map either.
                var graphic = target.GetComponent<Graphic>();

                if (graphic != null && graphic.color.a > 0f)
                {
                    return true;
                }
            }

            return false;
        }
        public static Vector2 ConvertWorldToLocalCanvasPosition(
            Vector3 worldPosition,
            Camera mainCamera, Canvas canvas,
            Vector2 offsetDirection = default, Vector2 offset = default,
            float width = 0, float height = 0)
        {
            var screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            var canvasRectTransform = (RectTransform)canvas.transform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform, screenPosition, canvas.worldCamera, out var localPosition);

            var calculatedPosition = localPosition + offsetDirection + offset;

            var sizeDelta = canvasRectTransform.sizeDelta;

            var halfCanvasWidth = sizeDelta.x / 2;

            var halfCanvasHeight = sizeDelta.y / 2;

            if (offset != Vector2.zero)
            {
                var isOutOfHorizontalBounds = Mathf.Abs(calculatedPosition.x) + width / 2 > halfCanvasWidth;
                var isOutOfVerticalBounds = Mathf.Abs(calculatedPosition.y) + height / 2 > halfCanvasHeight;

                if (isOutOfHorizontalBounds) offset.x *= -1;
                if (isOutOfVerticalBounds) offset.y *= -1;

                calculatedPosition = localPosition + offsetDirection + offset;
            }

            calculatedPosition = ClampPosition(calculatedPosition, halfCanvasWidth, halfCanvasHeight);
            
            calculatedPosition += AdjustForOverflow(calculatedPosition, halfCanvasWidth, halfCanvasHeight, width, height);

            return calculatedPosition;
        }

        public static Vector2 ConvertWorldToAnchorPosition(Vector3 worldPosition, Camera mainCamera)
        {
            var screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            return screenPosition;
        }

        private static Vector2 ClampPosition(Vector2 position, float halfCanvasWidth, float halfCanvasHeight)
        {
            position.x = Mathf.Clamp(position.x, -halfCanvasWidth, halfCanvasWidth);

            position.y = Mathf.Clamp(position.y, -halfCanvasHeight, halfCanvasHeight);

            return position;
        }

        private static Vector2 AdjustForOverflow(
            Vector2 position, float halfCanvasWidth, float halfCanvasHeight, float width, float height)
        {
            var horizontalOverflow = CalculateOverflow(position.x, halfCanvasWidth, width, Vector2.right);

            var verticalOverflow = CalculateOverflow(position.y, halfCanvasHeight, height, Vector2.up);

            return horizontalOverflow + verticalOverflow;
        }

        private static Vector2 CalculateOverflow(float position, float halfCanvasSize, float spriteSize, Vector2 direction)
        {
            var overflowEdge = position + Mathf.Sign(position) * spriteSize * 0.5f;

            var reverseDirection = GetOverflowReverseDirection(overflowEdge, halfCanvasSize, direction);

            var overflowAmount = Mathf.Abs(Mathf.Abs(overflowEdge) - halfCanvasSize);

            return reverseDirection * overflowAmount;
        }

        private static Vector2 GetOverflowReverseDirection(float position, float boundary, Vector2 originalDirection)
        {
            if (position < -boundary) return originalDirection;

            if (position > boundary) return -originalDirection;

            return Vector2.zero;
        }

        public static Vector2 SetX(this Vector2 vector, float newX)
        {
            return new Vector2(newX, vector.y);
        }

        public static Vector2 SetY(this Vector2 vector, float newY)
        {
            return new Vector2(vector.x, newY);
        }

        public static Vector3 SetX(this Vector3 vector, float newX)
        {
            return new Vector3(newX, vector.y, vector.z);
        }

        public static Vector3 SetY(this Vector3 vector, float newY)
        {
            return new Vector3(vector.x, newY, vector.z);
        }

        public static Vector3 SetZ(this Vector3 vector, float newZ)
        {
            return new Vector3(vector.x, vector.y, newZ);
        }
    }
}