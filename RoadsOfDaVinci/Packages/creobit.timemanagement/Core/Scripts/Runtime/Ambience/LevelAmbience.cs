using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    [ExecuteAlways]
    public sealed class LevelAmbience : MonoBehaviour
    {
        private const int MaxPointLights = 8;

        private static readonly int SunDirectionId = Shader.PropertyToID("_TMNSunDirection");
        private static readonly int SunColorId = Shader.PropertyToID("_TMNSunColor");
        private static readonly int AmbientColorId = Shader.PropertyToID("_TMNAmbientColor");
        private static readonly int HazeColorId = Shader.PropertyToID("_TMNHazeColor");
        private static readonly int TimeId = Shader.PropertyToID("_TMNTime");
        private static readonly int GradeId = Shader.PropertyToID("_TMNGrade");
        private static readonly int TiltShiftStrengthId = Shader.PropertyToID("_TMNTiltShiftStrength");
        private static readonly int TiltShiftCenterId = Shader.PropertyToID("_TMNTiltShiftCenter");
        private static readonly int TiltShiftWidthId = Shader.PropertyToID("_TMNTiltShiftWidth");
        private static readonly int TiltShiftFeatherId = Shader.PropertyToID("_TMNTiltShiftFeather");
        private static readonly int PointLightPositionsId = Shader.PropertyToID("_TMNPointLightPositions");
        private static readonly int PointLightColorsId = Shader.PropertyToID("_TMNPointLightColors");
        private static readonly int PointLightCountId = Shader.PropertyToID("_TMNPointLightCount");

        private static readonly Vector4[] PointLightPositionsBuffer = new Vector4[MaxPointLights];
        private static readonly Vector4[] PointLightColorsBuffer = new Vector4[MaxPointLights];

        public bool enableAmbience = true;

        public static bool DebugDisableHaze;
        public static bool DebugDisableParticles;
        public static bool DebugDisableTiltShift;
        public static bool DebugDisableSpriteLighting;
        public static bool DebugDisablePointLights;
        public static bool DebugDisablePostFx;

        private static readonly List<AmbienceHazeLayerSettings> EmptyHazeLayers = new List<AmbienceHazeLayerSettings>();
        private static readonly List<AmbienceParticleLayerSettings> EmptyParticleLayers = new List<AmbienceParticleLayerSettings>();

        private bool _spriteLightingDebugSuspended;
        public AmbiencePreset preset = AmbiencePreset.Custom;
        public AmbiencePresetSO presetAsset;
        public bool applyAssetOnStart = true;

        [Button("Apply From Asset")]
        public void ApplyFromAsset()
        {
            if (presetAsset == null)
            {
                return;
            }

            EnsureData();
            presetAsset.ApplyTo(this);
            preset = AmbiencePreset.Custom;
            _appliedPreset = AmbiencePreset.Custom;
        }

        [Button("Save To Asset")]
        public void SaveToAsset()
        {
#if UNITY_EDITOR
            EnsureData();

            if (presetAsset == null)
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Ambience Preset",
                    "AmbiencePreset",
                    "asset",
                    "");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                presetAsset = ScriptableObject.CreateInstance<AmbiencePresetSO>();
                AssetDatabase.CreateAsset(presetAsset, path);
            }

            presetAsset.SaveFrom(this);
            EditorUtility.SetDirty(presetAsset);
            AssetDatabase.SaveAssetIfDirty(presetAsset);
