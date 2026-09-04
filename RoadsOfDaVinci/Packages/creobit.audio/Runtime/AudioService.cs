using System.Threading;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Creobit.Audio
{
    public class AudioService : IAudioService
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private AudioMixer _audioMixer;

        private Tween _currentMusicTween;
        private AudioSource _musicAudioSource;
        private AudioSource _sfxAudioSource;
        
        public void Init(AudioMixer audioMixer, AudioSource musicAudioSource, AudioSource sfxAudioSource)
        {
            _audioMixer = audioMixer;
            _musicAudioSource = musicAudioSource;
            _sfxAudioSource = sfxAudioSource;
            
            SetupMusicSource(musicAudioSource);
            SetupSfxSource(sfxAudioSource);
        }

        public void PlaySfx(AudioClip resourceToPlay)
        {
            if (resourceToPlay == null)
            {
                return;
            }

            _sfxAudioSource.PlayOneShot(resourceToPlay);
        }

        public void PlayRandomSfx(AudioClipCollection resourceToPlay)
        {
            if (resourceToPlay is null)
            {
                return;
            }

            _sfxAudioSource.PlayOneShot(resourceToPlay.AudioClips[Random.Range(0, resourceToPlay.AudioClips.Length)]);
        }

        public void PlayMusic(AudioClip resourceToPlay)
        {
            if (_musicAudioSource.isPlaying)
            {
                _currentMusicTween?.Kill();

                _currentMusicTween = _musicAudioSource.DOFade(0f, 1.5f)
                    .SetEase(Ease.OutSine)
                    .OnComplete(() =>
                    {
                        _musicAudioSource.clip = resourceToPlay;
                        _musicAudioSource.Play();
                        _currentMusicTween = _musicAudioSource.DOFade(1f, 1.5f)
                            .SetEase(Ease.InSine)
                            .OnComplete(() => _currentMusicTween = null);
                    });
            }
            else
            {
                _musicAudioSource.clip = resourceToPlay;
                _musicAudioSource.volume = 0f;
                _musicAudioSource.Play();
                _currentMusicTween = _musicAudioSource.DOFade(1f, 1.5f)
                    .SetEase(Ease.InSine)
                    .OnComplete(() => _currentMusicTween = null);
            }
        }

        public void PlayRandomMusic(AudioClipCollection resourceToPlay)
        {
            if (resourceToPlay is null)
            {
                return;
            }

            PlayMusic(resourceToPlay.AudioClips[Random.Range(0, resourceToPlay.AudioClips.Length)]);
        }

        public void SetMasterVolume(float volume)
        {
            var dBVolume = LinearToDecibel(volume);
            _audioMixer.SetFloat(AudioRuntimeConstants.AudioMixerExposedParameters.MasterVolume, dBVolume);
        }

        public void SetMusicVolume(float volume)
        {
            var dBVolume = LinearToDecibel(volume);
            _audioMixer.SetFloat(AudioRuntimeConstants.AudioMixerExposedParameters.MusicVolume, dBVolume);
        }

        public void SetSfxVolume(float volume)
        {
            var dBVolume = LinearToDecibel(volume);
            _audioMixer.SetFloat(AudioRuntimeConstants.AudioMixerExposedParameters.SFXVolume, dBVolume);
        }

        public void PauseMusic()
        {
            _musicAudioSource.Pause();
        }

        public void ResumeMusic()
        {
            _musicAudioSource.UnPause();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _currentMusicTween?.Kill();
            Object.Destroy(_musicAudioSource);
            Object.Destroy(_sfxAudioSource);
        }

        private void SetupMusicSource(AudioSource audioSource)
        {
            _musicAudioSource = audioSource;
            Object.DontDestroyOnLoad(_musicAudioSource);
            _musicAudioSource.Play();
        }

        private void SetupSfxSource(AudioSource audioSource)
        {
            _sfxAudioSource = audioSource;
            Object.DontDestroyOnLoad(_sfxAudioSource);
        }

        /// <summary>
        ///     Converts a linear volume value (0 to 1) to a decibel value.
        /// </summary>
        /// <param name="linearVolume">The volume in a linear scale.</param>
        /// <returns>The corresponding decibel value.</returns>
        private float LinearToDecibel(float linearVolume)
        {
            // Ensure the linear volume is within the valid range
            if (linearVolume <= 0)
            {
                return -80f; // Minimum decibel value for silence
            }

            return 20f * Mathf.Log10(linearVolume);
        }

        /// <summary>
        ///     Converts a decibel value to a linear volume value (0 to 1).
        /// </summary>
        /// <param name="decibel">The volume in decibels.</param>
        /// <returns>The corresponding linear volume value.</returns>
        private float DecibelToLinear(float decibel)
        {
            // Decibels to linear calculation
            return Mathf.Pow(10.0f, decibel / 20.0f);
        }
    }
}