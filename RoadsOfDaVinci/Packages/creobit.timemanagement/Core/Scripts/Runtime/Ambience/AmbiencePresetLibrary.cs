using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    public enum AmbiencePreset
    {
        Custom = 0,
        ClearNoon = 1,
        WarmSunset = 2,
        Sirocco = 3,
        Pestilence = 4,
        Harvest = 5,
        Ashes = 6,
        JungleMist = 7,
        CaveDamp = 8,
        Nightfall = 9,
        StormWind = 10,
        Moonlight = 11,
        WarmDungeon = 12
    }

    public static class AmbiencePresetLibrary
    {
        public static void Apply(
            AmbiencePreset preset,
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            if (preset == AmbiencePreset.Custom)
            {
                return;
            }

            hazeLayers.Clear();
            particleLayers.Clear();

            switch (preset)
            {
                case AmbiencePreset.ClearNoon:
                    ApplyClearNoon(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.WarmSunset:
                    ApplyWarmSunset(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.Sirocco:
                    ApplySirocco(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.Pestilence:
                    ApplyPestilence(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.Harvest:
                    ApplyHarvest(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.Ashes:
                    ApplyAshes(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.JungleMist:
                    ApplyJungleMist(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.CaveDamp:
                    ApplyCaveDamp(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.Nightfall:
                    ApplyNightfall(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.StormWind:
                    ApplyStormWind(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.Moonlight:
                    ApplyMoonlight(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
                case AmbiencePreset.WarmDungeon:
                    ApplyWarmDungeon(sun, sprites, hazeLayers, particleLayers, wind);
                    break;
            }
        }

        private static void SetSprites(
            AmbienceSpriteSettings sprites,
            Color assetsTint, float volume, float rim, float groundShade, float saturation, float assetsHazeBlend,
            Color backgroundTint, float backgroundLit, float backgroundSaturation, float backgroundHazeBlend)
        {
            sprites.assetsTint = assetsTint;
            sprites.assetsLit = 1f;
            sprites.assetsVolume = volume;
            sprites.assetsRim = rim;
            sprites.assetsGroundShade = groundShade;
            sprites.assetsGroundShadeHeight = 1.1f;
            sprites.assetsSaturation = saturation;
            sprites.assetsHazeBlend = assetsHazeBlend;
            sprites.backgroundTint = backgroundTint;
            sprites.backgroundLit = backgroundLit;
            sprites.backgroundSaturation = backgroundSaturation;
            sprites.backgroundHazeBlend = backgroundHazeBlend;
        }

        private static void ApplyClearNoon(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(1f, 0.98f, 0.94f, 1f);
            sun.intensity = 1.3f;
            sun.angle = 250f;
            sun.elevation = 62f;
            sun.ambient = new Color(0.4f, 0.42f, 0.48f, 1f);
            sun.ambientIntensity = 1f;

            SetSprites(sprites,
                Color.white, 0.7f, 0.4f, 0.12f, 0.95f, 0.04f,
                Color.white, 0.97f, 0.95f, 0.08f);

            var air = NewHaze("Air", new Color(0.93f, 0.96f, 1f, 1f), 0.08f, "FogLight", 0);
            air.coverage = 0.3f;
            air.noiseScale = 2.2f;
            air.scrollSpeed = new Vector2(0.008f, 0.002f);
            air.maskBottom = 0.35f;
            air.maskFeather = 0.4f;
            hazeLayers.Add(air);

            hazeLayers.Add(NewCloudShadows(0.2f, 1));

            var motes = NewParticles("Motes", new Color(1f, 0.98f, 0.85f, 0.5f), 12f, "FogLight", 5);
            motes.sizeRange = new Vector2(0.04f, 0.09f);
            motes.lifetimeRange = new Vector2(6f, 11f);
            motes.speedRange = new Vector2(0.1f, 0.3f);
            motes.turbulence = 0.5f;
            motes.brightness = 1.8f;
            motes.flicker = 0.3f;
            particleLayers.Add(motes);

            wind.direction = 25f;
            wind.strength = 0.35f;
            wind.frequency = 0.4f;
            wind.gustAmount = 0.45f;
            wind.gustInterval = 8f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyWarmSunset(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(1f, 0.84f, 0.68f, 1f);
            sun.intensity = 1.3f;
            sun.angle = 205f;
            sun.elevation = 22f;
            sun.ambient = new Color(0.36f, 0.3f, 0.38f, 1f);
            sun.ambientIntensity = 1.05f;

            SetSprites(sprites,
                new Color(1f, 0.97f, 0.93f, 1f), 0.9f, 0.5f, 0.16f, 0.85f, 0.1f,
                new Color(1f, 0.96f, 0.91f, 1f), 0.95f, 0.82f, 0.28f);

            var glow = NewHaze("SunGlow", new Color(1f, 0.74f, 0.48f, 1f), 0.15f, "FogLight", 0);
            glow.coverage = 0.5f;
            glow.noiseScale = 1.8f;
            glow.detail = 0.35f;
            glow.softness = 0.75f;
            glow.scrollSpeed = new Vector2(0.006f, 0.0015f);
            glow.maskBottom = 0.15f;
            glow.maskFeather = 0.45f;
            hazeLayers.Add(glow);

            var lowMist = NewHaze("LowMist", new Color(0.98f, 0.78f, 0.65f, 1f), 0.12f, "FogLight", 1);
            lowMist.coverage = 0.6f;
            lowMist.noiseScale = 4.5f;
            lowMist.scrollSpeed = new Vector2(0.02f, 0.001f);
            lowMist.maskTop = 0.35f;
            lowMist.maskFeather = 0.25f;
            hazeLayers.Add(lowMist);

            hazeLayers.Add(NewCloudShadows(0.15f, 2));

            var pollen = NewParticles("Pollen", new Color(1f, 0.85f, 0.6f, 0.6f), 18f, "FogLight", 5);
            pollen.sizeRange = new Vector2(0.05f, 0.11f);
            pollen.lifetimeRange = new Vector2(6f, 10f);
            pollen.speedRange = new Vector2(0.2f, 0.5f);
            pollen.turbulence = 0.6f;
            pollen.brightness = 2.2f;
            pollen.flicker = 0.3f;
            particleLayers.Add(pollen);

            wind.direction = 15f;
            wind.strength = 0.4f;
            wind.frequency = 0.35f;
            wind.gustAmount = 0.5f;
            wind.gustInterval = 9f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplySirocco(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(1f, 0.93f, 0.8f, 1f);
            sun.intensity = 1.2f;
            sun.angle = 240f;
            sun.elevation = 45f;
            sun.ambient = new Color(0.5f, 0.45f, 0.36f, 1f);
            sun.ambientIntensity = 1.1f;

            SetSprites(sprites,
                new Color(1f, 0.98f, 0.93f, 1f), 0.6f, 0.35f, 0.12f, 0.82f, 0.14f,
                new Color(1f, 0.97f, 0.9f, 1f), 0.94f, 0.78f, 0.38f);

            var dustWall = NewHaze("DustWall", new Color(0.88f, 0.81f, 0.67f, 1f), 0.24f, "FogLight", 0);
            dustWall.coverage = 0.7f;
            dustWall.noiseScale = 2.6f;
            dustWall.detail = 0.4f;
            dustWall.softness = 0.85f;
            dustWall.scrollSpeed = new Vector2(0.05f, 0.004f);
            dustWall.windResponse = 1.6f;
            dustWall.maskFeather = 0.5f;
            hazeLayers.Add(dustWall);

            var groundDust = NewHaze("GroundDust", new Color(0.84f, 0.76f, 0.6f, 1f), 0.18f, "FogLight", 1);
            groundDust.coverage = 0.6f;
            groundDust.noiseScale = 5.5f;
            groundDust.scrollSpeed = new Vector2(0.09f, 0.002f);
            groundDust.windResponse = 2f;
            groundDust.maskTop = 0.35f;
            groundDust.maskFeather = 0.22f;
            hazeLayers.Add(groundDust);

            var sand = NewParticles("Sand", new Color(0.94f, 0.86f, 0.68f, 0.65f), 60f, "FogLight", 5);
            sand.sizeRange = new Vector2(0.03f, 0.08f);
            sand.lifetimeRange = new Vector2(2.5f, 5f);
            sand.speedRange = new Vector2(1.2f, 2.2f);
            sand.windFollow = 2.2f;
            sand.turbulence = 0.8f;
            sand.turbulenceFrequency = 0.9f;
            sand.brightness = 1.5f;
            sand.stretch = 2f;
            particleLayers.Add(sand);

            wind.direction = 8f;
            wind.strength = 1.5f;
            wind.frequency = 0.9f;
            wind.gustAmount = 1.2f;
            wind.gustInterval = 4.5f;
            wind.gustDuration = 1.8f;
        }

        private static void ApplyPestilence(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(0.85f, 0.97f, 0.86f, 1f);
            sun.intensity = 1.1f;
            sun.angle = 250f;
            sun.elevation = 55f;
            sun.ambient = new Color(0.28f, 0.44f, 0.35f, 1f);
            sun.ambientIntensity = 1.05f;

            SetSprites(sprites,
                new Color(0.97f, 1f, 0.97f, 1f), 0.85f, 0.5f, 0.16f, 0.85f, 0.12f,
                new Color(0.92f, 1f, 0.95f, 1f), 0.92f, 0.75f, 0.3f);

            var plague = NewHaze("PlagueFog", new Color(0.45f, 0.85f, 0.6f, 1f), 0.26f, "FogLight", 0);
            plague.coverage = 0.72f;
            plague.noiseScale = 2.4f;
            plague.detail = 0.6f;
            plague.softness = 0.8f;
            plague.scrollSpeed = new Vector2(0.02f, 0.006f);
            plague.maskFeather = 0.5f;
            hazeLayers.Add(plague);

            var gloom = NewHaze("Gloom", new Color(0.1f, 0.3f, 0.2f, 1f), 0.22f, "FogLight", 1);
            gloom.darken = 0.5f;
            gloom.coverage = 0.5f;
            gloom.noiseScale = 1.6f;
            gloom.scrollSpeed = new Vector2(-0.01f, 0.002f);
            gloom.maskFeather = 0.6f;
            hazeLayers.Add(gloom);

            var spores = NewParticles("Spores", new Color(0.55f, 1f, 0.7f, 0.7f), 26f, "FogLight", 5);
            spores.sizeRange = new Vector2(0.05f, 0.12f);
            spores.lifetimeRange = new Vector2(5f, 9f);
            spores.speedRange = new Vector2(0.15f, 0.4f);
            spores.turbulence = 0.9f;
            spores.brightness = 2.2f;
            spores.flicker = 0.5f;
            particleLayers.Add(spores);

            var darkMotes = NewParticles("DarkMotes", new Color(0.06f, 0.12f, 0.09f, 0.55f), 12f, "FogLight", 6);
            darkMotes.sizeRange = new Vector2(0.07f, 0.16f);
            darkMotes.lifetimeRange = new Vector2(4f, 8f);
            darkMotes.speedRange = new Vector2(0.2f, 0.5f);
            darkMotes.turbulence = 0.7f;
            darkMotes.brightness = 0.5f;
            particleLayers.Add(darkMotes);

            wind.direction = 200f;
            wind.strength = 0.5f;
            wind.frequency = 0.3f;
            wind.gustAmount = 0.7f;
            wind.gustInterval = 6f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyHarvest(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(1f, 0.94f, 0.83f, 1f);
            sun.intensity = 1.4f;
            sun.angle = 235f;
            sun.elevation = 38f;
            sun.ambient = new Color(0.44f, 0.42f, 0.4f, 1f);
            sun.ambientIntensity = 1f;

            SetSprites(sprites,
                new Color(1f, 0.98f, 0.94f, 1f), 0.75f, 0.45f, 0.14f, 0.9f, 0.08f,
                new Color(1f, 0.97f, 0.92f, 1f), 0.95f, 0.85f, 0.3f);

            var valleyFog = NewHaze("ValleyFog", new Color(0.92f, 0.9f, 0.84f, 1f), 0.26f, "FogLight", 0);
            valleyFog.coverage = 0.7f;
            valleyFog.noiseScale = 3.4f;
            valleyFog.detail = 0.45f;
            valleyFog.softness = 0.7f;
            valleyFog.scrollSpeed = new Vector2(0.012f, 0.0015f);
            valleyFog.maskTop = 0.5f;
            valleyFog.maskFeather = 0.35f;
            hazeLayers.Add(valleyFog);

            var upperHaze = NewHaze("UpperHaze", new Color(0.88f, 0.86f, 0.8f, 1f), 0.1f, "FogLight", 1);
            upperHaze.coverage = 0.45f;
            upperHaze.noiseScale = 2f;
            upperHaze.scrollSpeed = new Vector2(0.02f, 0.002f);
            upperHaze.maskBottom = 0.4f;
            upperHaze.maskFeather = 0.4f;
            hazeLayers.Add(upperHaze);

            hazeLayers.Add(NewCloudShadows(0.18f, 2));

            var leaves = NewParticles("Leaves", new Color(0.85f, 0.7f, 0.42f, 0.85f), 14f, "FogLight", 5);
            leaves.sizeRange = new Vector2(0.09f, 0.18f);
            leaves.lifetimeRange = new Vector2(5f, 9f);
            leaves.speedRange = new Vector2(0.4f, 0.9f);
            leaves.windFollow = 1.6f;
            leaves.gravity = 0.35f;
            leaves.turbulence = 1.1f;
            leaves.rotationSpeed = 60f;
            leaves.brightness = 1.15f;
            leaves.hardness = 0.35f;
            leaves.feather = 0.3f;
            particleLayers.Add(leaves);

            wind.direction = 12f;
            wind.strength = 0.6f;
            wind.frequency = 0.4f;
            wind.gustAmount = 0.8f;
            wind.gustInterval = 6.5f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyAshes(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(0.9f, 0.84f, 0.8f, 1f);
            sun.intensity = 1.05f;
            sun.angle = 245f;
            sun.elevation = 40f;
            sun.ambient = new Color(0.34f, 0.32f, 0.34f, 1f);
            sun.ambientIntensity = 0.95f;

            SetSprites(sprites,
                new Color(0.97f, 0.94f, 0.94f, 1f), 0.85f, 0.6f, 0.2f, 0.8f, 0.12f,
                new Color(0.92f, 0.9f, 0.92f, 1f), 0.9f, 0.7f, 0.35f);

            var smoke = NewHaze("Smoke", new Color(0.58f, 0.57f, 0.58f, 1f), 0.3f, "FogLight", 0);
            smoke.coverage = 0.66f;
            smoke.noiseScale = 2.8f;
            smoke.detail = 0.7f;
            smoke.softness = 0.8f;
            smoke.scrollSpeed = new Vector2(0.02f, 0.01f);
            smoke.maskFeather = 0.45f;
            hazeLayers.Add(smoke);

            var soot = NewHaze("Soot", new Color(0.08f, 0.08f, 0.09f, 1f), 0.24f, "FogLight", 1);
            soot.darken = 0.6f;
            soot.coverage = 0.45f;
            soot.noiseScale = 1.5f;
            soot.scrollSpeed = new Vector2(-0.008f, 0.004f);
            soot.maskFeather = 0.55f;
            hazeLayers.Add(soot);

            var embers = NewParticles("Embers", new Color(1f, 0.55f, 0.2f, 0.9f), 16f, "FogLight", 6);
            embers.sizeRange = new Vector2(0.03f, 0.07f);
            embers.lifetimeRange = new Vector2(4f, 7f);
            embers.speedRange = new Vector2(0.25f, 0.6f);
            embers.gravity = -0.25f;
            embers.turbulence = 0.55f;
            embers.turbulenceFrequency = 0.3f;
            embers.brightness = 3.5f;
            embers.flicker = 0.6f;
            particleLayers.Add(embers);

            var ash = NewParticles("Ash", new Color(0.78f, 0.77f, 0.75f, 0.6f), 30f, "FogLight", 5);
            ash.sizeRange = new Vector2(0.05f, 0.11f);
            ash.lifetimeRange = new Vector2(7f, 12f);
            ash.speedRange = new Vector2(0.15f, 0.4f);
            ash.gravity = 0.3f;
            ash.turbulence = 0.4f;
            ash.turbulenceFrequency = 0.3f;
            ash.brightness = 0.9f;
            particleLayers.Add(ash);

            wind.direction = 190f;
            wind.strength = 0.7f;
            wind.frequency = 0.35f;
            wind.gustAmount = 0.9f;
            wind.gustInterval = 5.5f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyJungleMist(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(0.95f, 1f, 0.92f, 1f);
            sun.intensity = 1.2f;
            sun.angle = 255f;
            sun.elevation = 58f;
            sun.ambient = new Color(0.3f, 0.42f, 0.36f, 1f);
            sun.ambientIntensity = 1.05f;

            SetSprites(sprites,
                new Color(0.98f, 1f, 0.98f, 1f), 0.9f, 0.55f, 0.16f, 0.9f, 0.08f,
                new Color(0.96f, 1f, 0.96f, 1f), 0.93f, 0.85f, 0.3f);

            var mist = NewHaze("Mist", new Color(0.84f, 0.96f, 0.9f, 1f), 0.22f, "FogLight", 0);
            mist.coverage = 0.68f;
            mist.noiseScale = 3.8f;
            mist.detail = 0.5f;
            mist.softness = 0.75f;
            mist.scrollSpeed = new Vector2(0.01f, 0.002f);
            mist.maskTop = 0.55f;
            mist.maskFeather = 0.35f;
            hazeLayers.Add(mist);

            var canopy = NewHaze("CanopyShade", new Color(0.12f, 0.25f, 0.16f, 1f), 0.16f, "FogLight", 1);
            canopy.darken = 0.45f;
            canopy.coverage = 0.4f;
            canopy.noiseScale = 1.4f;
            canopy.scrollSpeed = new Vector2(0.004f, 0.001f);
            canopy.maskFeather = 0.6f;
            hazeLayers.Add(canopy);

            var shafts = NewHaze("SunShafts", new Color(1f, 0.97f, 0.88f, 1f), 0.14f, "FogLight", 2);
            shafts.lightShafts = true;
            shafts.coverage = 0.35f;
            shafts.softness = 0.5f;
            shafts.noiseScale = 2.5f;
            shafts.scrollSpeed = new Vector2(0.004f, 0f);
            shafts.maskBottom = 0.25f;
            shafts.maskFeather = 0.5f;
            hazeLayers.Add(shafts);

            var fireflies = NewParticles("Fireflies", new Color(0.8f, 1f, 0.5f, 1f), 10f, "FogLight", 6);
            fireflies.sizeRange = new Vector2(0.035f, 0.07f);
            fireflies.lifetimeRange = new Vector2(4f, 8f);
            fireflies.speedRange = new Vector2(0.1f, 0.3f);
            fireflies.turbulence = 1.4f;
            fireflies.turbulenceFrequency = 1.2f;
            fireflies.brightness = 3.8f;
            fireflies.flicker = 0.85f;
            particleLayers.Add(fireflies);

            var pollen = NewParticles("Pollen", new Color(0.9f, 1f, 0.8f, 0.55f), 16f, "FogLight", 5);
            pollen.sizeRange = new Vector2(0.04f, 0.09f);
            pollen.lifetimeRange = new Vector2(6f, 10f);
            pollen.speedRange = new Vector2(0.15f, 0.4f);
            pollen.turbulence = 0.7f;
            pollen.brightness = 1.9f;
            particleLayers.Add(pollen);

            wind.direction = 30f;
            wind.strength = 0.45f;
            wind.frequency = 0.5f;
            wind.gustAmount = 0.6f;
            wind.gustInterval = 7f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyCaveDamp(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(0.62f, 0.76f, 1f, 1f);
            sun.intensity = 0.85f;
            sun.angle = 260f;
            sun.elevation = 70f;
            sun.ambient = new Color(0.2f, 0.24f, 0.34f, 1f);
            sun.ambientIntensity = 0.95f;

            SetSprites(sprites,
                new Color(0.9f, 0.94f, 1f, 1f), 1f, 0.8f, 0.24f, 0.85f, 0.1f,
                new Color(0.84f, 0.9f, 1f, 1f), 0.88f, 0.75f, 0.32f);

            var damp = NewHaze("Damp", new Color(0.5f, 0.62f, 0.8f, 1f), 0.22f, "FogLight", 0);
            damp.coverage = 0.6f;
            damp.noiseScale = 3f;
            damp.detail = 0.5f;
            damp.scrollSpeed = new Vector2(0.006f, 0.0015f);
            damp.maskTop = 0.5f;
            damp.maskFeather = 0.4f;
            hazeLayers.Add(damp);

            var depth = NewHaze("Depth", new Color(0.02f, 0.03f, 0.06f, 1f), 0.3f, "FogLight", 1);
            depth.darken = 0.8f;
            depth.coverage = 0.55f;
            depth.noiseScale = 1.2f;
            depth.softness = 0.9f;
            depth.scrollSpeed = new Vector2(0.002f, 0.001f);
            depth.maskFeather = 0.7f;
            hazeLayers.Add(depth);

            var godRays = NewHaze("GodRays", new Color(0.6f, 0.75f, 1f, 1f), 0.12f, "FogLight", 2);
            godRays.lightShafts = true;
            godRays.coverage = 0.3f;
            godRays.softness = 0.45f;
            godRays.noiseScale = 2f;
            godRays.scrollSpeed = new Vector2(0.003f, 0f);
            godRays.maskBottom = 0.2f;
            godRays.maskFeather = 0.5f;
            hazeLayers.Add(godRays);

            var drips = NewParticles("Drips", new Color(0.7f, 0.85f, 1f, 0.8f), 10f, "FogLight", 5);
            drips.sizeRange = new Vector2(0.025f, 0.05f);
            drips.lifetimeRange = new Vector2(1.6f, 2.8f);
            drips.speedRange = new Vector2(1.4f, 2.6f);
            drips.windFollow = 0.15f;
            drips.gravity = 2.5f;
            drips.turbulence = 0.1f;
            drips.brightness = 2.5f;
            drips.stretch = 3.5f;
            particleLayers.Add(drips);

            wind.direction = 270f;
            wind.strength = 0.15f;
            wind.frequency = 0.2f;
            wind.gustAmount = 0.2f;
            wind.gustInterval = 12f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyNightfall(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(0.6f, 0.72f, 1f, 1f);
            sun.intensity = 0.95f;
            sun.angle = 285f;
            sun.elevation = 65f;
            sun.ambient = new Color(0.18f, 0.22f, 0.36f, 1f);
            sun.ambientIntensity = 1f;

            SetSprites(sprites,
                new Color(0.88f, 0.92f, 1f, 1f), 0.9f, 0.9f, 0.2f, 0.85f, 0.1f,
                new Color(0.8f, 0.86f, 1f, 1f), 0.85f, 0.75f, 0.3f);

            var nightHaze = NewHaze("NightHaze", new Color(0.4f, 0.5f, 0.8f, 1f), 0.2f, "FogLight", 0);
            nightHaze.coverage = 0.55f;
            nightHaze.noiseScale = 2.6f;
            nightHaze.scrollSpeed = new Vector2(0.01f, 0.002f);
            nightHaze.maskFeather = 0.45f;
            hazeLayers.Add(nightHaze);

            var deepNight = NewHaze("DeepNight", new Color(0.02f, 0.03f, 0.1f, 1f), 0.3f, "FogLight", 1);
            deepNight.darken = 0.7f;
            deepNight.coverage = 0.5f;
            deepNight.noiseScale = 1.3f;
            deepNight.softness = 0.9f;
            deepNight.scrollSpeed = new Vector2(0.003f, 0.001f);
            deepNight.maskFeather = 0.65f;
            hazeLayers.Add(deepNight);

            var fireflies = NewParticles("Fireflies", new Color(1f, 0.95f, 0.55f, 1f), 14f, "FogLight", 6);
            fireflies.sizeRange = new Vector2(0.035f, 0.07f);
            fireflies.lifetimeRange = new Vector2(4f, 9f);
            fireflies.speedRange = new Vector2(0.08f, 0.25f);
            fireflies.turbulence = 1.5f;
            fireflies.turbulenceFrequency = 1.1f;
            fireflies.brightness = 4f;
            fireflies.flicker = 0.9f;
            particleLayers.Add(fireflies);

            wind.direction = 210f;
            wind.strength = 0.3f;
            wind.frequency = 0.3f;
            wind.gustAmount = 0.4f;
            wind.gustInterval = 10f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyStormWind(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(0.87f, 0.9f, 0.97f, 1f);
            sun.intensity = 1f;
            sun.angle = 225f;
            sun.elevation = 48f;
            sun.ambient = new Color(0.34f, 0.37f, 0.44f, 1f);
            sun.ambientIntensity = 1f;

            SetSprites(sprites,
                new Color(0.96f, 0.98f, 1f, 1f), 0.85f, 0.45f, 0.18f, 0.78f, 0.12f,
                new Color(0.93f, 0.95f, 1f, 1f), 0.92f, 0.72f, 0.3f);

            var stormFront = NewHaze("StormFront", new Color(0.62f, 0.68f, 0.8f, 1f), 0.26f, "FogLight", 0);
            stormFront.coverage = 0.64f;
            stormFront.noiseScale = 2.4f;
            stormFront.detail = 0.65f;
            stormFront.softness = 0.8f;
            stormFront.scrollSpeed = new Vector2(0.06f, 0.008f);
            stormFront.windResponse = 1.8f;
            stormFront.maskFeather = 0.45f;
            hazeLayers.Add(stormFront);

            var rain = NewParticles("Rain", new Color(0.78f, 0.87f, 1f, 0.65f), 90f, "FogLight", 6);
            rain.sizeRange = new Vector2(0.03f, 0.06f);
            rain.lifetimeRange = new Vector2(1.2f, 2f);
            rain.speedRange = new Vector2(5f, 7.5f);
            rain.windFollow = 1.4f;
            rain.gravity = 3.5f;
            rain.turbulence = 0.15f;
            rain.brightness = 2f;
            rain.stretch = 3.5f;
            particleLayers.Add(rain);

            var debris = NewParticles("Debris", new Color(0.62f, 0.64f, 0.57f, 0.7f), 12f, "FogLight", 5);
            debris.sizeRange = new Vector2(0.06f, 0.12f);
            debris.lifetimeRange = new Vector2(3f, 6f);
            debris.speedRange = new Vector2(1.4f, 2.6f);
            debris.windFollow = 2.4f;
            debris.turbulence = 1.3f;
            debris.rotationSpeed = 120f;
            debris.brightness = 0.9f;
            debris.hardness = 0.4f;
            debris.feather = 0.25f;
            particleLayers.Add(debris);

            wind.direction = 5f;
            wind.strength = 1.8f;
            wind.frequency = 1.1f;
            wind.gustAmount = 1.4f;
            wind.gustInterval = 3.5f;
            wind.gustDuration = 1.4f;
        }

        private static void ApplyMoonlight(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(0.72f, 0.82f, 1f, 1f);
            sun.intensity = 1.15f;
            sun.angle = 290f;
            sun.elevation = 55f;
            sun.ambient = new Color(0.16f, 0.2f, 0.33f, 1f);
            sun.ambientIntensity = 1f;

            SetSprites(sprites,
                new Color(0.9f, 0.94f, 1f, 1f), 1f, 1f, 0.22f, 0.7f, 0.1f,
                new Color(0.82f, 0.88f, 1f, 1f), 0.85f, 0.6f, 0.3f);

            var moonHaze = NewHaze("MoonHaze", new Color(0.55f, 0.65f, 0.95f, 1f), 0.18f, "FogLight", 0);
            moonHaze.coverage = 0.5f;
            moonHaze.noiseScale = 2.4f;
            moonHaze.scrollSpeed = new Vector2(0.008f, 0.0015f);
            moonHaze.maskFeather = 0.5f;
            hazeLayers.Add(moonHaze);

            var moonRays = NewHaze("MoonRays", new Color(0.75f, 0.85f, 1f, 1f), 0.12f, "FogLight", 1);
            moonRays.lightShafts = true;
            moonRays.coverage = 0.3f;
            moonRays.softness = 0.5f;
            moonRays.noiseScale = 2.2f;
            moonRays.scrollSpeed = new Vector2(0.003f, 0f);
            moonRays.maskBottom = 0.2f;
            moonRays.maskFeather = 0.5f;
            hazeLayers.Add(moonRays);

            hazeLayers.Add(NewCloudShadows(0.3f, 2));

            var fireflies = NewParticles("Fireflies", new Color(0.8f, 0.9f, 1f, 0.9f), 10f, "FogLight", 6);
            fireflies.sizeRange = new Vector2(0.035f, 0.07f);
            fireflies.lifetimeRange = new Vector2(4f, 8f);
            fireflies.speedRange = new Vector2(0.08f, 0.25f);
            fireflies.turbulence = 1.2f;
            fireflies.turbulenceFrequency = 0.8f;
            fireflies.brightness = 3.5f;
            fireflies.flicker = 0.8f;
            particleLayers.Add(fireflies);

            var motes = NewParticles("SilverMotes", new Color(0.85f, 0.9f, 1f, 0.4f), 10f, "FogLight", 5);
            motes.sizeRange = new Vector2(0.03f, 0.07f);
            motes.lifetimeRange = new Vector2(6f, 11f);
            motes.speedRange = new Vector2(0.08f, 0.25f);
            motes.turbulence = 0.5f;
            motes.brightness = 2f;
            motes.flicker = 0.3f;
            particleLayers.Add(motes);

            wind.direction = 210f;
            wind.strength = 0.35f;
            wind.frequency = 0.3f;
            wind.gustAmount = 0.4f;
            wind.gustInterval = 9f;
            wind.gustDuration = 2.2f;
        }

        private static void ApplyWarmDungeon(
            AmbienceSunSettings sun,
            AmbienceSpriteSettings sprites,
            List<AmbienceHazeLayerSettings> hazeLayers,
            List<AmbienceParticleLayerSettings> particleLayers,
            AmbienceWindSettings wind)
        {
            sun.color = new Color(1f, 0.9f, 0.78f, 1f);
            sun.intensity = 1.05f;
            sun.angle = 245f;
            sun.elevation = 52f;
            sun.ambient = new Color(0.32f, 0.25f, 0.22f, 1f);
            sun.ambientIntensity = 1f;

            SetSprites(sprites,
                new Color(1f, 0.98f, 0.95f, 1f), 0.75f, 0.35f, 0.24f, 0.95f, 0.05f,
                new Color(0.9f, 0.84f, 0.79f, 1f), 0.78f, 0.88f, 0.18f);

            var upperShade = NewHaze("UpperShade", new Color(0.2f, 0.13f, 0.1f, 1f), 0.18f, "FogLight", 0);
            upperShade.darken = 0.45f;
            upperShade.coverage = 0.58f;
            upperShade.noiseScale = 1.35f;
            upperShade.detail = 0.35f;
            upperShade.softness = 0.8f;
            upperShade.scrollSpeed = new Vector2(0.003f, 0.001f);
            upperShade.windResponse = 0.25f;
            upperShade.maskBottom = 0.25f;
            upperShade.maskFeather = 0.45f;
            hazeLayers.Add(upperShade);

            var warmAir = NewHaze("WarmAir", new Color(0.88f, 0.63f, 0.42f, 1f), 0.07f, "FogLight", 1);
            warmAir.coverage = 0.4f;
            warmAir.noiseScale = 2.8f;
            warmAir.detail = 0.35f;
            warmAir.softness = 0.75f;
            warmAir.scrollSpeed = new Vector2(0.006f, 0.001f);
            warmAir.windResponse = 0.4f;
            warmAir.maskBottom = 0.1f;
            warmAir.maskFeather = 0.5f;
            hazeLayers.Add(warmAir);

            var dust = NewParticles("WarmDust", new Color(1f, 0.78f, 0.52f, 0.35f), 6f, "FogLight", 5);
            dust.sizeRange = new Vector2(0.025f, 0.055f);
            dust.lifetimeRange = new Vector2(6f, 10f);
            dust.speedRange = new Vector2(0.06f, 0.18f);
            dust.windFollow = 0.6f;
            dust.turbulence = 0.45f;
            dust.turbulenceFrequency = 0.35f;
            dust.brightness = 1.4f;
            dust.flicker = 0.2f;
            particleLayers.Add(dust);

            wind.direction = 205f;
            wind.strength = 0.18f;
            wind.frequency = 0.25f;
            wind.gustAmount = 0.22f;
            wind.gustInterval = 12f;
            wind.gustDuration = 2.4f;
        }

        private static AmbienceHazeLayerSettings NewCloudShadows(float strength, int sortingOrder)
        {
            var layer = NewHaze("CloudShadows", new Color(0.05f, 0.07f, 0.12f, 1f), strength, "FogLight", sortingOrder);
            layer.darken = 0.85f;
            layer.coverage = 0.42f;
            layer.noiseScale = 1.1f;
            layer.detail = 0.35f;
            layer.softness = 0.65f;
            layer.scrollSpeed = new Vector2(0.014f, 0.004f);
            layer.windResponse = 1.2f;
            layer.maskFeather = 0.6f;
            return layer;
        }

        private static AmbienceHazeLayerSettings NewHaze(string layerName, Color color, float opacity, string sortingLayer, int sortingOrder)
        {
            return new AmbienceHazeLayerSettings
            {
                enabled = true,
                layerName = layerName,
                color = color,
                opacity = opacity,
                sortingLayer = sortingLayer,
                sortingOrder = sortingOrder,
                depth = -1f - sortingOrder * 0.05f
            };
        }

        private static AmbienceParticleLayerSettings NewParticles(string layerName, Color color, float rate, string sortingLayer, int sortingOrder)
        {
            return new AmbienceParticleLayerSettings
            {
                enabled = true,
                layerName = layerName,
                color = color,
                rate = rate,
                sortingLayer = sortingLayer,
                sortingOrder = sortingOrder,
                depth = -2f - sortingOrder * 0.05f
            };
        }
    }
}
