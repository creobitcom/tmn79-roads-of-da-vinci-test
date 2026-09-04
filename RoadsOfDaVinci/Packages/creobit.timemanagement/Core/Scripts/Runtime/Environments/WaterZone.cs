using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    [Serializable]
    public sealed class WaterSurfaceFxSettings
    {
        public static readonly WaterSurfaceFxSettings Default = new();

        public float RippleDistance = 0.45f;
        public float FootprintDistance = 0.28f;
        public float WetDuration = 4.0f;
        public float RippleDuration = 0.85f;
        public float FootprintDuration = 3.5f;
        public float RippleSize = 0.85f;
        public float FootprintSize = 0.46f;
        public float FootprintStride = 0.10f;
        public float IsoSquash = 0.58f;
        public Color RippleColor = new(0.85f, 0.95f, 1.0f, 0.90f);
        public Color FootprintColor = new(0.06f, 0.08f, 0.12f, 0.92f);
        public string SortingLayerName = "Gameplay";

        public bool DownwashEnabled = true;
        public float DownwashRadius = 0.95f;
        public Color DownwashColor = new(0.94f, 0.99f, 1.0f, 1.0f);
        public float DownwashFoamAlpha = 0.42f;
        public float DownwashPulseSpeed = 8f;
        public float DownwashPulseAmount = 0.10f;
        public float DownwashFadeDuration = 0.35f;
        public float DownwashRingInterval = 0.20f;
        public float DownwashRingSize = 2.2f;
        public float DownwashRingAlpha = 0.55f;
        public float DownwashRingDuration = 0.80f;
        public float DownwashSprayInterval = 0.09f;
        public int DownwashSprayCount = 2;
        public float DownwashSpraySize = 0.17f;
        public float DownwashSprayDuration = 0.40f;
        public Vector2 DownwashAnchorNudge = Vector2.zero;
    }

    public sealed class WaterZone : MonoBehaviour
    {
        [Title("Water Zone")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private bool _affectFlyingUnits = true;
        [SerializeField] private LayerMask _unitLayers = ~0;
        [SerializeField] private GameplayTagSO[] _ignoredTags;
        [SerializeField] private WaterSurfaceFxSettings _settings = new();

        private readonly HashSet<MovableObjectView> _unitsInside = new();
        private readonly HashSet<MovableObjectView> _unitsScratch = new();
        private readonly List<MovableObjectView> _toExit = new();
        private Collider[] _colliders3d = Array.Empty<Collider>();
        private Collider2D[] _colliders2d = Array.Empty<Collider2D>();

        public WaterSurfaceFxSettings Settings => _settings ?? WaterSurfaceFxSettings.Default;

        private void Reset()
        {
            foreach (var collider in GetComponents<Collider>())
            {
                collider.isTrigger = true;
            }
        }

        private void Awake()
        {
            CacheColliders();
        }

        private void OnEnable() => CacheColliders();

        private void OnDisable() => ClearZone();

        private void OnDestroy() => ClearZone();

        private void LateUpdate()
        {
            if (!_enabled)
            {
                if (_unitsInside.Count > 0)
                {
                    ClearZone();
                }

                return;
            }

            if (_colliders3d.Length == 0 && _colliders2d.Length == 0)
            {
                CacheColliders();
            }

            _unitsScratch.Clear();
            CollectUnitsInside(_unitsScratch);

            foreach (var unit in _unitsScratch)
            {
                if (_unitsInside.Add(unit))
                {
                    UnitWaterSurfaceFx.Of(unit)?.Enter(this);
                }
            }

            _toExit.Clear();
            foreach (var unit in _unitsInside)
            {
                if (unit == null || !_unitsScratch.Contains(unit))
                {
                    _toExit.Add(unit);
                }
            }

            for (var i = 0; i < _toExit.Count; i++)
            {
                var unit = _toExit[i];
                _unitsInside.Remove(unit);
                if (unit != null && unit.TryGetComponent(out UnitWaterSurfaceFx fx))
                {
                    fx.Exit(this);
                }
            }
        }

        [Button]
        public void SetEnabled(bool value)
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            if (!_enabled)
            {
                ClearZone();
            }
        }

        public void RelayEnter(Collider other)
        {
            var unit = other != null ? other.GetComponentInParent<MovableObjectView>() : null;
            if (unit == null || Ignores(unit))
            {
                return;
            }

            if (_unitsInside.Add(unit))
            {
                UnitWaterSurfaceFx.Of(unit)?.Enter(this);
            }
        }

        public void RelayExit(Collider other)
        {
            var unit = other != null ? other.GetComponentInParent<MovableObjectView>() : null;
            if (unit == null || !_unitsInside.Remove(unit))
            {
                return;
            }

            if (unit.TryGetComponent(out UnitWaterSurfaceFx fx))
            {
                fx.Exit(this);
            }
        }

        public bool Ignores(MovableObjectView unit)
        {
            if (unit == null)
            {
                return true;
            }

            if (!_affectFlyingUnits && UnitWaterSurfaceFx.IsFlyingUnit(unit))
            {
                return true;
            }

            if (_ignoredTags == null || _ignoredTags.Length == 0 || unit.ObjectDataSO == null)
            {
                return false;
            }

            return ContainsAny(_ignoredTags, unit.ObjectDataSO.ObjectTypeTags)
                   || ContainsAny(_ignoredTags, unit.MovableObjectDataSO != null ? unit.MovableObjectDataSO.BaseTypeTags : null);
        }

        private void CollectUnitsInside(HashSet<MovableObjectView> result)
        {
            if (!TryGetQueryBounds(out var center, out var halfExtents))
            {
                return;
            }

            var hits = WaterOverlapBuffer.Overlap(center, halfExtents, _unitLayers);
            for (var i = 0; i < hits.Count; i++)
            {
                var unit = WaterOverlapBuffer.ResolveUnit(hits[i]);
                if (unit == null || Ignores(unit))
                {
                    continue;
                }

                if (ContainsXy(unit.transform.position))
                {
                    result.Add(unit);
                }
            }
        }

        private bool TryGetQueryBounds(out Vector3 center, out Vector3 halfExtents)
        {
            var has = false;
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;

            for (var i = 0; i < _colliders3d.Length; i++)
            {
                var col = _colliders3d[i];
                if (col == null || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (col is BoxCollider box)
                {
                    EncapsulateBox(box, ref min, ref max);
                    has = true;
                    continue;
                }

                if (col is SphereCollider sphere)
                {
                    var sphereCenter = sphere.transform.TransformPoint(sphere.center);
                    var radius = sphere.radius * MaxAbs(sphere.transform.lossyScale);
                    min = Vector3.Min(min, sphereCenter - new Vector3(radius, radius, 0f));
                    max = Vector3.Max(max, sphereCenter + new Vector3(radius, radius, 0f));
                    has = true;
                    continue;
                }

                var bounds = col.bounds;
                if (bounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                min = Vector3.Min(min, bounds.min);
                max = Vector3.Max(max, bounds.max);
                has = true;
            }

            for (var i = 0; i < _colliders2d.Length; i++)
            {
                var col = _colliders2d[i];
                if (col == null || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var bounds = col.bounds;
                if (bounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                min = Vector3.Min(min, bounds.min);
                max = Vector3.Max(max, bounds.max);
                has = true;
            }

            if (!has)
            {
                center = Vector3.zero;
                halfExtents = Vector3.zero;
                return false;
            }

            center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, transform.position.z);
            halfExtents = new Vector3(
                Mathf.Max((max.x - min.x) * 0.5f, 0.01f),
                Mathf.Max((max.y - min.y) * 0.5f, 0.01f),
                WaterOverlapBuffer.DepthHalfExtent);
            return true;
        }

        private static void EncapsulateBox(BoxCollider box, ref Vector3 min, ref Vector3 max)
        {
            var half = box.size * 0.5f;
            var matrix = box.transform.localToWorldMatrix;

            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z) + box.center;

                var world = matrix.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }
        }

        private void CacheColliders()
        {
            _colliders3d = GetComponentsInChildren<Collider>(true);
            _colliders2d = GetComponentsInChildren<Collider2D>(true);

            if (!Application.isPlaying)
            {
                return;
            }

            for (var i = 0; i < _colliders3d.Length; i++)
            {
                var col = _colliders3d[i];
                if (col == null)
                {
                    continue;
                }

                col.isTrigger = true;
                col.enabled = false;
            }
        }

        private bool ContainsXy(Vector3 worldPos)
        {
            for (var i = 0; i < _colliders3d.Length; i++)
            {
                var col = _colliders3d[i];
                if (col == null || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (col is BoxCollider box)
                {
                    if (ContainsBoxXy(box, worldPos))
                    {
                        return true;
                    }

                    continue;
                }

                if (col is SphereCollider sphere)
                {
                    if (ContainsSphereXy(sphere, worldPos))
                    {
                        return true;
                    }

                    continue;
                }

                if (ContainsBoundsXy(col.bounds, worldPos))
                {
                    return true;
                }
            }

            var point2d = new Vector2(worldPos.x, worldPos.y);
            for (var i = 0; i < _colliders2d.Length; i++)
            {
                var col = _colliders2d[i];
                if (col == null || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (col.OverlapPoint(point2d))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsBoxXy(BoxCollider box, Vector3 worldPos)
        {
            var local = box.transform.InverseTransformPoint(worldPos) - box.center;
            var half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }

        private static bool ContainsSphereXy(SphereCollider sphere, Vector3 worldPos)
        {
            var center = sphere.transform.TransformPoint(sphere.center);
            var dx = worldPos.x - center.x;
            var dy = worldPos.y - center.y;
            var radius = sphere.radius * MaxAbs(sphere.transform.lossyScale);
            return dx * dx + dy * dy <= radius * radius;
        }

        private static bool ContainsBoundsXy(Bounds bounds, Vector3 worldPos)
        {
            return worldPos.x >= bounds.min.x && worldPos.x <= bounds.max.x
                   && worldPos.y >= bounds.min.y && worldPos.y <= bounds.max.y;
        }

        private static float MaxAbs(Vector3 scale)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }

        private void ClearZone()
        {
            foreach (var unit in _unitsInside)
            {
                if (unit != null && unit.TryGetComponent(out UnitWaterSurfaceFx fx))
                {
                    fx.Exit(this);
                }
            }

            _unitsInside.Clear();
        }

        private static bool ContainsAny(GameplayTagSO[] ignored, GameplayTagSO[] tags)
        {
            if (ignored == null || tags == null)
            {
                return false;
            }

            for (var i = 0; i < ignored.Length; i++)
            {
                var ignoredTag = ignored[i];
                if (ignoredTag == null)
                {
                    continue;
                }

                for (var j = 0; j < tags.Length; j++)
                {
                    if (ignoredTag.Equals(tags[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var color = _enabled ? new Color(0.25f, 0.75f, 0.85f, 0.18f) : new Color(0.5f, 0.5f, 0.5f, 0.12f);
            Gizmos.color = color;

            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                DrawColliderGizmo(collider, false);
            }

            color.a = 0.9f;
            Gizmos.color = color;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                DrawColliderGizmo(collider, true);
            }
        }

        private static void DrawColliderGizmo(Collider collider, bool wire)
        {
            if (collider is not BoxCollider box)
            {
                return;
            }

            Gizmos.matrix = collider.transform.localToWorldMatrix;
            if (wire)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else
            {
                Gizmos.DrawCube(box.center, box.size);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
