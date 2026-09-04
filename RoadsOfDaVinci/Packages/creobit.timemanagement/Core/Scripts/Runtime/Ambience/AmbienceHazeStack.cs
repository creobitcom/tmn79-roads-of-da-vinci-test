using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    public sealed class AmbienceHazeStack
    {
        private const string ShaderName = "TMN/AmbienceHaze";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int UseTextureId = Shader.PropertyToID("_UseTexture");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int DetailId = Shader.PropertyToID("_Detail");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");
        private static readonly int WindResponseId = Shader.PropertyToID("_WindResponse");
        private static readonly int MaskBottomId = Shader.PropertyToID("_MaskBottom");
        private static readonly int MaskTopId = Shader.PropertyToID("_MaskTop");
        private static readonly int MaskFeatherId = Shader.PropertyToID("_MaskFeather");
        private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
        private static readonly int DarkenId = Shader.PropertyToID("_Darken");
        private static readonly int LightShaftsId = Shader.PropertyToID("_LightShafts");

        private sealed class LayerView
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public Material Material;
        }

        private readonly List<LayerView> _views = new List<LayerView>();
        private Transform _parent;
        private Mesh _quad;

        public Transform Root => _parent;

        public void Rebuild(Transform owner, List<AmbienceHazeLayerSettings> layers)
        {
            Clear();

            var shader = Shader.Find(ShaderName);
            if (shader == null || owner == null || layers == null)
            {
                return;
            }

            var parentObject = new GameObject("AmbienceFog");
            if (!Application.isPlaying)
            {
                parentObject.hideFlags = HideFlags.HideAndDontSave;
            }

            _parent = parentObject.transform;
            _parent.SetParent(owner, false);

            _quad = CreateQuad();

            for (var i = 0; i < layers.Count; i++)
            {
                var settings = layers[i];
                if (settings == null)
                {
                    continue;
                }

                var layerObject = new GameObject("Fog_" + (string.IsNullOrEmpty(settings.layerName) ? "Haze" : settings.layerName));
                var layerTransform = layerObject.transform;
                layerTransform.SetParent(_parent, false);

                var filter = layerObject.AddComponent<MeshFilter>();
                filter.sharedMesh = _quad;

                var renderer = layerObject.AddComponent<MeshRenderer>();
                var material = new Material(shader)
                {
                    name = layerObject.name,
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

                _views.Add(new LayerView { Root = layerTransform, Renderer = renderer, Material = material });
            }

            ApplySettings(layers);
        }

        public void ApplySettings(List<AmbienceHazeLayerSettings> layers)
        {
            ApplySettings(layers, null);
        }

        public void ApplySettings(List<AmbienceHazeLayerSettings> layers, Vector2? fitSize)
        {
            if (layers == null)
            {
                return;
            }

            for (var i = 0; i < _views.Count && i < layers.Count; i++)
            {
                var view = _views[i];
                var settings = layers[i];
                if (view == null || settings == null || view.Renderer == null)
                {
                    continue;
                }

                if (view.Renderer.enabled != settings.enabled)
                {
                    view.Renderer.enabled = settings.enabled;
                }

                if (!settings.enabled)
                {
                    continue;
                }

                var layerSize = settings.fitToCamera && fitSize.HasValue ? fitSize.Value : settings.size;
                view.Root.localPosition = new Vector3(settings.offset.x, settings.offset.y, settings.depth);
                view.Root.localScale = new Vector3(layerSize.x, layerSize.y, 1f);

                view.Renderer.sortingLayerName = string.IsNullOrEmpty(settings.sortingLayer) ? "Default" : settings.sortingLayer;
                view.Renderer.sortingOrder = settings.sortingOrder;

                var material = view.Material;
                material.SetTexture(MainTexId, settings.texture);
                material.SetColor(ColorId, settings.color);
                material.SetFloat(OpacityId, settings.opacity);
                material.SetFloat(UseTextureId, settings.texture != null ? 1f : 0f);
                material.SetFloat(NoiseScaleId, settings.noiseScale);
                material.SetFloat(DetailId, settings.detail);
                material.SetFloat(CoverageId, settings.coverage);
                material.SetFloat(SoftnessId, settings.softness);
                material.SetVector(ScrollId, new Vector4(settings.scrollSpeed.x, settings.scrollSpeed.y, 0f, 0f));
                material.SetFloat(WindResponseId, settings.windResponse);
                material.SetFloat(MaskBottomId, settings.maskBottom);
                material.SetFloat(MaskTopId, settings.maskTop);
                material.SetFloat(MaskFeatherId, settings.maskFeather);
                material.SetFloat(EdgeFadeId, settings.edgeFade);
                material.SetFloat(DarkenId, settings.darken);
                material.SetFloat(LightShaftsId, settings.lightShafts ? 1f : 0f);
            }
        }

        public int LayerCount => _views.Count;

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

            if (_quad != null)
            {
                SafeDestroy(_quad);
                _quad = null;
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

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "AmbienceHazeQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
