using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class ProceduralTail : MaskableGraphic
    {
        [SerializeField] private float _baseWidth = 30f;
        [SerializeField] private Vector2 _targetPoint;
        [SerializeField] private Vector2 _baseOffset = Vector2.zero;

        public float BaseWidth { get => _baseWidth; set => _baseWidth = value; }
        public Vector2 TargetPoint { get => _targetPoint; set => _targetPoint = value; }
        public Vector2 BaseOffset { get => _baseOffset; set => _baseOffset = value; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var v = UIVertex.simpleVert;
            v.color = color;

            v.position = new Vector3(-_baseWidth / 2f + _baseOffset.x, _baseOffset.y);
            vh.AddVert(v);

            v.position = new Vector3(_baseWidth / 2f + _baseOffset.x, _baseOffset.y);
            vh.AddVert(v);

            v.position = _targetPoint;
            vh.AddVert(v);

            vh.AddTriangle(0, 1, 2);
        }
    }
}