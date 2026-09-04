using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    public enum WaterSurfaceDecalKind
    {
        Ripple,
        Footprint,
        Downwash
    }

    public sealed class WaterSurfaceDecal : MonoBehaviour
    {
        private static readonly List<WaterSurfaceDecal> Pool = new();
        private static readonly Color32 Transparent = new(255, 255, 255, 0);

        private static Transform _root;
        private static Sprite _rippleSprite;
        private static Sprite _footprintSprite;
        private static Sprite _downwashSprite;
        private static bool _spritesReady;

        private SpriteRenderer _renderer;
        private WaterSurfaceDecalKind _kind;
        private Color _color;
        private Vector3 _startScale;
        private Vector3 _endScale;
        private float _duration;
        private float _age;
        private bool _held;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
        }

        public static WaterSurfaceDecal Play(
            WaterSurfaceDecalKind kind,
            Vector3 position,
            Quaternion rotation,
            Vector3 startScale,
            Vector3 endScale,
            Color color,
            float duration,
            string sortingLayerName,
            int sortingOrder)
        {
            var decal = Prepare(kind, position, rotation, startScale, color, sortingLayerName, sortingOrder);
            decal._endScale = endScale;
            decal._duration = Mathf.Max(0.05f, duration);
            decal._held = false;
            decal.ApplyAlpha(0f);
            return decal;
        }

        public static WaterSurfaceDecal Hold(
            WaterSurfaceDecalKind kind,
            Vector3 position,
            Vector3 scale,
            Color color,
            string sortingLayerName,
            int sortingOrder)
        {
            var decal = Prepare(kind, position, Quaternion.identity, scale, color, sortingLayerName, sortingOrder);
            decal._endScale = scale;
            decal._duration = 1f;
            decal._held = true;
            decal.ApplyColor(color);
            return decal;
        }

        public void UpdateHeld(Vector3 position, Vector3 scale, Color color)
        {
            if (!_held)
            {
                return;
            }

            transform.position = position;
            transform.localScale = scale;
            _color = color;
            _startScale = scale;
            _endScale = scale;
            ApplyColor(color);
        }

        public void ReleaseHeld(float fadeDuration)
        {
            if (!_held)
            {
                return;
            }

            _held = false;

            if (fadeDuration <= 0f)
            {
                Release();
                return;
            }

            _age = 0f;
            _duration = fadeDuration;
            _startScale = transform.localScale;
            _endScale = _startScale * 1.18f;
        }

        private static WaterSurfaceDecal Prepare(
            WaterSurfaceDecalKind kind,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Color color,
            string sortingLayerName,
            int sortingOrder)
        {
            EnsureSprites();

            var decal = Rent();
            decal._kind = kind;
            decal._color = color;
            decal._startScale = scale;
            decal._age = 0f;

            decal.transform.SetPositionAndRotation(position, rotation);
            decal.transform.localScale = scale;

            if (decal._renderer != null)
            {
                decal._renderer.sprite = SpriteFor(kind);
                if (!string.IsNullOrEmpty(sortingLayerName))
                {
                    decal._renderer.sortingLayerName = sortingLayerName;
                }

                decal._renderer.sortingOrder = sortingOrder;
            }

            decal.gameObject.SetActive(true);
            return decal;
        }

        private static Sprite SpriteFor(WaterSurfaceDecalKind kind)
        {
            switch (kind)
            {
                case WaterSurfaceDecalKind.Footprint:
                    return _footprintSprite;
                case WaterSurfaceDecalKind.Downwash:
                    return _downwashSprite;
                default:
                    return _rippleSprite;
            }
        }

        private void Update()
        {
            if (_held)
            {
                return;
            }

            _age += Time.deltaTime;
            var t = Mathf.Clamp01(_age / _duration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);

            transform.localScale = Vector3.LerpUnclamped(_startScale, _endScale, eased);
            ApplyAlpha(t);

            if (t >= 1f)
            {
                Release();
            }
        }

        private void ApplyAlpha(float t)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * AlphaCurve(t));
        }

        private void ApplyColor(Color color)
        {
            if (_renderer != null)
            {
                _renderer.color = color;
            }
        }

        private float AlphaCurve(float t)
        {
            switch (_kind)
            {
                case WaterSurfaceDecalKind.Ripple:
                {
                    var rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.15f));
                    var fall = 1f - Mathf.SmoothStep(0.2f, 1f, t);
                    return rise * fall;
                }
                case WaterSurfaceDecalKind.Downwash:
                    return 1f - Mathf.SmoothStep(0f, 1f, t);
                default:
                    return 1f - Mathf.SmoothStep(0.35f, 1f, t);
            }
        }

        private static WaterSurfaceDecal Rent()
        {
            EnsureRoot();

            for (var i = Pool.Count - 1; i >= 0; i--)
            {
                var item = Pool[i];
                Pool.RemoveAt(i);
                if (item != null)
                {
                    return item;
                }
            }

            return Create();
        }

        private void Release()
        {
            _held = false;
            gameObject.SetActive(false);
            Pool.Add(this);
        }

        private static WaterSurfaceDecal Create()
        {
            var go = new GameObject("WaterSurfaceDecal");
            go.transform.SetParent(_root, false);
            go.hideFlags = HideFlags.DontSave;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;

            var decal = go.AddComponent<WaterSurfaceDecal>();
            decal._renderer = renderer;
            return decal;
        }

        private static void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            var go = new GameObject("WaterSurfaceDecals");
            go.hideFlags = HideFlags.DontSave;
            _root = go.transform;
        }

        private static void EnsureSprites()
        {
            if (_spritesReady)
            {
                return;
            }

            _rippleSprite = BuildRippleSprite();
            _footprintSprite = BuildFootprintSprite();
            _downwashSprite = BuildDownwashSprite();
            _spritesReady = true;
        }

        private static Sprite BuildRippleSprite()
        {
            const int size = 128;
            var tex = MakeTexture(size);
            var pixels = new Color32[size * size];
            var cx = (size - 1) * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = (x - cx) / cx;
                    var ny = (y - cx) / cx;
                    var d = Mathf.Sqrt(nx * nx + ny * ny);
                    if (d > 1f)
                    {
                        pixels[y * size + x] = Transparent;
                        continue;
                    }

                    var ring1 = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.65f) / 0.16f);
                    ring1 = ring1 * ring1 * (3f - 2f * ring1);

                    var ring2 = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.32f) / 0.14f);
                    ring2 = ring2 * ring2 * (3f - 2f * ring2) * 0.75f;

                    var center = 1f - Mathf.Clamp01(d / 0.18f);
                    center = center * center * 0.25f;

                    var a = Mathf.Clamp01(ring1 + ring2 + center);
                    pixels[y * size + x] = White(a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildFootprintSprite()
        {
            const int size = 128;
            var tex = MakeTexture(size);
            var pixels = new Color32[size * size];
            var cx = (size - 1) * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = (x - cx) / cx;
                    var ny = (y - cx) / cx;

                    var dxH = nx / 0.28f;
                    var dyH = (ny + 0.34f) / 0.24f;
                    var dH = Mathf.Sqrt(dxH * dxH + dyH * dyH);
                    var aHeel = 1f - Mathf.SmoothStep(0.7f, 1.0f, dH);

                    var dxB = nx / 0.34f;
                    var dyB = (ny - 0.24f) / 0.36f;
                    var dB = Mathf.Sqrt(dxB * dxB + dyB * dyB);
                    var aBall = 1f - Mathf.SmoothStep(0.7f, 1.0f, dB);

                    var dxW = nx / 0.22f;
                    var dyW = (ny + 0.04f) / 0.26f;
                    var dW = Mathf.Sqrt(dxW * dxW + dyW * dyW);
                    var aWaist = 1f - Mathf.SmoothStep(0.7f, 1.0f, dW);

                    var a = Mathf.Clamp01(Mathf.Max(aHeel, Mathf.Max(aBall, aWaist * 0.85f)));
                    pixels[y * size + x] = White(a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildDownwashSprite()
        {
            const int size = 256;
            var tex = MakeTexture(size);
            var pixels = new Color32[size * size];
            var cx = (size - 1) * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = (x - cx) / cx;
                    var ny = (y - cx) / cx;
                    var d = Mathf.Sqrt(nx * nx + ny * ny);
                    if (d > 1f)
                    {
                        pixels[y * size + x] = Transparent;
                        continue;
                    }

                    var angle = Mathf.Atan2(ny, nx);

                    var turbulence =
                        Mathf.Sin(angle * 6f + d * 15f) * 0.5f +
                        Mathf.Sin(angle * 11f - d * 23f) * 0.3f +
                        Mathf.Sin(angle * 17f + d * 31f) * 0.2f;
                    turbulence = turbulence * 0.5f + 0.5f;

                    var rim = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.74f) / 0.26f);
                    rim = rim * rim * (3f - 2f * rim);

                    var core = 1f - Mathf.SmoothStep(0.15f, 0.92f, d);

                    var foam = Mathf.Clamp01(core * 0.55f + rim * (0.45f + turbulence * 0.55f));
                    foam *= 1f - Mathf.SmoothStep(0.86f, 1f, d);

                    var speckle = Mathf.Sin(angle * 29f + d * 47f) * Mathf.Sin(angle * 13f - d * 19f);
                    foam += Mathf.Clamp01(speckle) * rim * 0.25f;

                    pixels[y * size + x] = White(Mathf.Clamp01(foam));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        private static Color32 White(float alpha)
        {
            return new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
        }

        private static Texture2D MakeTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
