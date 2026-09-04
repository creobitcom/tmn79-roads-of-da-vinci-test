using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.ScanSweep
{
    public class ScanSweepView : MonoBehaviour
    {
        private const string OverlaySuffix = "_ScanGlow";
        private const float HiddenProgress = 2f;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int SweepAxisId = Shader.PropertyToID("_SweepAxis");
        private static readonly int BandColorId = Shader.PropertyToID("_BandColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int BandWidthId = Shader.PropertyToID("_BandWidth");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [SerializeField]
        [Tooltip("Объект, за взаимодействием которого следим. Пусто — берётся с этого же GameObject или с родителя.")]
        private ObjectView objectView;

        [SerializeField]
        [Tooltip("Материал с шейдером TMN/ScanSweep.")]
        private Material sweepMaterial;

        [SerializeField]
        [Tooltip("Общий пресет настроек. Пусто — используются настройки ниже.")]
        private ScanSweepSettings preset;

        [SerializeField]
        private ScanSweepParams sweep = new ScanSweepParams();

        [SerializeField]
        [Tooltip("Теги юнитов, на которых включается развёртка (тег дрона). Пусто — на любом юните.")]
        private GameplayTagSO[] unitTags;

        [SerializeField]
        [Tooltip("Спрайты, по которым едет полоса. Пусто — берётся самый крупный спрайт объекта.")]
        private SpriteRenderer[] targetRenderers;

        private readonly List<SpriteRenderer> _sources = new();
        private readonly List<SpriteRenderer> _overlays = new();
        private MaterialPropertyBlock _propertyBlock;
        private CancellationTokenSource _sweepCts;
        private bool _isSubscribed;
        private bool _finishRequested;

        public ScanSweepParams ActiveSweep => preset != null ? preset.Sweep : sweep;

        private ScanSweepParams Sweep => ActiveSweep;

        private void Awake()
        {
            if (objectView == null)
            {
                objectView = GetComponent<ObjectView>();
            }

            if (objectView == null)
            {
                objectView = GetComponentInParent<ObjectView>();
            }

            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopSweep();
            HideOverlays();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            StopSweep();
        }

        private void Subscribe()
        {
            if (_isSubscribed || objectView == null)
            {
                return;
            }

            objectView.OnStartInteract += HandleStartInteract;
            objectView.OnEndInteract += HandleEndInteract;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || objectView == null)
            {
                return;
            }

            objectView.OnStartInteract -= HandleStartInteract;
            objectView.OnEndInteract -= HandleEndInteract;
            _isSubscribed = false;
        }

        private void HandleStartInteract(ObjectView view)
        {
            if (!MatchesUnits())
            {
                return;
            }

            StopSweep();

            _finishRequested = false;
            _sweepCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunSweep(_sweepCts.Token).Forget();
        }

        private void HandleEndInteract(ObjectView view)
        {
            _finishRequested = true;
        }

        private void StopSweep()
        {
            if (_sweepCts == null)
            {
                return;
            }

            _sweepCts.Cancel();
            _sweepCts.Dispose();
            _sweepCts = null;
        }

        private bool MatchesUnits()
        {
            if (unitTags == null || unitTags.Length == 0)
            {
                return true;
            }

            var task = objectView != null ? objectView.CurrentTask : null;
            if (task?.Units == null)
            {
                return false;
            }

            foreach (var unit in task.Units)
            {
                if (HasAnyTag(unit))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAnyTag(MovableObjectView unit)
        {
            var tags = unit != null && unit.ObjectDataSO != null ? unit.ObjectDataSO.ObjectTypeTags : null;
            if (tags == null)
            {
                return false;
            }

            foreach (var unitTag in unitTags)
            {
                if (unitTag == null)
                {
                    continue;
                }

                foreach (var tag in tags)
                {
                    if (tag != null && tag.Equals(unitTag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async UniTaskVoid RunSweep(CancellationToken token)
        {
            if (!EnsureOverlays())
            {
                return;
            }

            var settings = Sweep;

            var passes = settings.LoopWhileInteracting ? int.MaxValue : settings.Passes;
            var passDuration = GetPassDuration(settings);
            var axis = BuildSweepAxis(settings);
            var lastProgress = HiddenProgress;

            ShowOverlays();

            for (var pass = 0; pass < passes && !_finishRequested; pass++)
            {
                var backwards = settings.PingPong && pass % 2 == 1;
                var elapsed = 0f;

                while (elapsed < passDuration && !_finishRequested)
                {
                    if (token.IsCancellationRequested)
                    {
                        HideOverlays();
                        return;
                    }

                    if (objectView == null || !objectView.IsPaused)
                    {
                        elapsed += Time.deltaTime;
                    }

                    var normalized = settings.EvaluatePass(Mathf.Clamp01(elapsed / passDuration));
                    lastProgress = backwards ? 1f - normalized : normalized;

                    Apply(settings, axis, lastProgress, 0f);

                    await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                }

                if (_finishRequested || settings.PassGap <= 0f)
                {
                    continue;
                }

                lastProgress = HiddenProgress;
                Apply(settings, axis, lastProgress, 0f);

                if (!await WaitScaled(settings.PassGap, token))
                {
                    HideOverlays();
                    return;
                }
            }

            await PlayShutterFlash(settings, axis, lastProgress, token);

            HideOverlays();
        }

        private async UniTask<bool> WaitScaled(float duration, CancellationToken token)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                if (objectView == null || !objectView.IsPaused)
                {
                    elapsed += Time.deltaTime;
                }

                if (_finishRequested)
                {
                    return true;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }

            return !token.IsCancellationRequested;
        }

        private async UniTask PlayShutterFlash(ScanSweepParams settings, Vector4 axis, float lastProgress,
            CancellationToken token)
        {
            if (!settings.FlashOnFinish || settings.FlashDuration <= 0f)
            {
                return;
            }

            var progress = settings.CutBandBeforeFlash ? HiddenProgress : lastProgress;
            var duration = settings.FlashDuration;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (objectView == null || !objectView.IsPaused)
                {
                    elapsed += Time.deltaTime;
                }

                var envelope = settings.EvaluateFlash(elapsed);
                var flashAmount = settings.FlashIntensity * envelope;
                Apply(settings, axis, progress, flashAmount);

                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }
        }

        private float GetPassDuration(ScanSweepParams settings)
        {
            var total = !settings.LoopWhileInteracting && settings.SyncWithInteractionTime && objectView != null
                ? objectView.InteractionTime / settings.Passes
                : settings.PassDuration;

            if (objectView != null && objectView.InteractionSpeed > 0.01f)
            {
                total /= objectView.InteractionSpeed;
            }

            return Mathf.Max(0.05f, total);
        }

        private Vector4 BuildSweepAxis(ScanSweepParams settings)
        {
            var radians = settings.Angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            var hasBounds = false;
            var combined = new Bounds();

            foreach (var source in _sources)
            {
                if (source == null || source.sprite == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = source.bounds;
                    hasBounds = true;
                    continue;
                }

                combined.Encapsulate(source.bounds);
            }

            if (!hasBounds)
            {
                return new Vector4(direction.x, direction.y, 0f, 1f);
            }

            var center = Vector2.Dot(combined.center, direction);
            var extent = Mathf.Abs(combined.extents.x * direction.x) + Mathf.Abs(combined.extents.y * direction.y);
            var padding = extent * 2f * settings.BandWidth;
            var min = center - extent - padding;
            var length = Mathf.Max(0.001f, (extent + padding) * 2f);

            return new Vector4(direction.x, direction.y, min, length);
        }

        private bool EnsureOverlays()
        {
            CollectSources();

            if (_sources.Count == 0 || sweepMaterial == null)
            {
                return false;
            }

            while (_overlays.Count < _sources.Count)
            {
                _overlays.Add(null);
            }

            for (var index = 0; index < _sources.Count; index++)
            {
                var source = _sources[index];
                var overlay = _overlays[index];

                if (overlay == null)
                {
                    overlay = CreateOverlay(source);
                    _overlays[index] = overlay;
                }

                overlay.sprite = source.sprite;
                overlay.flipX = source.flipX;
                overlay.flipY = source.flipY;
                overlay.drawMode = source.drawMode;
                overlay.size = source.size;
                overlay.sortingLayerID = source.sortingLayerID;
                overlay.sortingOrder = source.sortingOrder + Sweep.SortingOrderOffset;
                overlay.maskInteraction = source.maskInteraction;
            }

            return true;
        }

        private SpriteRenderer CreateOverlay(SpriteRenderer source)
        {
            var holder = new GameObject(source.name + OverlaySuffix);
            holder.transform.SetParent(source.transform, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            if (!Application.isPlaying)
            {
                holder.hideFlags = HideFlags.DontSave;
            }

            var overlay = holder.AddComponent<SpriteRenderer>();
            overlay.sharedMaterial = sweepMaterial;
            overlay.color = Color.white;
            holder.SetActive(false);

            return overlay;
        }

        private void CollectSources()
        {
            _sources.RemoveAll(s => s == null);

            if (_sources.Count > 0)
            {
                return;
            }

            if (targetRenderers != null && targetRenderers.Length > 0)
            {
                foreach (var target in targetRenderers)
                {
                    if (target != null && target.sprite != null)
                    {
                        _sources.Add(target);
                    }
                }

                return;
            }

            SpriteRenderer biggest = null;
            var biggestArea = 0f;

            foreach (var candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate == null || candidate.sprite == null || !candidate.enabled)
                {
                    continue;
                }

                if (candidate.name.EndsWith(OverlaySuffix))
                {
                    continue;
                }

                var size = candidate.bounds.size;
                var area = size.x * size.y;

                if (area <= biggestArea)
                {
                    continue;
                }

                biggest = candidate;
                biggestArea = area;
            }

            if (biggest != null)
            {
                _sources.Add(biggest);
            }
        }

        private void Apply(ScanSweepParams settings, Vector4 axis, float progress, float flashAmount)
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            foreach (var overlay in _overlays)
            {
                if (overlay == null)
                {
                    continue;
                }

                overlay.GetPropertyBlock(_propertyBlock);

                _propertyBlock.SetFloat(ProgressId, progress);
                _propertyBlock.SetVector(SweepAxisId, axis);
                _propertyBlock.SetColor(BandColorId, settings.Color);
                _propertyBlock.SetFloat(IntensityId, settings.Intensity);
                _propertyBlock.SetFloat(BandWidthId, settings.BandWidth);
                _propertyBlock.SetFloat(SoftnessId, settings.Softness);
                _propertyBlock.SetColor(FlashColorId, settings.FlashColor);
                _propertyBlock.SetFloat(FlashAmountId, flashAmount);

                overlay.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ShowOverlays()
        {
            foreach (var overlay in _overlays)
            {
                if (overlay != null)
                {
                    overlay.gameObject.SetActive(true);
                }
            }
        }

        private void HideOverlays()
        {
            foreach (var overlay in _overlays)
            {
                if (overlay != null)
                {
                    overlay.gameObject.SetActive(false);
                }
            }
        }

#if UNITY_EDITOR
        public void EditorPreviewStep(float progress, float flashAmount)
        {
            if (!EnsureOverlays())
            {
                return;
            }

            var settings = Sweep;
            var axis = BuildSweepAxis(settings);
            ShowOverlays();
            Apply(settings, axis, progress, flashAmount);
        }

        public void EditorStopPreview()
        {
            HideOverlays();
            for (var i = _overlays.Count - 1; i >= 0; i--)
            {
                if (_overlays[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_overlays[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(_overlays[i].gameObject);
                    }
                }
            }
            _overlays.Clear();
            _sources.Clear();
        }
#endif
    }
}
