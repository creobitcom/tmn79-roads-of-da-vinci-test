using UnityEngine;
#if TK2D
namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(tk2dCameraAnchor))]
    public class SafeAreaMover : MonoBehaviour
    {
        private Rect _lastSafeArea;
        private tk2dCameraAnchor _tk2DCameraAnchor;
        
#if (UNITY_IOS || UNITY_ANDROID) && TK2D
        private void Start()
        {
            _tk2DCameraAnchor = GetComponent<tk2dCameraAnchor>();
            UpdateAnchorOffsetPixels();
        }
        
        private void FixedUpdate()
        {
            if (_lastSafeArea == Screen.safeArea)
                return;
            
            UpdateAnchorOffsetPixels();
        }
#endif
        
        
        private void UpdateAnchorOffsetPixels()
        {
            _lastSafeArea = Screen.safeArea;

            _tk2DCameraAnchor.AnchorOffsetPixels = _tk2DCameraAnchor.AnchorPoint switch
            {
                tk2dBaseSprite.Anchor.MiddleLeft => new Vector2(Screen.safeArea.min.x, 0f),
                tk2dBaseSprite.Anchor.MiddleRight => new Vector2( Screen.safeArea.max.x - Screen.width, 0f),
                tk2dBaseSprite.Anchor.UpperRight => new Vector2( Screen.safeArea.max.x - Screen.width, Screen.safeArea.max.y - Screen.height),
                tk2dBaseSprite.Anchor.UpperLeft => new Vector2( Screen.safeArea.min.x, Screen.safeArea.max.y - Screen.height),
                tk2dBaseSprite.Anchor.LowerRight => new Vector2( Screen.safeArea.max.x - Screen.width, Screen.safeArea.min.y),
                tk2dBaseSprite.Anchor.LowerLeft => new Vector2( Screen.safeArea.min.x, Screen.safeArea.min.y),
                _ => _tk2DCameraAnchor.AnchorOffsetPixels
            };
        }
    }
}
#endif