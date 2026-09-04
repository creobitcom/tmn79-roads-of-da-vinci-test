using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Stages
{
    /// <summary>
    /// Трёхпозиционный флаг стадии. Ноль — «как в окне», поэтому у уже существующих окон
    /// ничего не меняется: незаполненные настройки означают поведение из настроек окна.
    /// </summary>
    public enum TutorialStageFlag
    {
        [LabelText("как в окне")]
        AsWindow = 0,

        [LabelText("включить")]
        On = 1,

        [LabelText("выключить")]
        Off = 2,
    }

    public enum TutorialStageTransition
    {
        [LabelText("как в окне")]
        AsWindow = 0,

        [LabelText("без анимации")]
        Instant = 1,

        [LabelText("своя анимация")]
        Custom = 2,
    }

    /// <summary>
    /// Переопределения поведения окна на конкретной стадии.
    /// Всё, что оставлено «как в окне», берётся из настроек туториала (SO).
    /// </summary>
    [Serializable]
    public class TutorialStageSettings
    {
        [LabelText("Стадия")]
        [MinValue(0)]
        [SerializeField]
        private int stage = 1;

        [LabelText("Пауза таймера")]
        [EnumToggleButtons]
        [SerializeField]
        private TutorialStageFlag pauseLevelTimer = TutorialStageFlag.AsWindow;

        [LabelText("Поверх HUD")]
        [EnumToggleButtons]
        [SerializeField]
        private TutorialStageFlag showAboveHud = TutorialStageFlag.AsWindow;

        [LabelText("Блокировать HUD")]
        [EnumToggleButtons]
        [SerializeField]
        private TutorialStageFlag blockHudInput = TutorialStageFlag.AsWindow;

        [LabelText("Не пропускать стадию")]
        [Tooltip("«Включить» — стадия покажется, даже если игрок выключил туториалы. " +
                 "Окно открывают вызовом ShowTutorialAtStage с номером этой стадии.")]
        [EnumToggleButtons]
        [SerializeField]
        private TutorialStageFlag unskipTutorial = TutorialStageFlag.AsWindow;

        [LabelText("Пропускать клики сквозь окно")]
        [SerializeField]
        private bool passClicksThroughWindow;

        [Title("Переход на эту стадию")]
        [LabelText("Анимация")]
        [EnumToggleButtons]
        [SerializeField]
        private TutorialStageTransition transition = TutorialStageTransition.AsWindow;

        [LabelText("Шаги")]
        [ShowIf(nameof(UsesCustomAnimation))]
        [SerializeField]
        private TutorialAnimationSet transitionAnimation = new();

        [Title("Условия входа")]
        [LabelText("Ключи входа")]
        [Tooltip("Стадия включится, когда AddStageOpenKey наберёт этот набор. Пусто — ключи не нужны.")]
        [ListDrawerSettings(DraggableItems = false, ShowFoldout = false)]
        [SerializeField]
        private List<int> keysToEnter = new();

        [LabelText("Входить только со стадии")]
        [MinValue(0)]
        [Tooltip("0 — не проверять, с какой стадии входим.")]
        [SerializeField]
        private int enterFromStage;

        [Title("Условия закрытия")]
        [LabelText("Ключи закрытия")]
        [Tooltip("HideWindow и закрытие тутора на этой стадии ждут этот набор через AddStageCloseKey. Пусто — не нужны.")]
        [ListDrawerSettings(DraggableItems = false, ShowFoldout = false)]
        [SerializeField]
        private List<int> keysToClose = new();

        public int Stage => stage;

        public TutorialStageFlag PauseLevelTimer => pauseLevelTimer;

        public TutorialStageFlag ShowAboveHud => showAboveHud;

        public TutorialStageFlag BlockHudInput => blockHudInput;

        public TutorialStageFlag UnskipTutorial => unskipTutorial;

        public bool PassClicksThroughWindow => passClicksThroughWindow;

        public TutorialStageTransition Transition => transition;

        public TutorialAnimationSet TransitionAnimation => transitionAnimation;

        public IReadOnlyList<int> KeysToEnter => keysToEnter;

        public int EnterFromStage => enterFromStage;

        public bool HasEnterKeys => keysToEnter != null && keysToEnter.Count > 0;

        public IReadOnlyList<int> KeysToClose => keysToClose;

        public bool HasCloseKeys => keysToClose != null && keysToClose.Count > 0;

        private bool UsesCustomAnimation => transition == TutorialStageTransition.Custom;

        public static bool Resolve(TutorialStageFlag flag, bool windowValue) => flag switch
        {
            TutorialStageFlag.On => true,
            TutorialStageFlag.Off => false,
            _ => windowValue,
        };

#if UNITY_EDITOR
        public void EditorSetStage(int value) => stage = value;
#endif
    }
}
