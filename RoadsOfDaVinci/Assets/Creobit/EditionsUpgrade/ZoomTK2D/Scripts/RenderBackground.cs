using UnityEngine;
using System.Collections;

namespace TimeManagerEngine
{

    public class RenderBackground : MonoBehaviour
    {
#if TK2D
        public tk2dCamera mainCamera;
        public Transform quadTransform;
        public GameObject standaloneBackground;

        void OnEnable()
        {
#if UNITY_ANDROID || UNITY_IOS
            standaloneBackground.SetActive(false);

            Vector3 scale = quadTransform.localScale;
            scale.x = scale.y * mainCamera.TargetResolution.x / mainCamera.TargetResolution.y;
            quadTransform.localScale = scale;
#else
		gameObject.SetActive(false);
#endif
        }
#endif
    }

}