using System;
using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    public sealed class AmbienceSpriteLighting
    {
        private const string ShaderName = "TMN/AmbienceSprite";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int TintId = Shader.PropertyToID("_Color");
        private static readonly int LitAmountId = Shader.PropertyToID("_LitAmount");
        private static readonly int VolumeAmountId = Shader.PropertyToID("_VolumeAmount");
        private static readonly int RimAmountId = Shader.PropertyToID("_RimAmount");
        private static readonly int GroundShadeId = Shader.PropertyToID("_GroundShade");
        private static readonly int GroundShadeHeightId = Shader.PropertyToID("_GroundShadeHeight");
        private static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");
        private static readonly int HazeBlendId = Shader.PropertyToID("_HazeBlend");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int GradeInfluenceId = Shader.PropertyToID("_GradeInfluence");
        private static readonly int GradeOverrideId = Shader.PropertyToID("_GradeOverride");

        private enum SpriteCategory : byte
        {
            Asset = 0,
            Wind = 1,
            Background = 2
        }

        private sealed class TrackedSprite
        {
            public SpriteRenderer Renderer;
            public Material OriginalMaterial;
            public SpriteCategory Category;
            public Sprite LastSprite;
            public bool Painted;
        }

        private readonly List<SpriteRenderer> _scanBuffer = new List<SpriteRenderer>();
        private readonly List<TrackedSprite> _tracked = new List<TrackedSprite>();
        private readonly List<SpriteRenderer> _backgrounds = new List<SpriteRenderer>();
        private readonly HashSet<SpriteRenderer> _trackedSet = new HashSet<SpriteRenderer>();
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

        private Material _material;
        private int _appliedSettingsHash;

        public bool IsReady => _material != null;

        public IReadOnlyList<SpriteRenderer> Backgrounds => _backgrounds;

        public bool EnsureMaterials()
        {
            if (_material != null)
            {
                return true;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return false;
            }

            _material = new Material(shader)
            {
                name = "AmbienceSprite",
                hideFlags = HideFlags.HideAndDontSave
            };
            return true;
        }

        public void Rescan(Transform root, AmbienceSpriteSettings settings)
        {
            if (root == null || !EnsureMaterials())
            {
                return;
            }

            root.GetComponentsInChildren(true, _scanBuffer);

            for (var i = 0; i < _scanBuffer.Count; i++)
            {
                var renderer = _scanBuffer[i];
                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                if (_trackedSet.Contains(renderer))
                {
                    continue;
                }

                if (ShouldSkip(renderer, settings))
                {
                    continue;
                }

                var isBackground = NameContainsAny(renderer, settings.backgroundNameParts);
                if (isBackground && !settings.includeBackground)
                {
                    continue;
                }

                var category = SpriteCategory.Asset;
                if (isBackground)
                {
                    category = SpriteCategory.Background;
                    _backgrounds.Add(renderer);
                }
                else if (settings.useShaderWind && NameContainsAny(renderer, settings.windNameParts))
                {
                    category = SpriteCategory.Wind;
                }

                _tracked.Add(new TrackedSprite
                {
                    Renderer = renderer,
                    OriginalMaterial = renderer.sharedMaterial,
                    Category = category
                });
                _trackedSet.Add(renderer);
                renderer.sharedMaterial = _material;
            }

            _scanBuffer.Clear();
        }

        public void ApplySettings(AmbienceSpriteSettings settings, float hazeBlendScale)
        {
            if (!IsReady)
            {
                return;
            }

            _material.SetColor(TintId, settings.assetsTint);
            _material.SetFloat(LitAmountId, settings.assetsLit);
            _material.SetFloat(VolumeAmountId, settings.assetsVolume);
            _material.SetFloat(RimAmountId, settings.assetsRim);
            _material.SetFloat(GroundShadeId, settings.assetsGroundShade);
            _material.SetFloat(GroundShadeHeightId, settings.assetsGroundShadeHeight);
            _material.SetFloat(WindStrengthId, 0f);
            _material.SetFloat(HazeBlendId, settings.assetsHazeBlend * hazeBlendScale);
            _material.SetFloat(SaturationId, settings.assetsSaturation);

            var settingsHash = ComputeMpbSettingsHash(settings, hazeBlendScale);
            var settingsChanged = settingsHash != _appliedSettingsHash;
            _appliedSettingsHash = settingsHash;

            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var tracked = _tracked[i];
                var renderer = tracked.Renderer;
                if (renderer == null)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                var sprite = renderer.sprite;
                var needsWrite = !tracked.Painted
                    || !ReferenceEquals(sprite, tracked.LastSprite)
                    || (settingsChanged && tracked.Category != SpriteCategory.Asset);

                if (!needsWrite)
                {
                    continue;
                }

                tracked.Painted = true;
                tracked.LastSprite = sprite;

                _propertyBlock.Clear();
                renderer.GetPropertyBlock(_propertyBlock);

                if (sprite != null && sprite.texture != null)
                {
                    _propertyBlock.SetTexture(MainTexId, sprite.texture);
                }

                if (tracked.Category == SpriteCategory.Wind)
                {
                    _propertyBlock.SetFloat(WindStrengthId, settings.shaderWindStrength);
                }
                else if (tracked.Category == SpriteCategory.Background)
                {
                    _propertyBlock.SetColor(TintId, settings.backgroundTint);
                    _propertyBlock.SetFloat(LitAmountId, settings.backgroundLit);
                    _propertyBlock.SetFloat(VolumeAmountId, 0f);
                    _propertyBlock.SetFloat(RimAmountId, 0f);
                    _propertyBlock.SetFloat(GroundShadeId, 0f);
                    _propertyBlock.SetFloat(HazeBlendId, settings.backgroundHazeBlend * hazeBlendScale);
                    _propertyBlock.SetFloat(SaturationId, settings.backgroundSaturation);
                    _propertyBlock.SetFloat(GradeInfluenceId, settings.backgroundGradeInfluence);
                    _propertyBlock.SetVector(GradeOverrideId, new Vector4(
                        settings.backgroundContrast,
                        1f,
                        settings.backgroundTemperature,
                        settings.overrideBackgroundGrade ? 1f : 0f));
                }

                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static int ComputeMpbSettingsHash(AmbienceSpriteSettings settings, float hazeBlendScale)
        {
            var hash = HashCode.Combine(
                settings.shaderWindStrength,
                settings.backgroundTint,
                settings.backgroundLit,
                settings.backgroundHazeBlend,
                hazeBlendScale,
                settings.backgroundSaturation,
                settings.backgroundGradeInfluence,
                settings.backgroundContrast);
            return HashCode.Combine(hash, settings.backgroundTemperature, settings.overrideBackgroundGrade);
        }

        public void Restore()
        {
            for (var i = 0; i < _tracked.Count; i++)
            {
                var tracked = _tracked[i];
                if (tracked.Renderer == null)
                {
                    continue;
                }

                tracked.Renderer.sharedMaterial = tracked.OriginalMaterial;
                tracked.Renderer.SetPropertyBlock(null);
            }

            _tracked.Clear();
            _trackedSet.Clear();
            _backgrounds.Clear();
            DestroyMaterial(ref _material);
        }

        private static bool ShouldSkip(SpriteRenderer renderer, AmbienceSpriteSettings settings)
        {
            if (renderer.GetComponentInParent<Canvas>() != null)
            {
                return true;
            }

            var name = renderer.gameObject.name;
            if (name.IndexOf("UpgradeMark", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("WaterSurfaceDecal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var sortingLayerName = SortingLayer.IDToName(renderer.sortingLayerID);
            for (var i = 0; i < settings.skipSortingLayers.Length; i++)
            {
                if (string.Equals(sortingLayerName, settings.skipSortingLayers[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return NameContainsAny(renderer, settings.skipNameParts);
        }

        private static bool NameContainsAny(Component component, string[] parts)
        {
            if (parts == null)
            {
                return false;
            }

            var name = component.gameObject.name;
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

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            material = null;
        }
    }
}
