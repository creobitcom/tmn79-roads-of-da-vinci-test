using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils
{
    [CreateAssetMenu(fileName = "Gameplay Settings", menuName = "8floor/TimeManager/Gameplay/Settings")]
    public class GameplaySettings : ScriptableObject
    {
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectTaskSettings)]
        [field: SerializeField] 
        public List<TaskBadgeWrapper> BadgeSprites { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectTaskSettings)]
        [field: ToggleLeft]
        [field: SerializeField, Space(5f)] 
        public bool CanCancelTaskDuringInteraction { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectTaskSettings)]
        [field: ToggleLeft]
        [field: SerializeField, Space(5f)] 
        public bool CanCancelTaskDuringRunning { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.MovableObjectTaskSettings)]
        [field: SerializeField, Range(0f, 3f), Space(5f)]
        public float DelayAfterTask { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)] 
        public AssetReference TooltipPrefab { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)] 
        public AssetReference NotEnoughResourceTooltipPrefab { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)] 
        public AssetReference NoPathTooltipPrefab { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)] 
        public AssetReference CantReachAnotherObjectTooltipPrefab { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)] 
        public AssetReference TooltipResourcePrefab { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)]
        public AssetReference ExchangeTooltipPrefab { get; private set; }

        /// <summary>
        /// Новые (LA8) шаблоны тултипов. Перечисленные здесь префабы предзагружаются при старте
        /// уровня — показ тултипа синхронный и дождаться загрузки на месте не может.
        /// Объект выбирает шаблон в ObjectDataSO → TooltipSettings → Template;
        /// шаблон, которого нет в этом списке, не покажется.
        /// Список пуст — всё работает по-старому.
        /// </summary>
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)]
        public List<Tooltip.Data.TooltipTemplate> TooltipTemplates { get; private set; } = new();

        /// <summary>
        /// Шаблон карточки «не хватает ресурсов»: красная шапка + ячейки недостающего.
        /// Пусто — работает старый префаб NotEnoughResourceTooltipPrefab, поведение прежнее.
        ///
        /// Префаб должен быть свой, не общий с шаблонами объектов и не общий с
        /// <see cref="MessageTooltipTemplate"/>: на один префаб создаётся ОДИН инстанс, а
        /// подсказка и тултип объекта могут понадобиться порознь.
        /// </summary>
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)]
        public Tooltip.Data.TooltipTemplate NotEnoughResourcesTooltipTemplate { get; private set; }

        /// <summary>
        /// Шаблон карточки текстовых подсказок: «нет пути» и «не дотянуться». Ресурсов в них нет,
        /// поэтому карточка прячет блок входа и рисуется одной шапкой.
        ///
        /// Отдельный шаблон от «не хватает ресурсов» ровно по одной причине: у карточки с ячейками
        /// нижний отступ больше верхнего (под блок ресурсов), и на карточке без ячеек это читается
        /// как пустота под шапкой. Разные отступы одним префабом не задать.
        ///
        /// Пусто — работают старые префабы (NoPathTooltipPrefab и соседний).
        /// </summary>
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)]
        public Tooltip.Data.TooltipTemplate MessageTooltipTemplate { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.TooltipSettings)]
        [field: SerializeField, Space(5f)]
        [field: Tooltip("Сколько секунд висит служебная карточка. У старых префабов это поле Duration на вьюхе.")]
        public float SecondaryTooltipDuration { get; private set; } = 2f;

        [field: SerializeField]
        [field: Tooltip("Off — easy mode gives no stars at all: none in the win view, none saved to the profile. The next level still unlocks.")]
        public bool EasyModeGivesMaxStars { get; private set; } = true;

        [field: FoldoutGroup("Tutorial Settings")]
        [field: SerializeField]
        [field: Tooltip("On - the tutorial button never touches the running level: turning tutorials off hides them "
                        + "and releases every input block at once, turning them back on applies from the next level "
                        + "or after a restart. Hints (ShowHint) stay available while tutorials are off and the button "
                        + "is not hidden on levels without tutorials. Off - legacy behaviour: turning tutorials back "
                        + "on reloads the level.")]
        public bool TutorialToggleAppliesNextLevel { get; private set; }

        [field: SerializeField]
        public bool EnableIdleHighlight { get; private set; } = false;

        [field: FoldoutGroup("Highlight Settings")]
        [field: SerializeField]
        public float IdleTimeToHighlight { get; private set; } = 15f;

        [field: FoldoutGroup("Highlight Settings")]
        [field: SerializeField]
        public Color HighlightColor { get; private set; } = new Color(1f, 0.95f, 0.5f, 0f);
        
        [field: FoldoutGroup("Highlight Settings")]
        [field: SerializeField] public float HighlightFadeDuration { get; private set; } = 0.6f;

        [field: FoldoutGroup("Highlight Settings")]
        [field: SerializeField] public int HighlightCyclesCount { get; private set; } = 3;

        [field: FoldoutGroup("Highlight Settings")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float HighlightMaxAlpha { get; private set; } = 0.85f;
    }
}