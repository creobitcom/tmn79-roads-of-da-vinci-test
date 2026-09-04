using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Creobit.Audio
{
    public interface IAudioService : IDisposable, IAudioShared
    {
        /// <summary>
        /// Initializes the audio service with the provided audio mixer, music audio source, and SFX audio source.
        /// Must be initialized before use
        /// </summary>
        /// <param name="audioMixer">The audio mixer to be used by the service.</param>
        /// <param name="musicAudioSource">The audio source that will handle music playback.</param>
        /// <param name="sfxAudioSource">The audio source that will handle sound effect playback.</param>
        public void Init(AudioMixer audioMixer, AudioSource musicAudioSource, AudioSource sfxAudioSource);
    }
}