using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    [Serializable]
    public class TutorialStageEvent
    {
        [LabelText("Стадия")]
        [MinValue(1)]
        public int stage = 1;

        [LabelText("При закрытии стадии")]
        public UltEvent onStageHidden;

#if UNITY_EDITOR
        public string EditorLabel => $"Стадия {stage}: при закрытии";
#endif
    }

    [Serializable]
    public class TutorialViewLevelData
    {
        [field: SerializeField]
        public TutorialViewSo TutorialViewData { get; private set; }

        [field: SerializeField]
        [RequireInterface(typeof(IReactToActions))]
#if UNITY_EDITOR
        [field: ListDrawerSettings(ListElementLabelName = nameof(TutorialHighlightObject.EditorLabel))]
        [field: InfoBox("$" + nameof(StagesSummary), InfoMessageType.None)]
#endif
        public List<TutorialHighlightObject> HighlightObjects { get; private set; } = new();

        [field: SerializeField]
        [field: LabelText("События стадий")]
#if UNITY_EDITOR
        [field: ListDrawerSettings(ListElementLabelName = nameof(TutorialStageEvent.EditorLabel))]
#endif
        public List<TutorialStageEvent> StageEvents { get; private set; } = new();

#if UNITY_EDITOR
        /// <summary>
        /// Сводка по окну прямо в мосте уровня: сколько у окна стадий и на каких из них
        /// уже разрешён клик. Иначе номера стадий приходится вбивать вслепую.
        /// </summary>
        private string StagesSummary
        {
            get
            {
                var maxStage = GetWindowStageCount();

                if (maxStage < 0)
                {
                    return "Окно не выбрано или у него не найден префаб.";
                }

                if (maxStage == 0)
                {
                    return "У этого окна нет стадий — подсветки работают по галочке «Подсветить сразу».";
                }

                var byStage = new List<string>();

                for (var stage = 1; stage <= maxStage; stage++)
                {
                    var names = new List<string>();

                    foreach (var highlight in HighlightObjects)
                    {
                        if (highlight.stage == stage && highlight.reactToActions != null)
                        {
                            names.Add(highlight.reactToActions.name);
                        }
                    }

                    byStage.Add(names.Count == 0
                        ? $"  Стадия {stage}: нажимать нельзя"
                        : $"  Стадия {stage}: {string.Join(", ", names)}");
                }

                return $"У окна {maxStage} стадий.\n" + string.Join("\n", byStage);
            }
        }

        /// <summary>
        /// Максимальный номер стадии в префабе окна. -1 — окно или префаб не найдены.
        /// </summary>
        private int GetWindowStageCount()
        {
            if (TutorialViewData == null
                || TutorialViewData.panelReference == null
                || TutorialViewData.panelReference.UIPanelReference == null)
            {
                return -1;
            }

            var guid = TutorialViewData.panelReference.UIPanelReference.AssetGUID;

            if (string.IsNullOrEmpty(guid))
            {
                return -1;
            }

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                return -1;
            }

            var maxStage = 0;

            foreach (var element in prefab
                         .GetComponentsInChildren<Stages.TutorialStageElement>(true))
            {
                maxStage = Mathf.Max(maxStage, element.Stage);
            }

            return maxStage;
        }
#endif
    }
}
