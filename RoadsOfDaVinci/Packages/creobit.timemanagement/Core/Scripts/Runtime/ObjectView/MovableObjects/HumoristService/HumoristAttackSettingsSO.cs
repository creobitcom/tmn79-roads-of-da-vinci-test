using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.HumoristService
{
    [CreateAssetMenu(fileName = "HumoristAttackSettings", menuName = "TimeManagement/Humorist Attack Settings")]
    public class HumoristAttackSettingsSO : ScriptableObject
    {
        [Serializable]
        public class TargetReaction
        {
            [field: SerializeField] public GameplayTagSO TargetTag { get; private set; }
            [field: SerializeField] public string AnimationState { get; private set; } = "Idle";
        }

        [field: SerializeField] public GameplayTagSO[] TargetTags { get; private set; } = Array.Empty<GameplayTagSO>();
        [field: SerializeField] public GameplayTagsContainsMode TargetTagsMode { get; private set; } = GameplayTagsContainsMode.OnlyOne;
        [field: Min(0f)]
        [field: SerializeField] public float AttackCooldownSeconds { get; private set; } = 5f;
        [field: Min(0f)]
        [field: SerializeField] public float StunDurationSeconds { get; private set; } = 5f;
        [field: Tooltip("Safety fallback: if the Hit clip never raises StunObject/StunAnimFinished, " +
            "the attack is force-finalized after this many seconds so the enemy can never freeze.")]
        [field: Min(0.1f)]
        [field: SerializeField] public float HitTimeoutSeconds { get; private set; } = 3f;
        [field: Tooltip("Neutral state used as the target's baseline temporary animation and as the " +
            "default reaction. Movement (Run/Idle) of the enemy itself is driven by MovableObjectView.")]
        [field: SerializeField] public string IdleAnimationState { get; private set; } = "idle";
        [field: SerializeField] public string HitAnimationState { get; private set; } = "Hit";
        [field: Tooltip("Анимация испуганного бега, когда враг убегает после пожарного (оригинал: RunTerror). " +
            "Должна существовать в Animator-контроллере врага; если состояния нет — оставь пустым (будет обычный бег).")]
        [field: SerializeField] public string RunAwayAnimationState { get; private set; } = "RunTerror";
        [field: SerializeField] public TargetReaction[] TargetReactions { get; private set; } = Array.Empty<TargetReaction>();
        [field: Tooltip("FX над оглушённой целью (звёздочки). Спавнится на цель на время стана. " +
            "В оригинале это дочерний effect_stun на самом юните; здесь — instantiate, чтобы не править префаб юнита. " +
            "Если у рабочего/мага звёздочки уже встроены в анимацию Stun — оставь пустым.")]
        [field: SerializeField] public GameObject StunEffectPrefab { get; private set; }
        [field: SerializeField] public Vector3 StunEffectLocalPosition { get; private set; }
        [field: SerializeField] public AudioClip AttackSound { get; private set; }

        public string GetReactionAnimation(MovableObjectView target)
        {
            if (target == null || target.MovableObjectDataSO == null)
            {
                return IdleAnimationState;
            }

            foreach (var reaction in TargetReactions)
            {
                if (reaction?.TargetTag != null &&
                    target.MovableObjectDataSO.ObjectTypeTags.Contains(
                        GameplayTagsContainsMode.OnlyOne,
                        new[] { reaction.TargetTag }))
                {
                    return reaction.AnimationState;
                }
            }

            return IdleAnimationState;
        }
    }
}
