using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    [CreateAssetMenu(menuName = "TMN/Ambience Preset", fileName = "AmbiencePreset")]
    public sealed class AmbiencePresetSO : ScriptableObject
    {
        public AmbienceSunSettings sun = new AmbienceSunSettings();
        public AmbienceSpriteSettings sprites = new AmbienceSpriteSettings();
        public List<AmbienceHazeLayerSettings> hazeLayers = new List<AmbienceHazeLayerSettings>();
        public List<AmbienceParticleLayerSettings> particleLayers = new List<AmbienceParticleLayerSettings>();
        public AmbienceWindSettings wind = new AmbienceWindSettings();

        [Range(0f, 2f)] public float exposure = 1f;
        [Range(0.5f, 1.5f)] public float contrast = 1f;
        [Range(0f, 2f)] public float colorSaturation = 1f;
        [Range(-1f, 1f)] public float temperature;
        [Range(0f, 2f)] public float particleDensity = 1f;
        [Range(0.2f, 2f)] public float particleSpeed = 1f;
        [Range(0f, 2f)] public float hazeInfluenceOnSprites = 1f;

        public bool enableBloom;
        [Range(0f, 2f)] public float bloomIntensity = 0.9f;
        [Range(0.5f, 2f)] public float bloomThreshold = 0.85f;
        [Range(0f, 1f)] public float bloomScatter = 0.6f;
        [Range(0f, 1f)] public float vignetteIntensity;
        [Range(0f, 1f)] public float vignetteSmoothness = 0.5f;
        [Range(0f, 1f)] public float tiltShiftStrength;
        [Range(0f, 1f)] public float tiltShiftCenter = 0.5f;
        [Range(0f, 0.5f)] public float tiltShiftWidth = 0.16f;
        [Range(0.01f, 0.6f)] public float tiltShiftFeather = 0.35f;
        [Range(0f, 0.3f)] public float backgroundParallax;

        public void ApplyTo(LevelAmbience target)
        {
            CopySettings(sun, target.sun);
            CopySettings(sprites, target.sprites);
            CopySettings(wind, target.wind);
            CopyHazeLayers(hazeLayers, target.hazeLayers);
            CopyParticleLayers(particleLayers, target.particleLayers);

            target.exposure = exposure;
            target.contrast = contrast;
            target.colorSaturation = colorSaturation;
            target.temperature = temperature;
            target.particleDensity = particleDensity;
            target.particleSpeed = particleSpeed;
            target.hazeInfluenceOnSprites = hazeInfluenceOnSprites;
            target.enableBloom = enableBloom;
            target.bloomIntensity = bloomIntensity;
            target.bloomThreshold = bloomThreshold;
            target.bloomScatter = bloomScatter;
            target.vignetteIntensity = vignetteIntensity;
            target.vignetteSmoothness = vignetteSmoothness;
            target.tiltShiftStrength = tiltShiftStrength;
            target.tiltShiftCenter = tiltShiftCenter;
            target.tiltShiftWidth = tiltShiftWidth;
            target.tiltShiftFeather = tiltShiftFeather;
            target.backgroundParallax = backgroundParallax;
        }

        public void SaveFrom(LevelAmbience source)
        {
            CopySettings(source.sun, sun);
            CopySettings(source.sprites, sprites);
            CopySettings(source.wind, wind);
            CopyHazeLayers(source.hazeLayers, hazeLayers);
            CopyParticleLayers(source.particleLayers, particleLayers);

            exposure = source.exposure;
            contrast = source.contrast;
            colorSaturation = source.colorSaturation;
            temperature = source.temperature;
            particleDensity = source.particleDensity;
            particleSpeed = source.particleSpeed;
            hazeInfluenceOnSprites = source.hazeInfluenceOnSprites;
            enableBloom = source.enableBloom;
            bloomIntensity = source.bloomIntensity;
            bloomThreshold = source.bloomThreshold;
            bloomScatter = source.bloomScatter;
            vignetteIntensity = source.vignetteIntensity;
            vignetteSmoothness = source.vignetteSmoothness;
            tiltShiftStrength = source.tiltShiftStrength;
            tiltShiftCenter = source.tiltShiftCenter;
            tiltShiftWidth = source.tiltShiftWidth;
            tiltShiftFeather = source.tiltShiftFeather;
            backgroundParallax = source.backgroundParallax;
        }

        private static void CopySettings<T>(T source, T target) where T : class
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target);
        }

        private static void CopyHazeLayers(List<AmbienceHazeLayerSettings> source, List<AmbienceHazeLayerSettings> target)
        {
            target.Clear();
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                {
                    continue;
                }

                target.Add(source[i].Clone());
            }
        }

        private static void CopyParticleLayers(List<AmbienceParticleLayerSettings> source, List<AmbienceParticleLayerSettings> target)
        {
            target.Clear();
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                {
                    continue;
                }

                target.Add(source[i].Clone());
            }
        }
    }
}