#endif
        }
        [LabelText("Brightness")]
        [Range(0f, 2f)] public float exposure = 1f;
        [Range(0.5f, 1.5f)] public float contrast = 1f;
        [LabelText("Saturation")]
        [Range(0f, 2f)] public float colorSaturation = 1f;
        [Range(-1f, 1f)] public float temperature;
        [Range(0f, 2f)] public float particleDensity = 1f;
        [Range(0.2f, 2f)] public float particleSpeed = 1f;

        [FoldoutGroup("Sun & Sky"), HideLabel]
        public AmbienceSunSettings sun = new AmbienceSunSettings();

        [FoldoutGroup("Point Lights")]
        public List<AmbiencePointLightSettings> pointLights = new List<AmbiencePointLightSettings>();

        [FoldoutGroup("Sprite Lighting"), HideLabel]
        public AmbienceSpriteSettings sprites = new AmbienceSpriteSettings();

        [FoldoutGroup("Sprite Lighting")]
        [Range(0f, 2f)] public float hazeInfluenceOnSprites = 1f;

        [FoldoutGroup("Fog")]
        public List<AmbienceHazeLayerSettings> hazeLayers = new List<AmbienceHazeLayerSettings>();

        [FoldoutGroup("Particles")]
        public List<AmbienceParticleLayerSettings> particleLayers = new List<AmbienceParticleLayerSettings>();

        [FoldoutGroup("Wind"), HideLabel]
        public AmbienceWindSettings wind = new AmbienceWindSettings();

        [FoldoutGroup("Post FX")]
        public bool enableBloom;

        [FoldoutGroup("Post FX")]
        [Range(0f, 2f)] public float bloomIntensity = 0.9f;

        [FoldoutGroup("Post FX")]
        [Range(0.5f, 2f)] public float bloomThreshold = 0.85f;

        [FoldoutGroup("Post FX")]
        [Range(0f, 1f)] public float bloomScatter = 0.6f;

        [FoldoutGroup("Post FX")]
        [Range(0f, 1f)] public float vignetteIntensity;

        [FoldoutGroup("Post FX")]
        [Range(0f, 1f)] public float vignetteSmoothness = 0.5f;

        [FoldoutGroup("Post FX")]
        [Range(0f, 1f)] public float tiltShiftStrength;

        [FoldoutGroup("Post FX")]
        [Range(0f, 1f)] public float tiltShiftCenter = 0.5f;

        [FoldoutGroup("Post FX")]
        [Range(0f, 0.5f)] public float tiltShiftWidth = 0.16f;

        [FoldoutGroup("Post FX")]
        [Range(0.01f, 0.6f)] public float tiltShiftFeather = 0.35f;

        [FoldoutGroup("Parallax")]
        [Range(0f, 0.3f)] public float backgroundParallax;

        [FoldoutGroup("Editor")]
        public bool previewInEditMode = true;

        [HideInInspector, SerializeField] private AmbiencePreset _appliedPreset = AmbiencePreset.Custom;

        private AmbienceSpriteLighting _spriteLighting;
        private AmbienceHazeStack _hazeStack;
        private AmbienceParticleStack _particleStack;
        private AmbienceWind _wind;
        private AmbiencePostFx _postFx;
        private Vector2 _smoothedParticleWind;

        private readonly List<Transform> _parallaxTargets = new List<Transform>();
        private readonly List<Vector3> _parallaxBasePositions = new List<Vector3>();
        private Camera _mainCamera;
        private Vector3 _parallaxCameraBase;
        private bool _parallaxCaptured;

        private Camera MainCameraCached
        {
            get
            {
                if (_mainCamera == null)
                {
                    _mainCamera = Camera.main;
                }

                return _mainCamera;
            }
        }

        private Light _sceneLight;
        private bool _lightStateSaved;
        private Color _originalLightColor;
        private float _originalLightIntensity;
        private Quaternion _originalLightRotation;
        private int _spriteRescanCountdown;
        private int _swayRescanCountdown;
        private bool _built;
        private float _lastTickTime;

        private AmbienceSpriteLighting SpriteLighting => _spriteLighting ??= new AmbienceSpriteLighting();
        private AmbienceHazeStack HazeStack => _hazeStack ??= new AmbienceHazeStack();
        private AmbienceParticleStack ParticleStack => _particleStack ??= new AmbienceParticleStack();
        private AmbienceWind WindSimulation => _wind ??= new AmbienceWind();
        private AmbiencePostFx PostFx => _postFx ??= new AmbiencePostFx();

        private bool ShouldRun => enableAmbience && (Application.isPlaying || previewInEditMode);

        private static float CurrentTime
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    return (float)EditorApplication.timeSinceStartup;
                }
