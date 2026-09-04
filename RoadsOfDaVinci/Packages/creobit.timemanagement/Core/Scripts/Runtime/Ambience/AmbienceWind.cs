using System;
using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    public sealed class AmbienceWind
    {
        private static readonly int WindId = Shader.PropertyToID("_TMNWind");
        private static readonly int WindGustId = Shader.PropertyToID("_TMNWindGust");

        private readonly List<Transform> _swayTargets = new List<Transform>();
        private readonly List<Quaternion> _swayBaseRotations = new List<Quaternion>();
        private readonly List<Vector3> _swayBasePositions = new List<Vector3>();
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();

        private float _gustTime;

        public Vector2 Direction { get; private set; } = Vector2.right;
        public float Gust { get; private set; }
        public float Strength { get; private set; }

        public Vector2 Velocity => Direction * (Strength * (1f + Gust));

        public void Tick(AmbienceWindSettings settings, float deltaTime)
        {
            if (settings == null)
            {
                return;
            }

            var radians = settings.direction * Mathf.Deg2Rad;
            Direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Strength = settings.apply ? settings.strength : 0f;

            if (settings.apply && settings.gustAmount > 0.001f)
            {
                _gustTime += deltaTime;
                var interval = Mathf.Max(settings.gustInterval, 0.2f);
                var duration = Mathf.Min(Mathf.Max(settings.gustDuration, 0.1f), interval);
                var phase = _gustTime % interval;
                Gust = phase < duration
                    ? Mathf.Sin(Mathf.PI * phase / duration) * settings.gustAmount
                    : 0f;
            }
            else
            {
                Gust = 0f;
            }

            Shader.SetGlobalVector(WindId, new Vector4(Direction.x, Direction.y, Strength, Mathf.Max(settings.frequency, 0.01f)));
            Shader.SetGlobalFloat(WindGustId, Gust);
        }

        public void RescanSway(Transform root, AmbienceWindSettings settings)
        {
            ResetSway();

            if (root == null || settings == null || !settings.swayTransforms)
            {
                return;
            }

            root.GetComponentsInChildren(true, _renderers);

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || !NameContainsAny(renderer.gameObject.name, settings.swayNameParts))
                {
                    continue;
                }

                var target = renderer.transform;
                _swayTargets.Add(target);
                _swayBaseRotations.Add(target.localRotation);
                _swayBasePositions.Add(target.localPosition);
            }

            _renderers.Clear();
        }

        public void ApplySway(AmbienceWindSettings settings, float time)
        {
            if (settings == null || !settings.swayTransforms || _swayTargets.Count == 0)
            {
                return;
            }

            var amplitude = (1f + Gust) * Strength;
            var frequency = Mathf.Max(settings.frequency, 0.01f);

            for (var i = 0; i < _swayTargets.Count; i++)
            {
                var target = _swayTargets[i];
                if (target == null)
                {
                    continue;
                }

                var basePosition = _swayBasePositions[i];
                var phase = basePosition.x * 0.83f + basePosition.y * 0.37f;
                var wave = Mathf.Sin(time * frequency * Mathf.PI * 2f + phase) * 0.65f
                           + Mathf.Sin(time * frequency * 3.1f + phase * 1.7f) * 0.35f;

                var angle = -wave * settings.swayAngle * amplitude * Mathf.Sign(Direction.x == 0f ? 1f : Direction.x);
                target.localRotation = _swayBaseRotations[i] * Quaternion.Euler(0f, 0f, angle);
                target.localPosition = basePosition + new Vector3(Direction.x, Direction.y, 0f) * (wave * settings.swayOffset * amplitude * 0.1f);
            }
        }

        public void ResetSway()
        {
            for (var i = 0; i < _swayTargets.Count; i++)
            {
                var target = _swayTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.localRotation = _swayBaseRotations[i];
                target.localPosition = _swayBasePositions[i];
            }

            _swayTargets.Clear();
            _swayBaseRotations.Clear();
            _swayBasePositions.Clear();
        }

        private static bool NameContainsAny(string name, string[] parts)
        {
            if (parts == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
