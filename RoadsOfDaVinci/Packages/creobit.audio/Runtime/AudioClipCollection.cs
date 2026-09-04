using UnityEngine;

namespace Creobit.Audio
{
    [CreateAssetMenu(fileName = nameof(AudioClipCollection), menuName = "Creobit/Bootstrap/Audio Clip Collection")]
    public class AudioClipCollection : ScriptableObject
    {
        public AudioClip[] AudioClips;
    }
}