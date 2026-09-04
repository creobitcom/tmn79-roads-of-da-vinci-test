using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    [Serializable]
    public sealed class AmbienceSunSettings
    {
        public bool applyToSceneLight = true;
        public Color color = new Color(1f, 0.96f, 0.88f, 1f);
        [Range(0f, 4f)] public float intensity = 1.55f;
        [Range(0f, 360f)] public float angle = 235f;
        [Range(0f, 89f)] public float elevation = 50f;
        public Color ambient = new Color(0.33f, 0.35f, 0.42f, 1f);
        [Range(0f, 3f)] public float ambientIntensity = 1f;
    }

    [Serializable]
    public sealed class AmbienceSpriteSettings
    {
        public bool apply = true;

        public Color assetsTint = Color.white;
        [Range(0f, 1f)] public float assetsLit = 1f;
        [Range(0f, 4f)] public float assetsVolume = 0.9f;
        [Range(0f, 2f)] public float assetsRim = 0.35f;
        [Range(0f, 1f)] public float assetsGroundShade = 0.22f;
        [Range(0.05f, 6f)] public float assetsGroundShadeHeight = 1.1f;
        [Range(0f, 2f)] public float assetsSaturation = 1f;
        [Range(0f, 1f)] public float assetsHazeBlend = 0.12f;

        public bool includeBackground = true;
        public Color backgroundTint = Color.white;
        [Range(0f, 1f)] public float backgroundLit = 0.85f;
        [Range(0f, 2f)] public float backgroundSaturation = 1f;
        [Range(0f, 1f)] public float backgroundHazeBlend = 0.3f;
        [Range(0f, 1f)] public float backgroundGradeInfluence = 1f;
        public bool overrideBackgroundGrade;
        [Range(0.5f, 1.5f)] public float backgroundContrast = 1f;
        [Range(-1f, 1f)] public float backgroundTemperature;

        public bool useShaderWind;
        [Range(0f, 2f)] public float shaderWindStrength = 1f;

        [FoldoutGroup("Filters (Advanced)")]
        [Range(1, 240)] public int rescanIntervalFrames = 30;

        [FoldoutGroup("Filters (Advanced)")]
        public string[] backgroundNameParts =
        {
            "Background", "FHD", "LevelBack", "Back_"
        };

        [FoldoutGroup("Filters (Advanced)")]
        public string[] windNameParts =
        {
            "weed", "bush", "grass", "roots", "tree", "flag", "flower", "web", "berry"
        };

        [FoldoutGroup("Filters (Advanced)")]
        public string[] skipNameParts =
        {
            "Glow", "Highlight", "Outline", "Frame", "Fog", "Shadow", "Particle", "Selection", "Arrow", "Icon",
            "Exclamation", "_mark", "Hint", "OnBlockPath", "Badge", "Bubble", "UpgradeMark", "Upgrade"
        };

        [FoldoutGroup("Filters (Advanced)")]
        public string[] skipSortingLayers =
        {
            "UI", "Fog", "FogLight"
        };
    }

    [Serializable]
    public sealed class AmbiencePointLightSettings
    {
        public bool enabled = true;
        public string lightName = "Torch";
        public Transform anchor;
        public Vector2 position;
        public Color color = new Color(1f, 0.62f, 0.25f, 1f);
        [Range(0f, 8f)] public float intensity = 1.8f;
        [Range(0.1f, 15f)] public float radius = 3.5f;
        [Range(0f, 1f)] public float flicker = 0.4f;
        [Range(0.1f, 20f)] public float flickerSpeed = 7f;
        [Range(0f, 1f)] public float pulse;
        [Range(0.1f, 10f)] public float pulseSpeed = 1.2f;
    }

    [Serializable]
    public sealed class AmbienceHazeLayerSettings
    {
        public bool enabled = true;
        public string layerName = "Haze";
        public Color color = new Color(0.85f, 0.9f, 0.95f, 1f);
        [Range(0f, 1f)] public float opacity = 0.3f;
        [Range(0f, 1f)] public float darken;
        public bool lightShafts;

        public Texture2D texture;
        [Range(0.1f, 20f)] public float noiseScale = 3f;
        [Range(0f, 1f)] public float detail = 0.55f;
        [Range(0f, 1f)] public float coverage = 0.5f;
        [Range(0.01f, 1f)] public float softness = 0.5f;

        public Vector2 scrollSpeed = new Vector2(0.015f, 0.003f);
        [Range(0f, 2f)] public float windResponse = 1f;

        [Range(0f, 1f)] public float maskBottom;
        [Range(0f, 1f)] public float maskTop = 1f;
        [Range(0.001f, 1f)] public float maskFeather = 0.3f;
        [Range(0f, 0.5f)] public float edgeFade = 0.06f;

        public bool fitToCamera = true;
        public Vector2 size = new Vector2(28f, 16f);
        public Vector2 offset = Vector2.zero;
        public string sortingLayer = "FogLight";
        public int sortingOrder;
        [Range(-20f, 20f)] public float depth = -1f;

        public AmbienceHazeLayerSettings Clone()
        {
            return (AmbienceHazeLayerSettings)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class AmbienceParticleLayerSettings
    {
        public bool enabled = true;
        public string layerName = "Dust";
        public Transform anchor;
        public Color color = new Color(1f, 0.95f, 0.82f, 0.45f);
        [Range(0f, 600f)] public float rate = 30f;

        public Vector2 sizeRange = new Vector2(0.03f, 0.09f);
        public Vector2 lifetimeRange = new Vector2(5f, 9f);
        public Vector2 speedRange = new Vector2(0.15f, 0.5f);

        [Range(0f, 3f)] public float windFollow = 1f;
        [Range(-4f, 4f)] public float gravity;
        [Range(0f, 3f)] public float turbulence = 0.4f;
        [Range(0f, 3f)] public float turbulenceFrequency = 0.5f;
        [Range(-180f, 180f)] public float rotationSpeed;
        [Range(0f, 1f)] public float flicker;
        [Range(0f, 4f)] public float brightness = 1f;

        [Range(0.01f, 1f)] public float feather = 0.55f;
        [Range(0f, 1f)] public float hardness = 0.15f;
        [Range(0f, 8f)] public float stretch;
        public Texture2D texture;

        public bool fitToCamera = true;
        public Vector2 area = new Vector2(28f, 16f);
        public Vector2 areaOffset = Vector2.zero;
        public string sortingLayer = "FogLight";
        public int sortingOrder = 5;
        [Range(-20f, 20f)] public float depth = -2f;

        public AmbienceParticleLayerSettings Clone()
        {
            return (AmbienceParticleLayerSettings)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class AmbienceWindSettings
    {
        public bool apply = true;
        [Range(0f, 360f)] public float direction = 20f;
        [Range(0f, 3f)] public float strength = 0.5f;
        [Range(0f, 3f)] public float frequency = 0.45f;
        [Range(0f, 2f)] public float gustAmount = 0.6f;
        [Range(0.2f, 30f)] public float gustInterval = 7f;
        [Range(0.1f, 10f)] public float gustDuration = 2.2f;

        public bool swayTransforms;
        [Range(0f, 20f)] public float swayAngle = 2.5f;
        [Range(0f, 1f)] public float swayOffset = 0.15f;
        [Range(1, 240)] public int swayRescanIntervalFrames = 60;

        public string[] swayNameParts =
        {
            "flag", "tent", "banner"
        };
    }
}
