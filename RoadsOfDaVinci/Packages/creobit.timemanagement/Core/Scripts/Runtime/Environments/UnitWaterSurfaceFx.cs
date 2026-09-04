using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using UnityEngine;
using UnityEngine.Rendering;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    public sealed class UnitWaterSurfaceFx : MonoBehaviour
    {
        private const float TeleportSqr = 9f;
        private const float MinMoveSqr = 0.0004f;
        private const string DroneTagFragment = "drone";
        private const string ShadowNameFragment = "Shadow";
        private const string ShadowOffsetProperty = "_ShadowOffset";

        private readonly List<WaterZone> _zones = new();

        private MovableObjectView _unit;
        private SortingGroup _sortingGroup;
        private bool _sortingGroupResolved;
        private Renderer _shadowRenderer;
        private bool _shadowResolved;
        private bool _isFlying;
        private WaterSurfaceDecal _foamDisc;
        private float _downwashFade;
        private float _ringTimer;
        private float _sprayTimer;
        private float _wetness;
        private Vector3 _lastPosition;
        private Vector3 _lastStepPosition;
        private float _idleTimer;
        private int _footSide = 1;
        private bool _wasInWater;
        private WaterSurfaceFxSettings _lastActiveSettings;

        public static bool IsFlyingUnit(MovableObjectView unit)
        {
            if (unit == null || unit.ObjectDataSO == null)
            {
                return false;
            }

            if (HasDroneTag(unit.ObjectDataSO.ObjectTypeTags))
            {
                return true;
            }

            return unit.MovableObjectDataSO != null && HasDroneTag(unit.MovableObjectDataSO.BaseTypeTags);
        }

        public static UnitWaterSurfaceFx Of(MovableObjectView unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (!unit.TryGetComponent(out UnitWaterSurfaceFx fx))
            {
                fx = unit.gameObject.AddComponent<UnitWaterSurfaceFx>();
            }

            fx._unit ??= unit;
            return fx;
        }

        public void Enter(WaterZone zone)
        {
            if (zone == null)
            {
                return;
            }

            _unit ??= GetComponent<MovableObjectView>();

            if (!_zones.Contains(zone))
            {
                _zones.Add(zone);
            }
        }

        public void Exit(WaterZone zone)
        {
            if (zone == null)
            {
                return;
            }

            _zones.Remove(zone);
        }

        private void Awake()
        {
            _unit ??= GetComponent<MovableObjectView>();
            _isFlying = IsFlyingUnit(_unit);
            _lastPosition = transform.position;
            _lastStepPosition = _lastPosition;
            ResolveSortingGroup();
        }

        private void OnDisable()
        {
            _zones.Clear();
            _wetness = 0f;
            _wasInWater = false;
            StopDownwash();
        }

        private void Update()
        {
            if (_unit == null)
            {
                return;
            }

            if (_unit.IsPaused || _unit.IsLevelEndFrozen || _unit.IsHeldByTransport || !IsRenderable())
            {
                _lastPosition = transform.position;
                return;
            }

            var position = transform.position;
            var delta = position - _lastPosition;
            if (delta.sqrMagnitude > TeleportSqr)
            {
                _lastPosition = position;
                _lastStepPosition = position;
                _idleTimer = 0.4f;
                return;
            }

            if (_isFlying)
            {
                UpdateDownwash();
                _lastPosition = position;
                return;
            }


            var moving = IsMoving(delta);
            var inNodeWater = WaterPathNode.IsAnyInWater(position, out var nodeSettings);
            var zoneSettings = ResolveZoneSettings();
            var inWater = zoneSettings != null || inNodeWater;
            var settings = zoneSettings ?? (inNodeWater ? nodeSettings : _lastActiveSettings ?? WaterSurfaceFxSettings.Default);

            if (inWater)
            {
                _lastActiveSettings = settings;
                _wetness = 1f;
                if (!_wasInWater)
                {
                    _wasInWater = true;
                    SpawnRippleBurst(position, moving ? 1.2f : 0.8f, settings);
                    _idleTimer = 0.45f;
                }

                if (moving)
                {
                    StepWhileInWater(position, settings);
                    _idleTimer = 0.45f;
                }
                else
                {
                    _idleTimer -= Time.deltaTime;
                    if (_idleTimer <= 0f)
                    {
                        SpawnRipple(position, 0.72f, settings);
                        _idleTimer = RandomRange(0.85f, 1.35f);
                    }
                }
            }
            else
            {
                if (_wasInWater)
                {
                    _wasInWater = false;
                    _lastStepPosition = position;
                    if (moving)
                    {
                        SpawnFootprint(position, delta, settings);
                    }
                }

                if (_wetness > 0f)
                {
                    if (moving)
                    {
                        StepWhileWet(position, delta, settings);
                    }

                    _wetness = Mathf.MoveTowards(_wetness, 0f, Time.deltaTime / settings.WetDuration);
                }
            }

            _lastPosition = position;
        }

        private void UpdateDownwash()
        {
            var anchor = ResolveSurfaceAnchor();
            var zoneSettings = ResolveZoneSettings();
            var settings = zoneSettings ?? (WaterPathNode.IsAnyInWater(anchor, out var nodeSettings) ? nodeSettings : null);

            if (settings == null || !settings.DownwashEnabled)
            {
                StopDownwash();
                return;
            }

            _lastActiveSettings = settings;
            anchor += new Vector3(settings.DownwashAnchorNudge.x, settings.DownwashAnchorNudge.y, 0f);

            _downwashFade = Mathf.MoveTowards(_downwashFade, 1f, Time.deltaTime / Mathf.Max(0.05f, settings.DownwashFadeDuration));
            UpdateFoamDisc(anchor, settings);

            _ringTimer -= Time.deltaTime;
            if (_ringTimer <= 0f)
            {
                _ringTimer = Mathf.Max(0.05f, settings.DownwashRingInterval);
                SpawnDownwashRing(anchor, settings);
            }

            _sprayTimer -= Time.deltaTime;
            if (_sprayTimer <= 0f)
            {
                _sprayTimer = Mathf.Max(0.02f, settings.DownwashSprayInterval);
                SpawnDownwashSpray(anchor, settings);
            }
        }

        private void UpdateFoamDisc(Vector3 anchor, WaterSurfaceFxSettings settings)
        {
            var pulse = 1f + Mathf.Sin(Time.time * settings.DownwashPulseSpeed) * settings.DownwashPulseAmount;
            var width = settings.DownwashRadius * 2f * pulse;
            var scale = new Vector3(width, width * settings.IsoSquash, 1f);

            var color = settings.DownwashColor;
            color.a *= settings.DownwashFoamAlpha * _downwashFade;

            ResolveSorting(anchor, 0, settings, out var layer, out var order);

            if (_foamDisc == null)
            {
                _foamDisc = WaterSurfaceDecal.Hold(WaterSurfaceDecalKind.Downwash, anchor, scale, color, layer, order);
                return;
            }

            _foamDisc.UpdateHeld(anchor, scale, color);
        }

        private void StopDownwash()
        {
            _ringTimer = 0f;
            _sprayTimer = 0f;
            _downwashFade = 0f;

            if (_foamDisc == null)
            {
                return;
            }

            _foamDisc.ReleaseHeld(_lastActiveSettings != null ? _lastActiveSettings.DownwashFadeDuration : 0.35f);
            _foamDisc = null;
        }

        private void SpawnDownwashRing(Vector3 anchor, WaterSurfaceFxSettings settings)
        {
            var squash = settings.IsoSquash;
            var startWidth = settings.DownwashRadius * 1.1f;
            var endWidth = settings.DownwashRadius * settings.DownwashRingSize * RandomRange(0.92f, 1.08f);

            var color = settings.DownwashColor;
            color.a *= settings.DownwashRingAlpha * _downwashFade;

            ResolveSorting(anchor, 1, settings, out var layer, out var order);
            WaterSurfaceDecal.Play(
                WaterSurfaceDecalKind.Ripple,
                anchor,
                Quaternion.identity,
                new Vector3(startWidth, startWidth * squash, 1f),
                new Vector3(endWidth, endWidth * squash, 1f),
                color,
                settings.DownwashRingDuration * RandomRange(0.9f, 1.1f),
                layer,
                order);
        }

        private void SpawnDownwashSpray(Vector3 anchor, WaterSurfaceFxSettings settings)
        {
            var squash = settings.IsoSquash;
            var count = Mathf.Max(1, settings.DownwashSprayCount);

            for (var i = 0; i < count; i++)
            {
                var angle = RandomRange(0f, Mathf.PI * 2f);
                var distance = settings.DownwashRadius * RandomRange(0.55f, 1.05f);
                var pos = new Vector3(
                    anchor.x + Mathf.Cos(angle) * distance,
                    anchor.y + Mathf.Sin(angle) * distance * squash,
                    0f);

                var size = settings.DownwashSpraySize * RandomRange(0.7f, 1.3f);
                var color = settings.DownwashColor;
                color.a *= RandomRange(0.55f, 0.95f) * _downwashFade;

                ResolveSorting(pos, 2, settings, out var layer, out var order);
                WaterSurfaceDecal.Play(
                    WaterSurfaceDecalKind.Downwash,
                    pos,
                    Quaternion.identity,
                    new Vector3(size * 0.5f, size * 0.5f * squash, 1f),
                    new Vector3(size, size * squash, 1f),
                    color,
                    settings.DownwashSprayDuration * RandomRange(0.8f, 1.2f),
                    layer,
                    order);
            }
        }

        private Vector3 ResolveSurfaceAnchor()
        {
            ResolveShadowRenderer();

            if (_shadowRenderer == null)
            {
                return transform.position;
            }

            var anchor = _shadowRenderer.transform.position;
            var material = _shadowRenderer.sharedMaterial;

            if (material != null && material.HasProperty(ShadowOffsetProperty))
            {
                var offset = material.GetVector(ShadowOffsetProperty);
                anchor.x += offset.x;
                anchor.y += offset.y;
            }

            anchor.z = 0f;
            return anchor;
        }

        private void ResolveShadowRenderer()
        {
            if (_shadowResolved)
            {
                return;
            }

            _shadowResolved = true;
            if (_unit == null)
            {
                return;
            }

            var renderers = _unit.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer != null
                    && renderer.gameObject.name.IndexOf(ShadowNameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _shadowRenderer = renderer;
                    return;
                }
            }
        }

        private WaterSurfaceFxSettings ResolveZoneSettings()
        {
            for (var i = _zones.Count - 1; i >= 0; i--)
            {
                var zone = _zones[i];
                if (zone == null)
                {
                    _zones.RemoveAt(i);
                    continue;
                }

                return zone.Settings;
            }

            return null;
        }

        private void StepWhileInWater(Vector3 position, WaterSurfaceFxSettings settings)
        {
            if ((position - _lastStepPosition).magnitude < settings.RippleDistance)
            {
                return;
            }

            SpawnRipple(position, 1f, settings);
            _lastStepPosition = position;
        }

        private void StepWhileWet(Vector3 position, Vector3 delta, WaterSurfaceFxSettings settings)
        {
            if ((position - _lastStepPosition).magnitude < settings.FootprintDistance)
            {
                return;
            }

            SpawnFootprint(position, delta, settings);
            _lastStepPosition = position;
        }

        private void SpawnRippleBurst(Vector3 position, float strength, WaterSurfaceFxSettings settings)
        {
            SpawnRipple(position, 0.85f * strength, settings);
            SpawnRipple(position + new Vector3(0.04f, -0.03f, 0f), 1.2f * strength, settings);
        }

        private void SpawnRipple(Vector3 position, float strength, WaterSurfaceFxSettings settings)
        {
            var squash = settings.IsoSquash;
            var size = settings.RippleSize * strength * RandomRange(0.92f, 1.08f);
            var start = new Vector3(size * 0.28f, size * 0.28f * squash, 1f);
            var end = new Vector3(size, size * squash, 1f);
            var color = settings.RippleColor;
            color.a *= Mathf.Clamp01(0.75f + strength * 0.25f);
            var pos = new Vector3(
                position.x + RandomRange(-0.04f, 0.04f),
                position.y + RandomRange(-0.03f, 0.03f),
                0f);

            ResolveSorting(pos, 0, settings, out var layer, out var order);
            WaterSurfaceDecal.Play(
                WaterSurfaceDecalKind.Ripple,
                pos,
                Quaternion.identity,
                start,
                end,
                color,
                settings.RippleDuration * RandomRange(0.9f, 1.1f),
                layer,
                order);
        }

        private void SpawnFootprint(Vector3 position, Vector3 delta, WaterSurfaceFxSettings settings)
        {
            var dir = new Vector3(delta.x, delta.y, 0f);
            if (dir.sqrMagnitude < MinMoveSqr)
            {
                return;
            }

            dir.Normalize();
            var perp = new Vector3(-dir.y, dir.x, 0f);
            var side = _footSide;
            _footSide = -_footSide;

            var offset = perp * (settings.FootprintStride * side) - dir * 0.06f;
            var pos = new Vector3(position.x + offset.x, position.y + offset.y, 0f);

            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            var rotation = Quaternion.Euler(0f, 0f, angle + RandomRange(-7f, 7f));
            var squash = settings.IsoSquash;
            var size = settings.FootprintSize * Mathf.Lerp(0.85f, 1f, _wetness);
            var scale = new Vector3(size * side, size * squash, 1f);
            var color = settings.FootprintColor;
            color.a *= Mathf.Clamp01(_wetness);

            ResolveSorting(pos, 0, settings, out var layer, out var order);
            WaterSurfaceDecal.Play(
                WaterSurfaceDecalKind.Footprint,
                pos,
                rotation,
                scale,
                scale * 0.94f,
                color,
                settings.FootprintDuration,
                layer,
                order);
        }

        private bool IsRenderable()
        {
            if (_unit.modelTransform != null && !_unit.modelTransform.gameObject.activeInHierarchy)
            {
                return false;
            }

            return _unit.gameObject.activeInHierarchy;
        }

        private static bool HasDroneTag(GameplayTagSO[] tags)
        {
            if (tags == null)
            {
                return false;
            }

            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                if (tag != null && !string.IsNullOrEmpty(tag.TagName)
                    && tag.TagName.IndexOf(DroneTagFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMoving(Vector3 delta)
        {
            return delta.sqrMagnitude > MinMoveSqr;
        }

        private void ResolveSortingGroup()
        {
            if (_sortingGroupResolved)
            {
                return;
            }

            _sortingGroupResolved = true;
            if (_unit == null)
            {
                return;
            }

            if (!_unit.TryGetComponent(out _sortingGroup))
            {
                _sortingGroup = _unit.GetComponentInChildren<SortingGroup>(true);
            }
        }

        private void ResolveSorting(Vector3 position, int bias, WaterSurfaceFxSettings settings, out string layer, out int order)
        {
            if (_isFlying)
            {
                ResolveShadowRenderer();
                if (_shadowRenderer != null)
                {
                    layer = _shadowRenderer.sortingLayerName;
                    order = _shadowRenderer.sortingOrder + 1 + bias;
                    return;
                }
            }

            ResolveSortingGroup();

            if (!_isFlying && _sortingGroup != null)
            {
                layer = _sortingGroup.sortingLayerName;
                order = _sortingGroup.sortingOrder - 2 + bias;
                return;
            }

            layer = string.IsNullOrEmpty(settings.SortingLayerName) || settings.SortingLayerName == "Environment"
                ? "Gameplay"
                : settings.SortingLayerName;
            order = 8 + Mathf.RoundToInt(-position.y * 4f) + bias;
        }

        private static float RandomRange(float min, float max)
        {
            return Random.Range(min, max);
        }
    }
}
