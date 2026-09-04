using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    public sealed class WaterPathNode : MonoBehaviour
    {
        private static readonly List<WaterPathNode> ActiveNodes = new();

        [SerializeField] private float _radius = 1.0f;
        [SerializeField] private LayerMask _unitLayers = ~0;
        [SerializeField] private WaterSurfaceFxSettings _customSettings = new();

        public float Radius => _radius;
        public WaterSurfaceFxSettings Settings => _customSettings ?? WaterSurfaceFxSettings.Default;

        public static IReadOnlyList<WaterPathNode> AllActiveNodes => ActiveNodes;

        private void OnEnable()
        {
            if (!ActiveNodes.Contains(this))
            {
                ActiveNodes.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveNodes.Remove(this);
        }

        private void LateUpdate()
        {
            if (ActiveNodes.Count == 0 || ActiveNodes[0] != this)
            {
                return;
            }

            AttachFxToUnitsNearNodes();
        }

        public bool Contains(Vector3 position)
        {
            var nodePos = transform.position;
            var dx = position.x - nodePos.x;
            var squash = ResolveSquash();
            var dy = (position.y - nodePos.y) / squash;
            return (dx * dx + dy * dy) <= _radius * _radius;
        }

        public static bool IsAnyInWater(Vector3 position, out WaterSurfaceFxSettings settings)
        {
            for (var i = ActiveNodes.Count - 1; i >= 0; i--)
            {
                var node = ActiveNodes[i];
                if (node == null)
                {
                    ActiveNodes.RemoveAt(i);
                    continue;
                }

                if (node.Contains(position))
                {
                    settings = node.Settings;
                    return true;
                }
            }

            settings = WaterSurfaceFxSettings.Default;
            return false;
        }

        private static void AttachFxToUnitsNearNodes()
        {
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            var layers = 0;
            var has = false;

            for (var i = ActiveNodes.Count - 1; i >= 0; i--)
            {
                var node = ActiveNodes[i];
                if (node == null)
                {
                    ActiveNodes.RemoveAt(i);
                    continue;
                }

                var position = node.transform.position;
                var extent = new Vector3(node._radius, node._radius, 0f);
                min = Vector3.Min(min, position - extent);
                max = Vector3.Max(max, position + extent);
                layers |= node._unitLayers.value;
                has = true;
            }

            if (!has)
            {
                return;
            }

            var center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
            var halfExtents = new Vector3(
                Mathf.Max((max.x - min.x) * 0.5f, 0.01f),
                Mathf.Max((max.y - min.y) * 0.5f, 0.01f),
                WaterOverlapBuffer.DepthHalfExtent);

            var hits = WaterOverlapBuffer.Overlap(center, halfExtents, layers);
            for (var i = 0; i < hits.Count; i++)
            {
                var unit = WaterOverlapBuffer.ResolveUnit(hits[i]);
                if (unit == null || UnitWaterSurfaceFx.IsFlyingUnit(unit))
                {
                    continue;
                }

                if (IsAnyInWater(unit.transform.position, out _))
                {
                    UnitWaterSurfaceFx.Of(unit);
                }
            }
        }

        private float ResolveSquash()
        {
            var squash = Settings.IsoSquash;
            return squash > 0.05f ? squash : 0.58f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            DrawWireEllipse(transform.position, _radius, _radius * ResolveSquash(), 24);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.95f, 1f, 0.9f);
            DrawWireEllipse(transform.position, _radius, _radius * ResolveSquash(), 32);
        }

        private static void DrawWireEllipse(Vector3 center, float radiusX, float radiusY, int segments)
        {
            var prev = center + new Vector3(radiusX, 0f, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var angle = (i / (float)segments) * Mathf.PI * 2f;
                var next = center + new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
