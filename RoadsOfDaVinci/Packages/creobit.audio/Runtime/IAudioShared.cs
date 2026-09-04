using UnityEngine;

namespace Creobit.Audio
{
    public interface IAudioShared
    {
        /// Plays a sound effect using the provided audio resource.
        /// <param name="resourceToPlay">
        /// The audio clip to be played as a sound effect.
        /// </param>
        public void PlaySfx(AudioClip resourceToPlay);

        /// Plays a random sound effect from the provided audio clip collection.
        /// <param name="resourceToPlay">
        /// The collection of audio clips from which a random sound effect will be selected and played.
        /// </param>
        public void PlayRandomSfx(AudioClipCollection resourceToPlay);

        /// <summary>
        /// Plays the specified music audio clip.
        /// </summary>
        /// <param name="resourceToPlay">The audio clip to play as background music.</param>
        public void PlayMusic(AudioClip resourceToPlay);

        /// <summary>
        /// Plays a random AudioClip from the provided AudioClipCollection as background music.
        /// </summary>
        /// <param name="resourceToPlay">The AudioClipCollection containing the AudioClips to choose from.</param>
        public void PlayRandomMusic(AudioClipCollection resourceToPlay);

        /// <summary>
        /// Adjusts the "master" volume of the audio system.
        /// </summary>
        /// <param name="volume">The desired "master" volume level, ranging from 0.0 (muted) to 1.0 (full volume).</param>
        public void SetMasterVolume(float volume);

        /// Sets the music volume level.
        /// <param name="volume">The volume level for the music, represented as a float value. Typically, it should range from 0.0 (muted) to 1.0 (maximum volume).</param>
        public void SetMusicVolume(float volume);

        /// <summary>
        /// Sets the volume level for sound effects (SFX).
        /// </summary>
        /// <param name="volume">The desired volume level for SFX, typically represented as a normalized value between 0.0 (mute) and 1.0 (maximum).</param>
        public void SetSfxVolume(float volume);

        /// <summary>
        /// Pauses the currently playing music track.
        /// </summary>
        /// <remarks>
        /// This method temporarily halts the playback of the music currently being played.
        /// The playback can be resumed later using the corresponding ResumeMusic method.
        /// </remarks>
        public void PauseMusic();

        /// Resumes the playback of currently paused music.
        /// This method allows the music to continue playing from the point where it
        /// was previously paused. No effect will occur if no music is currently paused.
        /// Typically, it is invoked after a previous call to PauseMusic to restart playback.
        public void ResumeMusic();
    }
}