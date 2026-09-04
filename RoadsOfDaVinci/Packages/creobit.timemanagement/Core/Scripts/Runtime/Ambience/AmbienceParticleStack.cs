using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    public sealed class AmbienceParticleStack
    {
        private const string ShaderName = "TMN/AmbienceParticle";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int UseTextureId = Shader.PropertyToID("_UseTexture");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int HardnessId = Shader.PropertyToID("_Hardness");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        private sealed class LayerView
        {
            public Transform Root;
            public ParticleSystem System;
            public ParticleSystemRenderer Renderer;
            public Material Material;
            public int ConfigHash;
            public bool PreviewPrimed;
        }

        private readonly List<LayerView> _views = new List<LayerView>();
        private Transform _parent;

        public Transform Root => _parent;

        public int LayerCount => _views.Count;

        public void Rebuild(Transform owner, List<AmbienceParticleLayerSettings> layers)
        {
            Clear();

            var shader = Shader.Find(ShaderName);
            if (shader == null || owner == null || layers == null)
            {
                return;
            }

            var parentObject = new GameObject("AmbienceParticles");
            if (!Application.isPlaying)
            {
                parentObject.hideFlags = HideFlags.HideAndDontSave;
            }

            _parent = parentObject.transform;
            _parent.SetParent(owner, false);

            for (var i = 0; i < layers.Count; i++)
            {
                var settings = layers[i];
                if (settings == null)
                {
                    continue;
                }

                var layerObject = new GameObject("Particle_" + (string.IsNullOrEmpty(settings.layerName) ? "Layer" : settings.layerName));
                var layerTransform = layerObject.transform;
                layerTransform.SetParent(_parent, false);

                var system = layerObject.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var renderer = layerObject.GetComponent<ParticleSystemRenderer>();
                var material = new Material(shader)
                {
                    name = layerObject.name,
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderer.sharedMaterial = material;

                _views.Add(new LayerView
                {
                    Root = layerTransform,
                    System = system,
                    Renderer = renderer,
                    Material = material,
                    ConfigHash = 0
                });
            }
        }

        public void ApplySettings(List<AmbienceParticleLayerSettings> layers, Vector2 windVelocity, float densityScale, float speedScale)
        {
            ApplySettings(layers, windVelocity, densityScale, speedScale, null);
        }

        public void ApplySettings(List<AmbienceParticleLayerSettings> layers, Vector2 windVelocity, float densityScale, float speedScale, Vector2? fitSize)
        {
            if (layers == null)
            {
                return;
            }

            for (var i = 0; i < _views.Count && i < layers.Count; i++)
            {
                var view = _views[i];
                var settings = layers[i];
                if (view == null || settings == null || view.System == null)
                {
                    continue;
                }

                var isEnabled = settings.enabled && settings.rate * densityScale > 0.01f;
                if (view.Renderer.enabled != isEnabled)
                {
                    view.Renderer.enabled = isEnabled;
                }

                if (!isEnabled)
                {
                    if (view.System.isPlaying)
                    {
                        view.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }

                    continue;
                }

                var configHash = ComputeConfigHash(settings);
                unchecked
                {
                    configHash = configHash * 31 + densityScale.GetHashCode();
                    configHash = configHash * 31 + speedScale.GetHashCode();
                }

                if (configHash != view.ConfigHash)
                {
                    view.ConfigHash = configHash;
                    view.PreviewPrimed = false;
                    Configure(view, settings, densityScale, speedScale);
                }

                if (settings.anchor != null)
                {
                    view.Root.position = settings.anchor.position
                                         + new Vector3(settings.areaOffset.x, settings.areaOffset.y, settings.depth);
                }
                else
                {
                    view.Root.localPosition = new Vector3(settings.areaOffset.x, settings.areaOffset.y, settings.depth);

                    if (settings.fitToCamera && fitSize.HasValue)
                    {
                        var shape = view.System.shape;
                        var targetScale = new Vector3(Mathf.Max(fitSize.Value.x, 0.1f), Mathf.Max(fitSize.Value.y, 0.1f), 0.01f);
                        if ((shape.scale - targetScale).sqrMagnitude > 0.01f)
                        {
                            shape.scale = targetScale;
                        }
                    }
                }

                ApplyWind(view, settings, windVelocity);

                if (Application.isPlaying && !view.System.isPlaying)
                {
                    view.System.Play();
                }
            }
        }

        public void SimulateEditorPreview(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            for (var i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null || view.System == null || view.Renderer == null || !view.Renderer.enabled)
                {
                    continue;
                }

                if (view.PreviewPrimed)
                {
                    view.System.Simulate(deltaTime, true, false, false);
                    continue;
                }

                view.PreviewPrimed = true;
                view.System.Simulate(Mathf.Max(view.System.main.duration, 1f), true, true, false);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _views.Count; i++)
            {
                SafeDestroy(_views[i].Material);
            }

            _views.Clear();

            if (_parent != null)
            {
                SafeDestroy(_parent.gameObject);
                _parent = null;
            }
        }

        private static void Configure(LayerView view, AmbienceParticleLayerSettings settings, float densityScale, float speedScale)
        {
            var system = view.System;
            if (system.isPlaying)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var rate = settings.rate * densityScale;
            var lifetimeMin = Mathf.Min(settings.lifetimeRange.x, settings.lifetimeRange.y);
            var lifetimeMax = Mathf.Max(settings.lifetimeRange.x, settings.lifetimeRange.y);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = true;
            main.duration = Mathf.Max(lifetimeMax, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.speedRange.x, settings.speedRange.y) * speedScale,
                Mathf.Max(settings.speedRange.x, settings.speedRange.y) * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Min(settings.sizeRange.x, settings.sizeRange.y),
                Mathf.Max(settings.sizeRange.x, settings.sizeRange.y));
            main.startColor = settings.color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = settings.gravity * speedScale;
            main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(rate * lifetimeMax) + 8, 8, 4000);

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(settings.area.x, 0.1f), Mathf.Max(settings.area.y, 0.1f), 0.01f);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            var noise = system.noise;
            noise.enabled = settings.turbulence > 0.001f;
            if (noise.enabled)
            {
                noise.strength = new ParticleSystem.MinMaxCurve(settings.turbulence);
                noise.frequency = Mathf.Max(settings.turbulenceFrequency, 0.01f);
                noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);
                noise.damping = true;
                noise.quality = ParticleSystemNoiseQuality.Medium;
            }

            var rotation = system.rotationOverLifetime;
            rotation.enabled = Mathf.Abs(settings.rotationSpeed) > 0.01f;
            if (rotation.enabled)
            {
                var radians = settings.rotationSpeed * Mathf.Deg2Rad;
                rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.Abs(radians), Mathf.Abs(radians));
            }

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateLifetimeGradient(settings.flicker));

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve());

            var renderer = view.Renderer;
            if (settings.stretch > 0.01f)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = settings.stretch;
                renderer.velocityScale = 0.08f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.lengthScale = 1f;
                renderer.velocityScale = 0f;
            }

            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingLayerName = string.IsNullOrEmpty(settings.sortingLayer) ? "Default" : settings.sortingLayer;
            renderer.sortingOrder = settings.sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var material = view.Material;
            material.SetTexture(MainTexId, settings.texture);
            material.SetFloat(UseTextureId, settings.texture != null ? 1f : 0f);
            material.SetFloat(FeatherId, settings.feather);
            material.SetFloat(HardnessId, settings.hardness);
            material.SetFloat(BrightnessId, settings.brightness);
        }

        private static void ApplyWind(LayerView view, AmbienceParticleLayerSettings settings, Vector2 windVelocity)
        {
            var velocity = view.System.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(windVelocity.x * settings.windFollow);
            velocity.y = new ParticleSystem.MinMaxCurve(windVelocity.y * settings.windFollow);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
        }

        private static int ComputeConfigHash(AmbienceParticleLayerSettings settings)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + settings.color.GetHashCode();
                hash = hash * 31 + settings.rate.GetHashCode();
                hash = hash * 31 + settings.sizeRange.GetHashCode();
                hash = hash * 31 + settings.lifetimeRange.GetHashCode();
                hash = hash * 31 + settings.speedRange.GetHashCode();
                hash = hash * 31 + settings.gravity.GetHashCode();
                hash = hash * 31 + settings.turbulence.GetHashCode();
                hash = hash * 31 + settings.turbulenceFrequency.GetHashCode();
                hash = hash * 31 + settings.rotationSpeed.GetHashCode();
                hash = hash * 31 + settings.flicker.GetHashCode();
                hash = hash * 31 + settings.brightness.GetHashCode();
                hash = hash * 31 + settings.feather.GetHashCode();
                hash = hash * 31 + settings.hardness.GetHashCode();
                hash = hash * 31 + settings.stretch.GetHashCode();
                hash = hash * 31 + (settings.texture != null ? settings.texture.GetInstanceID() : 0);
                hash = hash * 31 + settings.area.GetHashCode();
                hash = hash * 31 + settings.areaOffset.GetHashCode();
                hash = hash * 31 + (settings.sortingLayer != null ? settings.sortingLayer.GetHashCode() : 0);
                hash = hash * 31 + settings.sortingOrder;
                hash = hash * 31 + settings.depth.GetHashCode();
                return hash;
            }
        }

        private static void SafeDestroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static Gradient CreateLifetimeGradient(float flicker)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                flicker > 0.01f
                    ? new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(Mathf.Lerp(1f, 0.45f, flicker), 0.4f),
                        new GradientAlphaKey(1f, 0.6f),
                        new GradientAlphaKey(Mathf.Lerp(1f, 0.55f, flicker), 0.8f),
                        new GradientAlphaKey(0f, 1f)
                    }
                    : new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.18f),
                        new GradientAlphaKey(1f, 0.78f),
                        new GradientAlphaKey(0f, 1f)
                    });

            return gradient;
        }

        private static AnimationCurve CreateSizeCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 0.75f));
        }
    }
}
