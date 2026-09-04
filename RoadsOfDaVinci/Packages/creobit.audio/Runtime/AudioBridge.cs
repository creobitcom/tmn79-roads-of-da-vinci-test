using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Creobit.Audio
{
    public class AudioBridge : MonoBehaviour, IAudioShared
    {
        private IAudioService _audioService;

        [Inject]
        private void Construct(IAudioService audioController)
        {
            _audioService = audioController;
        }

        public void PlaySfx(AudioClip resourceToPlay)
        {
            _audioService.PlaySfx(resourceToPlay);
        }

        public void PlayRandomSfx(AudioClipCollection resourceToPlay)
        {
            _audioService.PlayRandomSfx(resourceToPlay);
        }

        public void PlayMusic(AudioClip resourceToPlay)
        {
            _audioService.PlayMusic(resourceToPlay);
        }

        public void PlayRandomMusic(AudioClipCollection resourceToPlay)
        {
            _audioService.PlayRandomMusic(resourceToPlay);
        }

        public void SetMasterVolume(float volume)
        {
            _audioService.SetMasterVolume(volume);
        }

        public void SetMusicVolume(float volume)
        {
            _audioService.SetMusicVolume(volume);
        }

        public void SetSfxVolume(float volume)
        {
            _audioService.SetSfxVolume(volume);
        }
        public void SetMasterVolumeSlider(Slider slider)
        {
            _audioService.SetMasterVolume(slider.value);
        }

        public void SetMusicVolumeSlider(Slider slider)
        {
            _audioService.SetMusicVolume(slider.value);
        }

        public void SetSfxVolumeSlider(Slider slider)
        {
            _audioService.SetSfxVolume(slider.value);
        }

        public void PauseMusic()
        {
            _audioService.PauseMusic();
        }

        public void ResumeMusic()
        {
            _audioService.ResumeMusic();
        }
    }
}