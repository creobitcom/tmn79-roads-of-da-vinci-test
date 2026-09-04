using System.Collections.Generic;
using Creobit.UI;
using Creobit.UI.Utility;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    [CreateAssetMenu(menuName = "Create TutorialViewSo", fileName = "Tutorial/TutorialViewSo")]
    public class TutorialViewSo : ScriptableObject
    {
        public PanelReference panelReference;
        public bool isSequence;
        public List<int> actionsToOpen = new();
        public List<int> actionsToClose = new();
        public bool IsUI;
        public int id;

        [Header("Поведение окна")]
        [Tooltip("Остановить таймер уровня, пока окно открыто. Юниты и производство продолжают работать.")]
        public bool pauseLevelTimer;

        [Tooltip("Показывать окно даже если игрок отключил туториалы в настройках.")]
        public bool unskipTutorial;

        [Tooltip("Стадии окна, помеченные «не пропускать». Список собирается из префаба автоматически " +
                 "при его сохранении, руками править не нужно.")]
        public List<int> unskipStages = new();

        [Tooltip("Рисовать окно поверх интерфейса: панели ресурсов, задач и прочего HUD.")]
        public bool showAboveHud;

        [Tooltip("Запретить клики по HUD, пока окно открыто. " +
                 "Объекты на уровне остаются доступны — те, что разрешены подсветкой.")]
        public bool blockHudInput;

        public bool HasUnskipStages => unskipStages != null && unskipStages.Count > 0;

        public bool IsStageUnskippable(int stage) =>
            stage > 0 && unskipStages != null && unskipStages.Contains(stage);
    }
}
