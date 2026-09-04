using System;
using System.Collections.Generic;
using System.Threading;
using Creobit.Audio;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.SeedEffect
{
    /// <summary>
    /// GG2 magic-tree seed effect. Port of the original EffectSeed: on final restoration the seed spawns
    /// at the tree (tree position + offset, so it starts from the centre of the crown), reveals with a
    /// grow-overshoot appear animation, then flies along an upward quadratic Bezier arc to the centre of
    /// the screen, holds there, and tears itself down after the particle tail. The endpoint is the active
    /// camera viewport centre. Render sorting is owned by the visual prefab, not this script. Spawned
    /// instances are tracked and destroyed when this component is destroyed (level reload), so nothing
    /// stays on screen. Runs on unscaled time so it never blocks or pauses gameplay.
    /// </summary>
    public class SeedFlightEffect : MonoBehaviour
    {
        [SerializeField] private SeedFlightSettingsSO _settings;

        private IAudioService _audioService;
        private readonly List<GameObject> _active = new();

        // Точная пружина появления из оригинала (effSeed.anim), нормализованная по времени в 0..1:
        // 0.1 → 1.0 → 1.1 → 0.95 → 1.025 → 1.0. Сглажена SmoothTangents для пружинистости.
        private static readonly AnimationCurve AppearSpring = BuildAppearSpring();

        private static AnimationCurve BuildAppearSpring()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.1f),
                new Keyframe(0.6f, 1f),
                new Keyframe(0.7f, 1.1f),
                new Keyframe(0.8f, 0.95f),
                new Keyframe(0.9f, 1.025f),
                new Keyframe(1f, 1f));

            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }

        public SeedFlightSettingsSO Settings => _settings;

        [Inject]
        private void Construct(IAudioService audioService)
        {
            _audioService = audioService;
        }

        private void OnDestroy()
        {
            // Level reload / tree teardown: kill any seed still flying or holding so it can't linger.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] != null)
                {
                    Destroy(_active[i]);
                }
            }

            _active.Clear();
        }

        /// <summary>
        /// Entry point wired by the tool to the tree's final restoration event (alongside the take_seed
        /// AddTaskProgress). Fire-and-forget.
        /// </summary>
        public void Play(int seedVariant)
        {
            if (_settings == null)
            {
                Debug.LogError($"{nameof(SeedFlightEffect)} on {name}: no settings assigned.", this);
                return;
            }

            if (_settings.FlightVisualPrefab == null)
            {
                Debug.LogError($"{nameof(SeedFlightEffect)} on {name}: {nameof(_settings.FlightVisualPrefab)} is null.", this);
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError($"{nameof(SeedFlightEffect)} on {name}: no main camera to aim the seed at.", this);
                return;
            }

            var sprite = _settings.GetSeedSprite(seedVariant);
            if (sprite == null)
            {
                return; // GetSeedSprite already logged the missing variant.
            }

            FlyAsync(transform.position + _settings.SpawnOffset, camera, sprite).Forget();
        }

        private async UniTaskVoid FlyAsync(Vector3 from, Camera camera, Sprite sprite)
        {
            var instance = Instantiate(_settings.FlightVisualPrefab);
            _active.Add(instance);

            // Cancel the flight if either the spawned instance OR this component (level reload) dies.
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                instance.GetCancellationTokenOnDestroy(),
                this.GetCancellationTokenOnDestroy());
            var token = linkedCts.Token;

            try
            {
                var root = instance.transform;

                var seedRenderer = instance.GetComponentInChildren<SpriteRenderer>(true);
                if (seedRenderer != null)
                {
                    seedRenderer.sprite = sprite;
                }

                var particles = instance.GetComponentInChildren<ParticleSystem>(true);

                // Endpoint: screen centre on the same depth plane as the start.
                var depth = camera.WorldToViewportPoint(from).z;
                var to = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
                from.z = to.z;
                root.position = from;

                // Final/settle scale = the prefab's own scale times the SO multiplier.
                var targetScale = root.localScale * _settings.Scale;
                root.localScale = Vector3.zero;

                if (particles != null)
                {
                    particles.Play(true);
                }

                if (_settings.Sound != null)
                {
                    _audioService?.PlaySfx(_settings.Sound);
                }

                // Пружина появления и полёт идут ОДНОВРЕМЕННО (как в оригинале: Animation играет
                // на awake, пока корутина Flying двигает семечку). Scale меняет AppearAsync,
                // позицию — TravelAsync, они не конфликтуют.
                await UniTask.WhenAll(
                    AppearAsync(root, targetScale, token),
                    TravelAsync(root, from, to, token));

                if (root != null)
                {
                    root.position = to;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_settings.HoldSeconds),
                    DelayType.UnscaledDeltaTime, cancellationToken: token).SuppressCancellationThrow();

                if (particles != null)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_settings.ParticleTailSeconds),
                    DelayType.UnscaledDeltaTime, cancellationToken: token).SuppressCancellationThrow();
            }
            finally
            {
                linkedCts.Dispose();
                _active.Remove(instance);
                if (instance != null)
                {
                    Destroy(instance);
                }
            }
        }

        private async UniTask AppearAsync(Transform target, Vector3 targetScale, CancellationToken token)
        {
            if (_settings.AppearSeconds <= 0f || target == null)
            {
                if (target != null)
                {
                    target.localScale = targetScale;
                }

                return;
            }

            target.localScale = targetScale * AppearSpring.Evaluate(0f);

            var elapsed = 0f;
            while (elapsed < _settings.AppearSeconds)
            {
                if (token.IsCancellationRequested || target == null)
                {
                    return;
                }

                var t = Mathf.Clamp01(elapsed / _settings.AppearSeconds);
                target.localScale = targetScale * AppearSpring.Evaluate(t);

                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }

            if (target != null)
            {
                target.localScale = targetScale;
            }
        }

        private async UniTask TravelAsync(Transform target, Vector3 from, Vector3 to, CancellationToken token)
        {
            var flat = to - from;
            var distance = flat.magnitude;
            if (distance <= Mathf.Epsilon || target == null)
            {
                return;
            }

            var direction = flat / distance;
            var mid = from + direction * (distance * 0.5f);
            mid.y = Mathf.Max(from.y, to.y) + _settings.ArcHeight;

            var travelled = 0f;
            while (travelled < distance)
            {
                if (token.IsCancellationRequested || target == null)
                {
                    return;
                }

                travelled += _settings.Speed * Time.unscaledDeltaTime;
                target.position = QuadraticBezier(from, mid, to, Mathf.Clamp01(travelled / distance));

                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }
        }

        private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            var inv = 1f - t;
            return p0 * (inv * inv) + p1 * (2f * t * inv) + p2 * (t * t);
        }
    }
}
