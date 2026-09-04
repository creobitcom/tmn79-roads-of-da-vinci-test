using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    [CreateAssetMenu(menuName = "Creobit/Bootstrap/SFX Sprites Data")]
    public class SfxSpritesData : ScriptableObject
    {
        [field: SerializeField] 
        public Sprite EnabledMusicSprite { get; private set; }
        [field: SerializeField] 
        public Sprite MutedMusicSprite { get; private set; }
        [field: SerializeField] 
        public Sprite EnabledSoundSprite { get; private set; }
        [field: SerializeField]
        public Sprite MutedSoundSprite { get; private set; }
    }
}