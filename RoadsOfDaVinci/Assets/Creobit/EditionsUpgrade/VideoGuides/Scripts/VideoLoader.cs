using System;
using System.Collections.Generic;
using System.IO;
using TaskSys = System.Threading.Tasks.Task;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Unity.VideoHelper;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Video;
using VContainer;

namespace Creobit.EditionsUpgrade
{
    public class VideoLoader : MonoBehaviour
    {
        [SerializeField] private VideoController _controller;

        [SerializeField, Tooltip("StreamingAssets/path")]
        private string _pathFile;

        [SerializeField, Tooltip("Fallback only, the mixer from the running game is preferred")]
        private AudioMixer _audioMixer;

        [SerializeField] private string _mixerGroupName = "SFXVolume";

        [SerializeField] private AudioSource _videoAudioSource;

        public Action OnStartedPlaying;

        public bool IsPlaying => _controller.IsPlaying;

        private VideoPlayer _player;

        private AudioMixer _runtimeMixer;

        private bool _isVideoExists;

        private const string DefaultMixerGroupName = "SFXVolume";

        private readonly List<AudioSource> _mutedSources = new List<AudioSource>();

        private readonly string[] _allowedFormats = new string[]
        {
            ".mp4",
            ".avi",
            ".mov",
            ".mkv"
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_pathFile)) return;

            if (IsVideo(_pathFile) == false)
            {
                Debug.LogError("Video not found at: " + _pathFile);
            }
        }
#endif

        private bool _isAudioMuted;

        [Inject]
        public void Construct(BootstrapPrefabReferences prefabReferences)
        {
            _runtimeMixer = prefabReferences?.AudioMixer;

            ApplyMixerRouting();
        }

        private void OnDestroy()
        {
            _controller.OnStartedPlaying.RemoveListener(VideoStartedHandler);
            _controller.OnFinishedPlaying.RemoveListener(VideoFinishedHandler);
            _controller.OnPaused.RemoveListener(VideoPausedHandler);
            SetExternalAudioSourcesState(false);
        }

        private void Awake()
        {
            _controller.OnStartedPlaying.AddListener(VideoStartedHandler);
            _controller.OnFinishedPlaying.AddListener(VideoFinishedHandler);
            _controller.OnPaused.AddListener(VideoPausedHandler);

            _player = _controller.GetComponent<VideoPlayer>();
        }

        private void OnEnable()
        {
            if (_isVideoExists == false) return;

            _controller.Play();

            Prepare();
        }

        private void OnDisable()
        {
            SetExternalAudioSourcesState(false);
        }

        public void Play() => _controller.Play();
        public void Pause() => _controller.Pause();

        public async TaskSys LoadAsync(string path, Action<bool> isLoaded = null)
        {
            var videoPath = FindFirstVideoFile(path);

            if (videoPath == null)
            {
                isLoaded?.Invoke(false);
                return;
            }

            ApplyMixerRouting();

            try
            {
                var normalizedPath = videoPath.Replace("\\", "/");
                var videoUrl = normalizedPath.StartsWith("/") ? $"file://{normalizedPath}" : $"file:///{normalizedPath}";
                _controller.PrepareForUrl(videoUrl);
            }
            catch
            {
                Debug.LogError("Not loaded video guide");
                isLoaded?.Invoke(false);
                return;
            }

            while (!_controller.IsPrepared)
                await TaskSys.Yield();

            isLoaded?.Invoke(true);

            _isVideoExists = true;

            _controller.Play();
        }

        private async void Prepare()
        {
            await TaskSys.Delay(TimeSpan.FromSeconds(0.2f));

            Play();
        }

        private void VideoStartedHandler()
        {
            ApplyMixerRouting();
            SetExternalAudioSourcesState(true);
            OnStartedPlaying?.Invoke();
        }

        private void VideoFinishedHandler()
        {
            SetExternalAudioSourcesState(false);
        }

        private void VideoPausedHandler()
        {
            SetExternalAudioSourcesState(false);
        }

        private AudioMixer ActiveMixer => _runtimeMixer != null ? _runtimeMixer : _audioMixer;

        private AudioSource ResolveVideoAudioSource()
        {
            if (_videoAudioSource == null && _controller != null)
            {
                _videoAudioSource = _controller.GetComponent<AudioSource>();
            }

            return _videoAudioSource;
        }

        private void ApplyMixerRouting()
        {
            var mixer = ActiveMixer;

            if (mixer == null)
            {
                return;
            }

            var audioSource = ResolveVideoAudioSource();

            if (audioSource == null)
            {
                return;
            }

            var groupName = string.IsNullOrEmpty(_mixerGroupName) ? DefaultMixerGroupName : _mixerGroupName;
            var groups = mixer.FindMatchingGroups(groupName);

            if (groups == null || groups.Length == 0)
            {
                Debug.LogWarning($"[VideoGuides] Mixer group {groupName} not found in {mixer.name}.");
                return;
            }

            audioSource.outputAudioMixerGroup = groups[0];
        }

        private void SetExternalAudioSourcesState(bool mute)
        {
            if (_isAudioMuted == mute) return;
            _isAudioMuted = mute;

            if (mute == false)
            {
                foreach (var audioSource in _mutedSources)
                {
                    if (audioSource != null)
                    {
                        audioSource.mute = false;
                    }
                }

                _mutedSources.Clear();
                return;
            }

            var videoAudioSource = ResolveVideoAudioSource();

            foreach (var audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (audioSource == null || audioSource == videoAudioSource || audioSource.mute)
                {
                    continue;
                }

                audioSource.mute = true;
                _mutedSources.Add(audioSource);
            }
        }

        private string FindFirstVideoFile(string path)
        {
            if (Directory.Exists(path) == false)
            {
                return null;
            }

            foreach (string format in _allowedFormats)
            {
                string[] files = Directory.GetFiles(path, "*" + format);
                if (files.Length > 0)
                {
                    foreach (string file in files)
                    {
                        if (IsVideo(file) == false) continue;

                        return file;
                    }
                }
            }

            return null;
        }

        private bool IsVideo(string path)
        {
            string extension = Path.GetExtension(path).ToLower();

            if (Array.IndexOf(_allowedFormats, extension) == -1)
            {
                throw new InvalidOperationException("File is not video: " + path);
            }

            return true;
        }
    }
}
