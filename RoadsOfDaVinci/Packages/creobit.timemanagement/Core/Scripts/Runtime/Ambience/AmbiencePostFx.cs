using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Ambience
{
    public sealed class AmbiencePostFx
    {
        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;
        private Vignette _vignette;
        private UniversalAdditionalCameraData _cameraData;
        private bool _cameraStateSaved;
        private bool _originalPostProcessing;

        public void Apply(
            Transform owner,
            bool bloomEnabled,
            float intensity,
            float threshold,
            float scatter,
            float vignetteIntensity,
            float vignetteSmoothness)
        {
            var vignetteEnabled = vignetteIntensity > 0.001f;
            if (!bloomEnabled && !vignetteEnabled)
            {
                Clear();
                return;
            }

            if (_volume == null)
            {
                var volumeObject = new GameObject("AmbiencePostFxVolume");
                if (!Application.isPlaying)
                {
                    volumeObject.hideFlags = HideFlags.HideAndDontSave;
                }

                volumeObject.transform.SetParent(owner, false);
                _volume = volumeObject.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 100f;
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.hideFlags = HideFlags.HideAndDontSave;
                _bloom = _profile.Add<Bloom>();
                _vignette = _profile.Add<Vignette>();
                _volume.sharedProfile = _profile;
            }

            _bloom.active = bloomEnabled;
            if (bloomEnabled)
            {
                _bloom.intensity.Override(intensity);
                _bloom.threshold.Override(threshold);
                _bloom.scatter.Override(scatter);
            }

            _vignette.active = vignetteEnabled;
            if (vignetteEnabled)
            {
                _vignette.intensity.Override(vignetteIntensity);
                _vignette.smoothness.Override(Mathf.Max(vignetteSmoothness, 0.01f));
                _vignette.color.Override(Color.black);
            }

            EnsureCameraPostProcessing();
        }

        public void Clear()
        {
            if (_cameraStateSaved && _cameraData != null)
            {
                _cameraData.renderPostProcessing = _originalPostProcessing;
            }

            _cameraStateSaved = false;
            _cameraData = null;

            if (_volume != null)
            {
                SafeDestroy(_volume.gameObject);
                _volume = null;
            }

            if (_profile != null)
            {
                SafeDestroy(_profile);
                _profile = null;
            }

            _bloom = null;
            _vignette = null;
        }

        private void EnsureCameraPostProcessing()
        {
            if (_cameraData == null)
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                if (!camera.TryGetComponent(out _cameraData))
                {
                    return;
                }

                _originalPostProcessing = _cameraData.renderPostProcessing;
                _cameraStateSaved = true;
            }

            if (!_cameraData.renderPostProcessing)
            {
                _cameraData.renderPostProcessing = true;
            }

            if (_volume != null)
            {
                var mask = _cameraData.volumeLayerMask.value;
                if (mask != 0 && (mask & (1 << _volume.gameObject.layer)) == 0)
                {
                    for (var layer = 0; layer < 32; layer++)
                    {
                        if ((mask & (1 << layer)) != 0)
                        {
                            _volume.gameObject.layer = layer;
                            break;
                        }
                    }
                }
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
    }
}
