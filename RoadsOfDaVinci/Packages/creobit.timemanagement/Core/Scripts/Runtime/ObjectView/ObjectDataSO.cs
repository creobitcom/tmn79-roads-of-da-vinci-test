using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    [InlineEditor]
    public abstract class ObjectDataSO : ScriptableObject, ITimeManagerSO, ISelfValidator
    {
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Tags)]
        [field: SerializeField]
        public GameplayTagSO[] ObjectTypeTags { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Tags)]
        [field: SerializeField]
        public GameplayTagSO[] TaskTypeTags { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ResourcesStealer)]
        [field: Tooltip("Determines if the object can be stealed by movable object")]
        [field: SerializeField]
        public bool IsStealable { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ResourcesStealer)]
        [field: Tooltip("Mode for compare tags")]
        [field: ShowIf(nameof(IsStealable))]
        [field: SerializeField]
        public GameplayTagsContainsMode StealerTagsContainsMode { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.ResourcesStealer)]
        [field: Tooltip("What tags does a stealer need to have to steal an object")]
        [field: ShowIf(nameof(IsStealable))]
        [field: SerializeField]
        public GameplayTagSO[] StealerObjectTags { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Actions)]
        [field: ToggleLeft]
        [field: Tooltip("Determines if the object can react to primary action")]
        [field: SerializeField]
        public bool CanReactToPrimaryAction { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Actions)]
        [field: ToggleLeft]
        [field: Tooltip("Determines if the object can react to secondary action")]
        [field: SerializeField]
        public bool CanReactToSecondaryAction { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Actions)]
        [field: Tooltip("Determines if the object is walkable")]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool BlocksPath { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UniqueBadges)]
        [field: SerializeField]
        public UniqueBadgesData UniqueBadgesData { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Highlight)]
        [field: SerializeField] public bool IgnoreIdleHighlight { get; private set; }
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Highlight)]
        [field: SerializeField] public bool OverrideIgnoreIdleHighlight { get; private set; }
        
        #region AudioClips

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.AudioSettings)]
        [field: SerializeField]
        public AudioClip TaskRegisterSound { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.AudioSettings)]
        [field: SerializeField]
        public AudioClip Interacting { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.AudioSettings)]
        [field: ShowIf("@Interacting != null")]
        [field: MinValue(0.01f)]
        [field: SerializeField]
        public float InteractingFrequencyMultiplier { get; set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.AudioSettings)]
        [field: SerializeField]
        public AudioClip InteractionEnd { get; private set; }

        #endregion

        #region InteractionInfo

        [field: ToggleLeft]
        [field: Tooltip("Determines if the object can be interacted")]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: SerializeField]
        public bool CanInteract { get; private set; }

        [field: ShowIf(nameof(CanInteract))]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("Determines the amount of possible interactions with object")]
        [field: SerializeField]
        public ushort InteractionsAmount { get; private set; } = 1;

        [field: ShowIf(nameof(CanInteract))]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("Determines if the object gives resources without requirement of going home.")]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool GivesResourcesImmediatelyAfterInteraction { get; private set; }

        [field: ShowIf(nameof(CanInteract))]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("Determines if the object interaction requires a resource cost.")]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool InteractionNeedInputResources { get; private set; }

        [field: ShowIf("@CanInteract && InteractionNeedInputResources")]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("GameResources needed to interact with the object")]
        [field: SerializeField]
        public ResourceAmount[] InputResources { get; set; }

        [field: ShowIf(nameof(CanInteract))]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("Determines if the object will give resource after interaction.")]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool InteractionHaveOutputResources { get; private set; }

        [field: ShowIf("@CanInteract && InteractionHaveOutputResources")]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("GameResources what will be given after interaction.")]
        [field: SerializeField]
        public ResourceAmount[] OutputResources { get; set; }

        [field: ShowIf(nameof(CanInteract))]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: MinValue(0.01f)]
        [field: Tooltip("The amount of time required to interact with the object in seconds")]
        [field: SerializeField]
        public float InteractionTime { get; private set; } = 1f;
        
        [field: ShowIf(nameof(CanInteract))]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("The amount of time required to interact with the object in seconds")]
        [field: SerializeField]
        public bool UseTaskView { get; private set; } = true;

        [field: ShowIf("@CanInteract && InteractionTime > 0.01f")]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: SerializeField]
        public ObjectViewInteractionType InteractionType { get; private set; } = ObjectViewInteractionType.Idle;

        [field: ShowIf(nameof(CanInteract))]
        [field: ToggleLeft]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: Tooltip("Defines if object should be deactivated after final interaction")]
        [field: SerializeField]
        public bool DeactivateAfterFinalInteraction { get; private set; }

        [field: ShowIf(nameof(CanInteract))]
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: SerializeField]
        public float InteractionOffset { get; private set; } = 0.5f;

        //[field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectInteractionInfo)]
        //[field: ToggleLeft]
        //[field: Tooltip("Determines if the object can be interacted with via a moveable object")]
        //[field: OnValueChanged(nameof(OnInteractVieMovableObjectChanged))]
        //[field: SerializeField]
        //public bool InteractViaMovableObject { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: ShowIf(nameof(CanInteract))]
        [field: Tooltip("The number of moveable objects required to interact")]
        [field: SerializeField]
        public List<UnitTypeCount> UnitTypeCount;


        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: ShowIf("@CanInteract")]
        [field: Tooltip("Defines if movable objects used to interact should return to start after interaction")]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool ReturnMovableObjectsToHomePoint { get; private set; } = true;


        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: ShowIf("@CanInteract")]
        [field: Tooltip("Defines if movable objects used to interact should return to start after interaction")]
        [field: ToggleLeft]
        [field: SerializeField]
        public bool MovableObjectHideOnInteract { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InteractionInfo)]
        [field: ShowIf("@CanInteract")]
        [field: SerializeField]
        public List<InteractionAlternative> AlternativeInteractions { get; private set; } = new();

        #endregion

        #region TooltipInfo

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField]
        public bool ShowTooltip { get; set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField]
        [field: ShowIf(nameof(ShowTooltip))]
        [field: InlineProperty]
        [field: HideLabel]
        public TooltipSettings TooltipSettings { get; private set; }

        #endregion

        public void Validate(SelfValidationResult result)
        {
            if (IsStealable && (!CanInteract || !InteractionHaveOutputResources || OutputResources.Length == 0))
            {
                result.AddError($"For stealable objects property {nameof(CanInteract)} should be true or have output resources");
            }
        }

        public void OnEnable()
        {
            //Временный костыль перед заменой на новую систему анимаций
            switch (InteractionType)
            {
                case ObjectViewInteractionType.Axe:
                    InteractingFrequencyMultiplier = 1.2f;
                    break;
                case ObjectViewInteractionType.Hammer:
                    InteractingFrequencyMultiplier = 0.85f;
                    break;
                case ObjectViewInteractionType.Pickaxe:
                    InteractingFrequencyMultiplier = 0.6f;
                    break;
                case ObjectViewInteractionType.Take:
                    InteractingFrequencyMultiplier = 1.65f;
                    break;
            }
        }

        // private void OnInteractVieMovableObjectChanged()
        // {
        //     if (MovableObjectAmountToInteract <= 0)
        //     {
        //         ReturnMovableObjectsToHomePoint = false;
        //     }
        // }
    }
}