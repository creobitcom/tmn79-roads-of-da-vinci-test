using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects
{
    public class InactiveObjectsBridge : MonoBehaviour, ILevelLoadUnit
    {
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int EdgeStartId = Shader.PropertyToID("_EdgeStart");
        private static readonly int EdgeEndId = Shader.PropertyToID("_EdgeEnd");
        private static readonly int EdgeGlowColorId = Shader.PropertyToID("_EdgeGlowColor");
        private static readonly int EdgeGlowWidthId = Shader.PropertyToID("_EdgeGlowWidth");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
        private static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        private static readonly int SwirlScaleId = Shader.PropertyToID("_SwirlScale");
        private static readonly int EdgeBreakId = Shader.PropertyToID("_EdgeBreak");
        private static readonly int ReliefId = Shader.PropertyToID("_Relief");
        private static readonly int DropShadowId = Shader.PropertyToID("_DropShadow");
        private static readonly int FogColorTopId = Shader.PropertyToID("_FogColorTop");
        private static readonly int NoiseAspectId = Shader.PropertyToID("_NoiseAspect");
        private static readonly int BreathingId = Shader.PropertyToID("_Breathing");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public bool StartActiveState;
        public bool ActiveStatePriority;
        public Transform LevelRoot;

        [Title("Fog Look")]
        public Color inactiveColor = new(0.58f, 0.62f, 0.72f, 1f);
        public Color fogColorLit = new(0.98f, 0.98f, 1f, 1f);

        [Range(0f, 1f)] public float relief = 0.8f;
        [Range(0f, 1f)] public float dropShadow = 0.55f;
        [Range(0f, 1f)] public float edgeStart = 0.34f;
        [Range(0f, 1f)] public float edgeEnd = 0.52f;

        [ColorUsage(true)] public Color edgeGlowColor = new(1f, 1f, 1f, 0f);
        [Range(0.01f, 0.5f)] public float edgeGlowWidth = 0.15f;

        [Title("Fog Motion")]
        [Range(0.5f, 24f)] public float noiseScale = 2.5f;
        [Range(0f, 2f)] public float noiseSpeed = 1f;
        [Range(0f, 3f)] public float swirl = 1f;
        [Range(0.1f, 3f)] public float swirlScale = 0.6f;
        [Range(0f, 1f)] public float edgeBreak = 0.65f;
        [Range(0f, 1f)] public float breathing = 0.15f;

        [Title("Reveal")]
        [Range(0f, 3f)] public float revealDuration = 0.45f;
        [Range(0f, 0.6f)] public float revealOvershoot = 0.12f;

        [HideIf(nameof(StartActiveState))]
        [Range(0f, 1f)] public float exploredMaskLevel = 0.55f;

        [Inject]
        private IInactiveObjectController _controller;
        [Inject]
        private GameplaySceneReferences _sceneReferences;

        private Camera _fogCamera;

        public UniTask Load()
        {
            ApplyFogMaterial();

            var nodes = GameObject.FindGameObjectsWithTag("GraphPoint")
                .Select(node => AstarPath.active.GetNearest(node.transform.position).node)
                .ToList();
            var objects = LevelRoot.GetComponentsInChildren<IReactToActions>()
                .ToList();

            _controller.SetDefaultState(StartActiveState);
            _controller.SetPriority(ActiveStatePriority);
            _controller.SetRevealSettings(revealDuration, revealOvershoot, StartActiveState ? 0f : exploredMaskLevel);
            _controller.SetNodesState(nodes, StartActiveState);
            _controller.SetObjectsState(objects, StartActiveState);

            return UniTask.CompletedTask;
        }

        private void LateUpdate()
        {
            if (_sceneReferences == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                ApplyFogSettings();
            }
#endif

            var fogTexture = _sceneReferences.FogTexture;

            if (fogTexture == null || !fogTexture.enabled)
            {
                return;
            }

            if (!TryGetFogCamera(out var fogCamera))
            {
                return;
            }

            AlignFogQuad(fogCamera, fogTexture, _sceneReferences.MainCamera);
        }

        public static void AlignFogQuad(Camera fogCamera, Renderer fogTexture, Camera viewCamera)
        {
            if (!fogCamera || !fogTexture)
            {
                return;
            }

            var renderTexture = fogCamera.targetTexture;

            if (!renderTexture || renderTexture.height <= 0)
            {
                return;
            }

            var rtAspect = renderTexture.width / (float)renderTexture.height;
            var quad = fogTexture.transform;
            float width;
            float height;

            if (viewCamera && viewCamera.orthographic)
            {
                var viewHeight = viewCamera.orthographicSize * 2f;
                var viewWidth = viewHeight * viewCamera.aspect;
                var camPos = viewCamera.transform.position;
                var cx = quad.position.x;
                var cy = quad.position.y;
                var needW = 2f * Mathf.Max(
                    Mathf.Abs(camPos.x + viewWidth * 0.5f - cx),
                    Mathf.Abs(camPos.x - viewWidth * 0.5f - cx));
                var needH = 2f * Mathf.Max(
                    Mathf.Abs(camPos.y + viewHeight * 0.5f - cy),
                    Mathf.Abs(camPos.y - viewHeight * 0.5f - cy));
                height = Mathf.Max(needH, needW / rtAspect);
                width = height * rtAspect;
            }
            else
            {
                height = Mathf.Abs(quad.localScale.y);

                if (height <= Mathf.Epsilon)
                {
                    return;
                }

                width = height * rtAspect;
            }

            if (Mathf.Abs(quad.localScale.x - width) > 0.01f || Mathf.Abs(quad.localScale.y - height) > 0.01f)
            {
                quad.localScale = new Vector3(width, height, quad.localScale.z);
            }

            var cameraTransform = fogCamera.transform;
            fogCamera.orthographic = true;
            fogCamera.orthographicSize = height * 0.5f;
            cameraTransform.position = new Vector3(quad.position.x, quad.position.y, cameraTransform.position.z);
        }

        private bool TryGetFogCamera(out Camera fogCamera)
        {
            if (_fogCamera)
            {
                fogCamera = _fogCamera;
                return true;
            }

            var fogMaterial = _sceneReferences != null ? _sceneReferences.FogMaterial : null;

            if (!fogMaterial)
            {
                fogCamera = null;
                return false;
            }

            var fogRenderTexture = fogMaterial.GetTexture(MainTexId);

            if (!fogRenderTexture)
            {
                fogCamera = null;
                return false;
            }

            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];

                if (camera.targetTexture != fogRenderTexture)
                {
                    continue;
                }

                _fogCamera = camera;
                fogCamera = camera;
                return true;
            }

            fogCamera = null;
            return false;
        }

        private void ApplyFogMaterial()
        {
            var fogTexture = _sceneReferences.FogTexture;

            if (fogTexture == null)
            {
                return;
            }

            fogTexture.sharedMaterial = StartActiveState
                ? _sceneReferences.AntiFogMaterial
                : _sceneReferences.FogMaterial;

            ApplyFogSettings();
        }

        private void ApplyFogSettings()
        {
            var fogTexture = _sceneReferences.FogTexture;

            if (fogTexture == null)
            {
                return;
            }

            var scale = fogTexture.transform.lossyScale;
            var aspect = Mathf.Approximately(scale.y, 0f) ? 1f : Mathf.Abs(scale.x / scale.y);

            var block = new MaterialPropertyBlock();
            fogTexture.GetPropertyBlock(block);

            block.SetColor(FogColorId, ToShaderColor(inactiveColor));
            block.SetColor(FogColorTopId, ToShaderColor(fogColorLit));
            block.SetFloat(ReliefId, relief);
            block.SetFloat(DropShadowId, dropShadow);
            block.SetFloat(EdgeStartId, edgeStart);
            block.SetFloat(EdgeEndId, edgeEnd);
            block.SetColor(EdgeGlowColorId, ToShaderColor(edgeGlowColor));
            block.SetFloat(EdgeGlowWidthId, edgeGlowWidth);
            block.SetFloat(NoiseScaleId, noiseScale);
            block.SetFloat(NoiseSpeedId, noiseSpeed);
            block.SetFloat(SwirlId, swirl);
            block.SetFloat(SwirlScaleId, swirlScale);
            block.SetFloat(EdgeBreakId, edgeBreak);
            block.SetFloat(NoiseAspectId, aspect);
            block.SetFloat(BreathingId, breathing);

            fogTexture.SetPropertyBlock(block);
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

        public UniTask Dispose()
        {
            return UniTask.CompletedTask;
        }
    }
}
