using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace _8floor.TimeManagement.Core.Scripts.Editor.InactiveObjects
{
    [InitializeOnLoad]
    public static class FogPrefabStagePreview
    {
        private const float QuadHeight = 16.68f;
        private const int MaskWidth = 1920;
        private const int MaskHeight = 1080;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int FogColorTopId = Shader.PropertyToID("_FogColorTop");
        private static readonly int ReliefId = Shader.PropertyToID("_Relief");
        private static readonly int DropShadowId = Shader.PropertyToID("_DropShadow");
        private static readonly int EdgeStartId = Shader.PropertyToID("_EdgeStart");
        private static readonly int EdgeEndId = Shader.PropertyToID("_EdgeEnd");
        private static readonly int EdgeGlowColorId = Shader.PropertyToID("_EdgeGlowColor");
        private static readonly int EdgeGlowWidthId = Shader.PropertyToID("_EdgeGlowWidth");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
        private static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        private static readonly int SwirlScaleId = Shader.PropertyToID("_SwirlScale");
        private static readonly int EdgeBreakId = Shader.PropertyToID("_EdgeBreak");
        private static readonly int NoiseAspectId = Shader.PropertyToID("_NoiseAspect");
        private static readonly int BreathingId = Shader.PropertyToID("_Breathing");

        private static InactiveObjectsBridge _bridge;
        private static RenderTexture _maskTexture;
        private static GameObject _quad;
        private static MeshRenderer _quadRenderer;
        private static Material _spriteMaterial;
        private static Mesh _quadMesh;
        private static readonly List<GameObject> _hiddenLights = new();

        static FogPrefabStagePreview()
        {
            PrefabStage.prefabStageOpened += OnStageOpened;
            PrefabStage.prefabStageClosing += OnStageClosing;
            EditorApplication.update += OnUpdate;
            EditorApplication.delayCall += () =>
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                {
                    OnStageOpened(stage);
                }
            };
        }

        private static void OnStageOpened(PrefabStage stage)
        {
            Cleanup();

            _bridge = stage.prefabContentsRoot.GetComponentInChildren<InactiveObjectsBridge>(true);

            if (_bridge == null)
            {
                return;
            }

            _maskTexture = new RenderTexture(MaskWidth, MaskHeight, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _spriteMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "FogPreview";
            _quad.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(_quad.GetComponent<Collider>());
            SceneManager.MoveGameObjectToScene(_quad, stage.scene);

            _quad.transform.position = new Vector3(0f, 0f, -1f);
            FitPreviewQuad();

            _quadRenderer = _quad.GetComponent<MeshRenderer>();
            _quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _quadRenderer.receiveShadows = false;

            var layers = SortingLayer.layers;
            _quadRenderer.sortingLayerID = layers[layers.Length - 1].id;
            _quadRenderer.sortingOrder = 32000;

            SceneVisibilityManager.instance.DisablePicking(_quad, true);

            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                sceneView.sceneViewState.alwaysRefresh = true;
            }
        }

        private static void OnStageClosing(PrefabStage stage)
        {
            Cleanup();
        }

        private static void OnUpdate()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage == null)
            {
                if (_bridge != null || _quad != null)
                {
                    Cleanup();
                }

                return;
            }

            if (_bridge == null || _quad == null || _maskTexture == null)
            {
                OnStageOpened(stage);

                if (_bridge == null || _quad == null)
                {
                    return;
                }
            }

            var refs = stage.prefabContentsRoot.GetComponentsInChildren<InactiveObjectRefs>(true);
            HideLights(refs);
            FitPreviewQuad();
            RenderMask(refs);
            ApplyQuadMaterial();
        }

        private static void HideLights(InactiveObjectRefs[] refs)
        {
            foreach (var inactiveRef in refs)
            {
                if (inactiveRef.lightSprite == null)
                {
                    continue;
                }

                var lightObject = inactiveRef.lightSprite.gameObject;

                if (_hiddenLights.Contains(lightObject))
                {
                    continue;
                }

                SceneVisibilityManager.instance.Hide(lightObject, true);
                _hiddenLights.Add(lightObject);
            }
        }

        private static void RenderMask(InactiveObjectRefs[] refs)
        {
            var commandBuffer = new CommandBuffer();
            commandBuffer.SetRenderTarget(_maskTexture);
            commandBuffer.ClearRenderTarget(true, true, Color.black);

            GetCoverSize(out var width, out var height);
            var projection = Matrix4x4.Ortho(
                -width * 0.5f, width * 0.5f,
                -height * 0.5f, height * 0.5f,
                -1000f, 1000f);
            commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, projection);

            foreach (var inactiveRef in refs)
            {
                if (inactiveRef.lightSprite == null || inactiveRef.lightSprite.sprite == null)
                {
                    continue;
                }

                if (!IsZoneDrawn(inactiveRef))
                {
                    continue;
                }

                var sprite = inactiveRef.lightSprite.sprite;
                var size = sprite.bounds.size;
                var scale = GetLightScale(inactiveRef);
                var position = inactiveRef.lightSprite.transform.position;
                var matrix = Matrix4x4.TRS(
                    new Vector3(position.x, position.y, 0f),
                    Quaternion.identity,
                    new Vector3(size.x * scale.x, size.y * scale.y, 1f));

                var block = new MaterialPropertyBlock();
                block.SetTexture(MainTexId, sprite.texture);
                commandBuffer.DrawMesh(_quadMesh, matrix, _spriteMaterial, 0, 0, block);
            }

            Graphics.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Release();
        }

        private static bool IsZoneDrawn(InactiveObjectRefs inactiveRef)
        {
            if (!inactiveRef.State)
            {
                return false;
            }

            var isUnBlock = inactiveRef.ObjectSwitcher && inactiveRef.NodeSwitcher;

            return _bridge.StartActiveState ? !isUnBlock : isUnBlock;
        }

        private static Vector2 GetLightScale(InactiveObjectRefs inactiveRef)
        {
            var lightTransform = inactiveRef.lightSprite.transform;
            var radius = inactiveRef.Radius;

            if (radius <= 0f)
            {
                return lightTransform.lossyScale;
            }

            var lightSize = radius / 5f * 1.6f;
            var parent = lightTransform.parent;

            if (parent == null)
            {
                return new Vector2(lightSize, lightSize);
            }

            var parentScale = parent.lossyScale;

            return new Vector2(lightSize * parentScale.x, lightSize * parentScale.y);
        }

        private static void FitPreviewQuad()
        {
            if (_quad == null)
            {
                return;
            }

            GetCoverSize(out var width, out var height);
            var scale = _quad.transform.localScale;

            if (Mathf.Abs(scale.x - width) > 0.01f || Mathf.Abs(scale.y - height) > 0.01f)
            {
                _quad.transform.localScale = new Vector3(width, height, 1f);
            }
        }

        private static void GetCoverSize(out float width, out float height)
        {
            var rtAspect = MaskWidth / (float)MaskHeight;
            var sceneView = SceneView.lastActiveSceneView;
            var viewCamera = sceneView != null ? sceneView.camera : null;

            if (viewCamera != null && viewCamera.orthographic)
            {
                var viewHeight = viewCamera.orthographicSize * 2f;
                var viewWidth = viewHeight * viewCamera.aspect;
                var camPos = viewCamera.transform.position;
                var cx = _quad != null ? _quad.transform.position.x : 0f;
                var cy = _quad != null ? _quad.transform.position.y : 0f;
                var needW = 2f * Mathf.Max(
                    Mathf.Abs(camPos.x + viewWidth * 0.5f - cx),
                    Mathf.Abs(camPos.x - viewWidth * 0.5f - cx));
                var needH = 2f * Mathf.Max(
                    Mathf.Abs(camPos.y + viewHeight * 0.5f - cy),
                    Mathf.Abs(camPos.y - viewHeight * 0.5f - cy));
                height = Mathf.Max(QuadHeight, Mathf.Max(needH, needW / rtAspect));
                width = height * rtAspect;
                return;
            }

            height = QuadHeight;
            width = QuadHeight * rtAspect;
        }

        private static void ApplyQuadMaterial()
        {
            var material = Resources.Load<Material>(_bridge.StartActiveState ? "AntiFog" : "Fog");

            if (material == null)
            {
                return;
            }

            _quadRenderer.sharedMaterial = material;

            var block = new MaterialPropertyBlock();
            _quadRenderer.GetPropertyBlock(block);
            block.SetTexture(MainTexId, _maskTexture);
            block.SetColor(FogColorId, ToShaderColor(_bridge.inactiveColor));
            block.SetColor(FogColorTopId, ToShaderColor(_bridge.fogColorLit));
            block.SetFloat(ReliefId, _bridge.relief);
            block.SetFloat(DropShadowId, _bridge.dropShadow);
            block.SetFloat(EdgeStartId, _bridge.edgeStart);
            block.SetFloat(EdgeEndId, _bridge.edgeEnd);
            block.SetColor(EdgeGlowColorId, ToShaderColor(_bridge.edgeGlowColor));
            block.SetFloat(EdgeGlowWidthId, _bridge.edgeGlowWidth);
            block.SetFloat(NoiseScaleId, _bridge.noiseScale);
            block.SetFloat(NoiseSpeedId, _bridge.noiseSpeed);
            block.SetFloat(SwirlId, _bridge.swirl);
            block.SetFloat(SwirlScaleId, _bridge.swirlScale);
            block.SetFloat(EdgeBreakId, _bridge.edgeBreak);
            block.SetFloat(NoiseAspectId, MaskWidth / (float)MaskHeight);
            block.SetFloat(BreathingId, _bridge.breathing);
            _quadRenderer.SetPropertyBlock(block);
        }

        private static Color ToShaderColor(Color color)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Gamma)
            {
                return color;
            }

            var linear = color.linear;
            linear.a = color.a;

            return linear;
        }

        private static void Cleanup()
        {
            foreach (var lightObject in _hiddenLights)
            {
                if (lightObject != null)
                {
                    SceneVisibilityManager.instance.Show(lightObject, true);
                }
            }

            _hiddenLights.Clear();

            if (_quad != null)
            {
                Object.DestroyImmediate(_quad);
            }

            _quad = null;
            _quadRenderer = null;

            if (_maskTexture != null)
            {
                _maskTexture.Release();
                Object.DestroyImmediate(_maskTexture);
            }

            _maskTexture = null;

            if (_spriteMaterial != null)
            {
                Object.DestroyImmediate(_spriteMaterial);
            }

            _spriteMaterial = null;
            _bridge = null;
        }
    }
}
