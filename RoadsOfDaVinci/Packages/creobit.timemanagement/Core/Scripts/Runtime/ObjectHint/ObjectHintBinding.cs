using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectHint
{
    public class ObjectHintBinding : MonoBehaviour, IAlternativeSelectionPolicy
    {
        [SerializeField]
        [Tooltip("Окно подсказки — обычный TutorialViewSo, как у эпик-хинтов. Показывается тем же " +
                 "ShowHint. Пусто — подсказка не показывается.")]
        private TutorialViewSo hintView;

        [Header("Наведение")]
        [SerializeField]
        [Tooltip("Показывать подсказку, пока курсор на объекте (на мобиле — пока держат палец).")]
        private bool showOnHover;

        [Header("Клик, когда действие невозможно")]
        [SerializeField]
        [Tooltip("Показывать подсказку по клику, если до объекта нет пути или не хватает ресурсов. " +
                 "Когда действие выполнимо, подсказка не появляется.")]
        private bool showOnBlockedClick;

        [SerializeField]
        [Tooltip("Показывать подсказку по клику на неинтерактивный объект (CanInteract = false).")]
        private bool showOnNonInteractableClick;

        [SerializeField]
        [Tooltip("Через сколько секунд подсказка, открытая кликом, закроется сама. 0 — не закрывать по времени.")]
        [Min(0f)]
        private float blockedClickDuration = 4f;

        [SerializeField]
        [Tooltip("Закрывать подсказку, открытую кликом, следующим кликом по экрану.")]
        private bool blockedClickCloseOnNextClick = true;

        [Header("Панель выбора альтернативы")]
        [SerializeField]
        [Tooltip("Показывать подсказку, когда открылась панель выбора юнита.")]
        private bool showOnSelectionPanel = true;

        [SerializeField]
        [Tooltip("На любом открытии панели или только когда у объекта осталась одна доступная " +
                 "альтернатива (для раскопок это состояние после дрона).")]
        private ObjectHintSelectionMode selectionMode = ObjectHintSelectionMode.OnlyWithSingleAlternative;

        [SerializeField]
        [Tooltip("Открывать панель выбора, даже когда доступна всего одна альтернатива. " +
                 "Выключено — единственная альтернатива запускается сразу, как и на остальных объектах.")]
        private bool openPanelWithSingleAlternative = true;

        public TutorialViewSo HintView => hintView;

        public bool ShowOnHover => showOnHover;

        public bool ShowOnBlockedClick => showOnBlockedClick;

        public bool ShowOnNonInteractableClick => showOnNonInteractableClick;

        public float BlockedClickDuration => blockedClickDuration;

        public bool BlockedClickCloseOnNextClick => blockedClickCloseOnNextClick;

        public bool ShowOnSelectionPanel => showOnSelectionPanel;

        public ObjectHintSelectionMode SelectionMode => selectionMode;

        public bool ForceSelectionPanel => openPanelWithSingleAlternative;

        public ObjectHintData BuildData(Component target)
        {
            switch (target)
            {
                case ComplexObject complexObject:
                    var stageView = GetStageView(complexObject);

                    return BuildData(stageView, GetCocSettings(complexObject, stageView));

                case ObjectView.ObjectView objectView:
                    return BuildData(objectView,
                        objectView.ObjectDataSO != null ? objectView.ObjectDataSO.TooltipSettings : null);

                default:
                    return default;
            }
        }

        private static ObjectHintData BuildData(ObjectView.ObjectView objectView, TooltipSettings tooltipSettings)
        {
            var tooltipData = tooltipSettings?.TooltipData;

            var icon = objectView is StaticObjectView staticObjectView
                       && staticObjectView.tooltipPreviewIcon != null
                ? staticObjectView.tooltipPreviewIcon
                : tooltipData != null
                    ? tooltipData.TooltipObjectIcon
                    : null;

            return new ObjectHintData(icon,
                tooltipData != null ? tooltipData.TooltipObjectName : null,
                tooltipData != null ? tooltipData.TooltipObjectNameSuffix : null);
        }

        private static StaticObjectView GetStageView(ComplexObject complexObject)
        {
            if (complexObject.transitionStateData != null
                && complexObject.currentStateIndex >= 0
                && complexObject.currentStateIndex < complexObject.transitionStateData.Length
                && complexObject.transitionStateData[complexObject.currentStateIndex].TransitionFrom != null)
            {
                return complexObject.transitionStateData[complexObject.currentStateIndex].TransitionFrom;
            }

            return complexObject.currentObjectView;
        }

        private static TooltipSettings GetCocSettings(ComplexObject complexObject, StaticObjectView stageView)
        {
            if (stageView == null || stageView.ObjectDataSO == null)
            {
                return complexObject.TooltipSettings;
            }

            return stageView.ObjectDataSO.ShowTooltip
                ? stageView.ObjectDataSO.TooltipSettings
                : complexObject.TooltipSettings;
        }
    }
}