#endif
                return Time.time;
            }
        }

        [ContextMenu("Apply Preset")]
        public void ApplyPreset()
        {
            EnsureData();
            AmbiencePresetLibrary.Apply(preset, sun, sprites, hazeLayers, particleLayers, wind);
            _appliedPreset = preset;
        }

        private void EnsureData()
        {
            sun ??= new AmbienceSunSettings();
            sprites ??= new AmbienceSpriteSettings();
            wind ??= new AmbienceWindSettings();
            pointLights ??= new List<AmbiencePointLightSettings>();
            hazeLayers ??= new List<AmbienceHazeLayerSettings>();
            particleLayers ??= new List<AmbienceParticleLayerSettings>();
        }

        private void OnValidate()
        {
            if (preset == _appliedPreset || preset == AmbiencePreset.Custom)
            {
                _appliedPreset = preset;
                return;
            }

            ApplyPreset();
        }

        private void OnEnable()
        {
            EnsureData();

            if (Application.isPlaying && _appliedPreset != preset && preset != AmbiencePreset.Custom)
            {
                ApplyPreset();
            }

            if (Application.isPlaying && presetAsset != null && applyAssetOnStart)
            {
                ApplyFromAsset();
            }

            _lastTickTime = CurrentTime;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorDriver;
                EditorApplication.update += EditorDriver;
                if (!EditPreviewInstances.Contains(this))
                {
                    EditPreviewInstances.Add(this);
                }
            }
#endif

            if (ShouldRun)
            {
                Build();
            }
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorDriver;
            EditPreviewInstances.Remove(this);
