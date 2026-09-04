using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Audio;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters 
{

    public class BoosterView : MonoBehaviour {

        private const float ChargedOnStartRemainingDurationSeconds = 0.1f;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent OnBoosterInitialized { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent OnBoosterReloaded { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent OnBoosterUsed { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent WhileBoosterActive { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent OnBoosterEnded { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent WhileBoosterCharging { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField] public UltEvent OnBoosterCharged { get; private set; }

        [SerializeField] private Image _discahrgedImage;
        [SerializeField] private Image _activeImage;
        [SerializeField] private Image _chargedImage;
        [SerializeField] private Image _extraImage;
        [SerializeField] private GameObject _extraObject;
        
        private IBoostersController _boostersController;
        private IAudioService _audioController;
        private ISaveController _saveController;
        
        public float Charge { get; set; }

        public BoosterDataSO BoosterData { get; private set; }

        public float UsageDuration => BoosterData.UsageDurationInSeconds;
        public float ChargeDuration => BoosterData.ChargeDurationInSeconds;

        public bool CanBeUsed { get; set; }
        public bool IsActive { get; set; }
        public bool ChargedOnStart { get; private set; }

        [Inject]
        public void Construct(IBoostersController boostersController,
            IAudioService audioController,
            ISaveController saveController)
        {
            _boostersController = boostersController;
            _audioController = audioController;
            _saveController = saveController;
        }

        public void Initialize(BoosterDataSO boosterData, bool chargedOnStart = false)
        {
            BoosterData = boosterData;
            ChargedOnStart = chargedOnStart;

            if (_saveController.CurrentSaveData.ProfileData.GameMode == GameMode.Easy)
            {
                if (boosterData.TimerSpeed is { Mode: ArithmeticModes.Multiplication, Value: 0f })
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                gameObject.SetActive(true);
            }

            ResetCharge();

            _discahrgedImage.sprite = boosterData.DischargedBoosterSprite;
            _activeImage.sprite = boosterData.ActiveBoosterSprite;
            _chargedImage.sprite = boosterData.ChargedBoosterSprite;

            if (_extraImage != null && boosterData.ExtraBoosterSprite != null)
            {
                _extraImage.sprite = boosterData.ExtraBoosterSprite;
            }

            if (_extraObject != null)
            {
                _extraObject.SetActive(boosterData.ExtraBoosterSprite != null);
            }
            
            OnBoosterInitialized?.Invoke();
        }

        public void ResetCharge()
        {
            CanBeUsed = false;
            Charge = ChargedOnStart && ChargeDuration > 0f
                ? Mathf.Clamp01(1f - ChargedOnStartRemainingDurationSeconds / ChargeDuration)
                : 0f;

            WhileBoosterCharging.InvokeSafe();
            _boostersController.ChargeBooster(this).Forget();
        }
        
        public void UseBooster() 
        {
            _boostersController.UseBooster(this);
        }

        public void PlayAudio(AudioClip clip)
        {
            _audioController.PlaySfx(clip);
        }

    }
}
