#if TK2D
namespace Creobit.EditionsUpgrade
{

    using UnityEngine;
    using System.Collections;

    public class BoundingBox2D : MonoBehaviour
    {
        public Bounds BoundingBox { get { return m_BoundingBox; } }

        [HideInInspector]
        [SerializeField] private Bounds m_BoundingBox;

        [SerializeField] private Vector2 m_Size;

        [SerializeField]
        private tk2dCamera mainCamera;

        [SerializeField]
        private Transform backgroundTransform;

        void Awake()
        {
#if UNITY_ANDROID || UNITY_IOS
            Vector3 scale = backgroundTransform.localScale;
            scale.x = scale.y * mainCamera.TargetResolution.x / mainCamera.TargetResolution.y;
            m_Size.Set(scale.x, scale.y);
#endif
            m_BoundingBox = new Bounds(gameObject.transform.position, m_Size);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(gameObject.transform.position, m_Size);
        }
    }
}
#endif