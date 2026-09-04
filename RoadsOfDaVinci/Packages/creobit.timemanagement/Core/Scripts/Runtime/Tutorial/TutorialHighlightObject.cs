using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    [Serializable]
    public struct TutorialHighlightObject
    {
        public int id;
        public GameObject reactToActions;
        public Transform target;
        public bool isHighlightOnStart;

        [Tooltip("If true, the highlight will be follow to the target position when the object is highlighted.")]
        public bool isHighlightMovable;

        // hideArrow инвертирован: false сохраняет старое поведение и показывает стрелку.
        [Tooltip("Не показывать стрелку. Объект всё равно остаётся кликабельным.")]
        public bool hideArrow;

        // Ноль = как раньше: подсветка не привязана к стадиям и живёт по isHighlightOnStart.
        // Это важно для совместимости — у старых уровней поле десериализуется в 0.
        [Tooltip("Стадия окна, на которой активна подсветка. 0 — не привязана к стадиям.")]
        public int stage;

        [Tooltip("Нажал на этот объект — окно само переходит на следующую стадию. " +
                 "Не нужно вешать вызовы моста на сам объект.")]
        public bool nextStageOnClick;

#if UNITY_EDITOR
        /// <summary>
        /// Подпись строки в списке: в свёрнутом виде сразу видно, что на какой стадии,
        /// без раскрытия каждой записи.
        /// </summary>
        public string EditorLabel
        {
            get
            {
                var objectName = reactToActions != null ? reactToActions.name : "объект не задан";
                var stageName = stage > 0 ? $"стадия {stage}" : "без стадии";
                var notes = string.Empty;

                if (hideArrow)
                {
                    notes += ", без стрелки";
                }

                if (nextStageOnClick)
                {
                    notes += ", клик → след. стадия";
                }

                return $"[{stageName}]  {objectName}{notes}";
            }
        }
#endif
    }
}