#endif
            Teardown();
        }

        private void LateUpdate()
        {
            Tick();
        }

        private void Tick()
        {
            EnsureData();

            if (!ShouldRun)
            {
                if (_built)
                {
                    Teardown();
                }

                return;
            }

            Build();

            var now = CurrentTime;
            var deltaTime = Mathf.Clamp(now - _lastTickTime, 0f, 0.1f);
            _lastTickTime = now;

            Shader.SetGlobalFloat(TimeId, now);

            WindSimulation.Tick(wind, deltaTime);
            ApplySun();

            if (DebugDisablePointLights)
            {
                Shader.SetGlobalFloat(PointLightCountId, 0f);
            }
            else
            {
                ApplyPointLights(now);
            }

            var activeHazeLayers = DebugDisableHaze ? EmptyHazeLayers : hazeLayers;
            var activeParticleLayers = DebugDisableParticles ? EmptyParticleLayers : particleLayers;

            if (activeHazeLayers.Count != HazeStack.LayerCount)
            {
                HazeStack.Rebuild(transform, activeHazeLayers);
            }

            if (activeParticleLayers.Count != ParticleStack.LayerCount)
            {
                ParticleStack.Rebuild(transform, activeParticleLayers);
            }

            _smoothedParticleWind = Vector2.Lerp(
                _smoothedParticleWind,
                WindSimulation.Velocity,
                1f - Mathf.Exp(-deltaTime * 2.5f));

            var fitSize = CalculateCameraFitSize();
            HazeStack.ApplySettings(activeHazeLayers, fitSize);
            ParticleStack.ApplySettings(activeParticleLayers, _smoothedParticleWind, particleDensity, particleSpeed, fitSize);
            PostFx.Apply(
                transform,
                enableBloom && !DebugDisablePostFx,
                bloomIntensity,
                bloomThreshold,
                bloomScatter,
                DebugDisablePostFx ? 0f : vignetteIntensity,
                vignetteSmoothness);

            if (DebugDisableTiltShift)
            {
                DisableTiltShift();
            }
            else
            {
                ApplyTiltShift();
            }

            UpdateSpriteLighting();

            if (Application.isPlaying)
            {
                UpdateFollowCamera();
                UpdateSway(now);
                UpdateParallax();
            }
            else
            {
                UpdateSway(now);
                ParticleStack.SimulateEditorPreview(deltaTime);
            }
        }

        private Vector2? CalculateCameraFitSize()
        {
            if (!Application.isPlaying)
            {
                return null;
            }

            var camera = MainCameraCached;
            if (camera == null || !camera.orthographic)
            {
                return null;
            }

            var height = camera.orthographicSize * 2f + 3f;
            var width = camera.orthographicSize * 2f * camera.aspect + 3f;
            return new Vector2(width, height);
        }

        private void UpdateFollowCamera()
        {
            var camera = MainCameraCached;
            if (camera == null)
            {
                return;
            }

            var cameraPosition = camera.transform.position;
            var followPosition = new Vector3(cameraPosition.x, cameraPosition.y, transform.position.z);

            var fogRoot = HazeStack.Root;
            if (fogRoot != null)
            {
                fogRoot.position = followPosition;
            }

            var particleRoot = ParticleStack.Root;
            if (particleRoot != null)
            {
                particleRoot.position = followPosition;
            }
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            CleanupOrphans();
            HazeStack.Rebuild(transform, hazeLayers);
            ParticleStack.Rebuild(transform, particleLayers);
            _spriteRescanCountdown = 0;
            _swayRescanCountdown = 0;
        }

        private void CleanupOrphans()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != "AmbienceFog" && child.name != "AmbienceParticles" && child.name != "AmbiencePostFxVolume")
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void Teardown()
        {
            if (!_built)
            {
                return;
            }

            _built = false;
            _mainCamera = null;
            RestoreParallax();
            HazeStack.Clear();
            ParticleStack.Clear();
            SpriteLighting.Restore();
            WindSimulation.ResetSway();
            PostFx.Clear();
            RestoreSceneLight();
            Shader.SetGlobalFloat(PointLightCountId, 0f);
            DisableTiltShift();
        }

        private void UpdateSpriteLighting()
        {
            if (DebugDisableSpriteLighting)
            {
                if (!_spriteLightingDebugSuspended)
                {
                    _spriteLightingDebugSuspended = true;
                    SpriteLighting.Restore();
                }

                return;
            }

            if (_spriteLightingDebugSuspended)
            {
                _spriteLightingDebugSuspended = false;
                _spriteRescanCountdown = 0;
            }

            if (!sprites.apply)
            {
                return;
            }

            if (_spriteRescanCountdown <= 0)
            {
                SpriteLighting.Rescan(transform, sprites);
                _spriteRescanCountdown = Mathf.Max(sprites.rescanIntervalFrames, 1);
            }
            else
            {
                _spriteRescanCountdown--;
            }

            SpriteLighting.ApplySettings(sprites, hazeInfluenceOnSprites);
        }

        private void UpdateSway(float time)
        {
            if (!wind.apply || !wind.swayTransforms)
            {
                return;
            }

            if (_swayRescanCountdown <= 0)
            {
                WindSimulation.RescanSway(transform, wind);
                _swayRescanCountdown = Mathf.Max(wind.swayRescanIntervalFrames, 1);
            }
            else
            {
                _swayRescanCountdown--;
            }

            WindSimulation.ApplySway(wind, time);
        }

        private void ApplySun()
        {
            var radians = sun.angle * Mathf.Deg2Rad;
            var shadowDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var towardSun = -shadowDirection;

            var sunLuminance = Luminance(sun.color) * sun.intensity;
            var ambientLuminance = Luminance(sun.ambient) * sun.ambientIntensity;
            var normalization = exposure / Mathf.Max(sunLuminance + ambientLuminance, 0.05f);

            Shader.SetGlobalVector(SunDirectionId, new Vector4(towardSun.x, towardSun.y, 0f, 0f));
            Shader.SetGlobalVector(SunColorId, new Vector4(
                sun.color.r,
                sun.color.g,
                sun.color.b,
                sun.intensity * normalization));
            Shader.SetGlobalVector(AmbientColorId, new Vector4(
                sun.ambient.r * sun.ambientIntensity * normalization,
                sun.ambient.g * sun.ambientIntensity * normalization,
                sun.ambient.b * sun.ambientIntensity * normalization,
                1f));
            Shader.SetGlobalVector(HazeColorId, CalculateHazeColor());
            Shader.SetGlobalVector(GradeId, new Vector4(contrast, colorSaturation, temperature, 0f));

            if (!Application.isPlaying || !sun.applyToSceneLight)
            {
                return;
            }

            var sceneLight = FindSceneLight();
            if (sceneLight == null)
            {
                return;
            }

            if (!_lightStateSaved)
            {
                _lightStateSaved = true;
                _originalLightColor = sceneLight.color;
                _originalLightIntensity = sceneLight.intensity;
                _originalLightRotation = sceneLight.transform.rotation;
            }

            sceneLight.color = sun.color;
            sceneLight.intensity = sun.intensity * exposure;

            var elevation = sun.elevation * Mathf.Deg2Rad;
            var forward = new Vector3(
                shadowDirection.x * Mathf.Cos(elevation),
                shadowDirection.y * Mathf.Cos(elevation),
                Mathf.Sin(elevation));

            if (forward.sqrMagnitude > 0.0001f)
            {
                sceneLight.transform.rotation = Quaternion.LookRotation(forward.normalized);
            }
        }

        private void ApplyPointLights(float time)
        {
            var count = 0;
            for (var i = 0; i < pointLights.Count && count < MaxPointLights; i++)
            {
                var lightSettings = pointLights[i];
                if (lightSettings == null || !lightSettings.enabled || lightSettings.intensity <= 0.001f)
                {
                    continue;
                }

                var worldPosition = lightSettings.anchor != null
                    ? lightSettings.anchor.position + (Vector3)lightSettings.position
                    : transform.TransformPoint(lightSettings.position);

                var flickerScale = 1f;
                if (lightSettings.flicker > 0.001f)
                {
                    var noise = Mathf.PerlinNoise(time * lightSettings.flickerSpeed, i * 7.31f);
                    flickerScale = 1f - lightSettings.flicker * 0.5f + lightSettings.flicker * noise;
                }

                if (lightSettings.pulse > 0.001f)
                {
                    var wave = 0.5f + 0.5f * Mathf.Sin(time * lightSettings.pulseSpeed * Mathf.PI * 2f + i * 1.7f);
                    flickerScale *= Mathf.Lerp(1f - lightSettings.pulse * 0.45f, 1f, wave);
                }

                var strength = lightSettings.intensity * flickerScale * exposure;
                PointLightPositionsBuffer[count] = new Vector4(
                    worldPosition.x,
                    worldPosition.y,
                    Mathf.Max(lightSettings.radius, 0.05f),
                    0f);
                PointLightColorsBuffer[count] = new Vector4(
                    lightSettings.color.r * strength,
                    lightSettings.color.g * strength,
                    lightSettings.color.b * strength,
                    1f);
                count++;
            }

            for (var i = count; i < MaxPointLights; i++)
            {
                PointLightPositionsBuffer[i] = new Vector4(0f, 0f, 0.05f, 0f);
                PointLightColorsBuffer[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(PointLightPositionsId, PointLightPositionsBuffer);
            Shader.SetGlobalVectorArray(PointLightColorsId, PointLightColorsBuffer);
            Shader.SetGlobalFloat(PointLightCountId, count);
        }

        private void UpdateParallax()
        {
            if (backgroundParallax <= 0.0001f)
            {
                RestoreParallax();
                return;
            }

            var parallaxCamera = MainCameraCached;
            if (parallaxCamera == null)
            {
                return;
            }

            if (!_parallaxCaptured)
            {
                var backgrounds = SpriteLighting.Backgrounds;
                if (backgrounds.Count == 0)
                {
                    return;
                }

                _parallaxCaptured = true;
                _parallaxCameraBase = parallaxCamera.transform.position;
                for (var i = 0; i < backgrounds.Count; i++)
                {
                    var background = backgrounds[i];
                    if (background == null)
                    {
                        continue;
                    }

                    _parallaxTargets.Add(background.transform);
                    _parallaxBasePositions.Add(background.transform.position);
                }
            }

            var delta = parallaxCamera.transform.position - _parallaxCameraBase;
            delta.z = 0f;
            var offset = delta * backgroundParallax;

            for (var i = 0; i < _parallaxTargets.Count; i++)
            {
                var target = _parallaxTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.position = _parallaxBasePositions[i] + offset;
            }
        }

        private void RestoreParallax()
        {
            for (var i = 0; i < _parallaxTargets.Count; i++)
            {
                var target = _parallaxTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.position = _parallaxBasePositions[i];
            }

            _parallaxTargets.Clear();
            _parallaxBasePositions.Clear();
            _parallaxCaptured = false;
        }

        private static ScriptableRendererFeature _tiltShiftFeature;
        private static bool _tiltShiftFeatureSearched;

        private static ScriptableRendererFeature FindTiltShiftFeature()
        {
            if (_tiltShiftFeatureSearched)
            {
                return _tiltShiftFeature;
            }

            _tiltShiftFeatureSearched = true;

            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline))
            {
                return null;
            }

            var listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField == null || !(listField.GetValue(pipeline) is ScriptableRendererData[] dataList))
            {
                return null;
            }

            for (var i = 0; i < dataList.Length; i++)
            {
                var data = dataList[i];
                if (data == null)
                {
                    continue;
                }

                var features = data.rendererFeatures;
                for (var j = 0; j < features.Count; j++)
                {
                    var feature = features[j];
                    if (feature != null && feature.name == "AmbienceTiltShift")
                    {
                        _tiltShiftFeature = feature;
                        return _tiltShiftFeature;
                    }
                }
            }

            return null;
        }

        private void ApplyTiltShift()
        {
            var isActive = Application.isPlaying && tiltShiftStrength > 0.001f;

            var feature = FindTiltShiftFeature();
            if (feature != null)
            {
                if (isActive && feature is FullScreenPassRendererFeature fullScreenFeature && !fullScreenFeature.fetchColorBuffer)
                {
                    fullScreenFeature.fetchColorBuffer = true;
                }

                if (feature.isActive != isActive)
                {
                    feature.SetActive(isActive);
                }
            }

            Shader.SetGlobalFloat(TiltShiftStrengthId, isActive ? tiltShiftStrength : 0f);
            Shader.SetGlobalFloat(TiltShiftCenterId, tiltShiftCenter);
            Shader.SetGlobalFloat(TiltShiftWidthId, tiltShiftWidth);
            Shader.SetGlobalFloat(TiltShiftFeatherId, tiltShiftFeather);
        }

        private void DisableTiltShift()
        {
            if (_tiltShiftFeature != null && _tiltShiftFeature.isActive)
            {
                _tiltShiftFeature.SetActive(false);
            }

            Shader.SetGlobalFloat(TiltShiftStrengthId, 0f);
        }

        private void RestoreSceneLight()
        {
            if (!_lightStateSaved)
            {
                _sceneLight = null;
                return;
            }

            _lightStateSaved = false;
            if (_sceneLight != null)
            {
                _sceneLight.color = _originalLightColor;
                _sceneLight.intensity = _originalLightIntensity;
                _sceneLight.transform.rotation = _originalLightRotation;
            }

            _sceneLight = null;
        }

        private static float Luminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }

        private Vector4 CalculateHazeColor()
        {
            var color = Vector3.zero;
            var weight = 0f;

            for (var i = 0; i < hazeLayers.Count; i++)
            {
                var layer = hazeLayers[i];
                if (layer == null || !layer.enabled || layer.opacity <= 0.001f)
                {
                    continue;
                }

                var layerWeight = layer.opacity * (1f - layer.darken);
                color += new Vector3(layer.color.r, layer.color.g, layer.color.b) * layerWeight;
                weight += layerWeight;
            }

            if (weight <= 0.001f)
            {
                return new Vector4(1f, 1f, 1f, 0f);
            }

            color /= weight;
            return new Vector4(color.x, color.y, color.z, Mathf.Clamp01(weight));
        }

        private Light FindSceneLight()
        {
            if (_sceneLight != null)
            {
                return _sceneLight;
            }

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional)
                {
                    continue;
                }

                _sceneLight = lights[i];
                return _sceneLight;
            }

            return null;
        }

#if UNITY_EDITOR
        private static readonly List<LevelAmbience> EditPreviewInstances = new List<LevelAmbience>();

        private void EditorDriver()
        {
            if (this == null)
            {
                EditorApplication.update -= EditorDriver;
                return;
            }

            if (Application.isPlaying || !isActiveAndEnabled || !previewInEditMode || !enableAmbience)
            {
                return;
            }

            Tick();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        [InitializeOnLoadMethod]
        private static void RegisterSaveGuards()
        {
            PrefabStage.prefabSaving += _ => SuspendPreviewsForSave();
            EditorSceneManager.sceneSaving += (_, _) => SuspendPreviewsForSave();
        }

        private static void SuspendPreviewsForSave()
        {
            if (Application.isPlaying)
            {
                return;
            }

            for (var i = 0; i < EditPreviewInstances.Count; i++)
            {
                var instance = EditPreviewInstances[i];
                if (instance == null)
                {
                    continue;
                }

                instance.SpriteLighting.Restore();
                instance._spriteRescanCountdown = 0;
                instance.WindSimulation.ResetSway();
                instance._swayRescanCountdown = 0;
            }
        }
#endif
    }
}
