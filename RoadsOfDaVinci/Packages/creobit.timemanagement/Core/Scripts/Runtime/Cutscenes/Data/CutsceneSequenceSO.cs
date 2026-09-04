using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data
{
    [CreateAssetMenu(fileName = "CutsceneSequence", menuName = "8floor/TimeManager/Cutscenes/Cutscene Sequence")]
    public class CutsceneSequenceSO : ScriptableObject, ITimeManagerSO
    {
        [InfoBox(
            "Если задан Comic Prefab — кадры (Frames) ниже НЕ используются: спрайты и тайминги берутся из префаба.",
            InfoMessageType.Warning, "@ComicPrefab != null")]
        [Tooltip("Префаб комикс-сцены (с компонентом ComicScene). Если задан — спавнится он, а Frames игнорируются.")]
        public GameObject ComicPrefab;

        [HideIf("@ComicPrefab != null")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<CutsceneFrame> Frames = new();
    }
}