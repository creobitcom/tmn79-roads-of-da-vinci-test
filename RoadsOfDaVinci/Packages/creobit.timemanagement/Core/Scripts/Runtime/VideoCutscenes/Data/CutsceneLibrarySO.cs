using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data
{
    /// <summary>
    /// Единый список всех видео-катсцен проекта. Собирается кнопкой из папки,
    /// вручную вести список не нужно. На сцену кладётся ссылка только на библиотеку.
    /// </summary>
    [CreateAssetMenu(fileName = "CutsceneLibrary", menuName = "8floor/TimeManager/Cutscene Library")]
    public class CutsceneLibrarySO : ScriptableObject
    {
        [BoxGroup("Экран")]
        [LabelText("Префаб экрана")]
        [InfoBox("Генерируется через Tools/Cutscenes/Video Cutscene Prefab.", InfoMessageType.None)]
        [SerializeField]
        private GameObject _viewPrefab;

        [BoxGroup("Звук")]
        [LabelText("Группа микшера")]
        [InfoBox("Без неё звук ролика идёт мимо микшера и не слушается настроек громкости игры. " +
                 "Обычно сюда кладётся SFXVolume из BaseAudioMixer.", InfoMessageType.None)]
        [SerializeField]
        private AudioMixerGroup _audioMixerGroup;

        [BoxGroup("Вид субтитров")]
        [HideLabel]
        [InfoBox("Общий для всех катсцен. Удобнее настраивать в Tools → Cutscenes → Тайминг субтитров: " +
                 "там видно результат прямо на кадре.", InfoMessageType.None)]
        [SerializeField]
        private SubtitleStyle _subtitleStyle = new();

        [BoxGroup("Сборка")]
        [LabelText("Папка с катсценами")]
        [FolderPath]
        [SerializeField]
        private string _cutscenesFolder = "Assets/LA8_slv/Cutscenes";

        [BoxGroup("Сборка")]
        [LabelText("Катсцены")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [SerializeField]
        private List<VideoCutsceneSO> _cutscenes = new();

        public GameObject ViewPrefab => _viewPrefab;
        public SubtitleStyle SubtitleStyle => _subtitleStyle;
        public AudioMixerGroup AudioMixerGroup => _audioMixerGroup;
        public IReadOnlyList<VideoCutsceneSO> Cutscenes => _cutscenes;

        public VideoCutsceneSO Find(string id)
        {
            foreach (var cutscene in _cutscenes)
            {
                if (cutscene != null && cutscene.Id == id)
                {
                    return cutscene;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        [BoxGroup("Сборка")]
        [Button("Пересобрать список", ButtonSizes.Large)]
        private void Rebuild()
        {
            if (string.IsNullOrEmpty(_cutscenesFolder) || !UnityEditor.AssetDatabase.IsValidFolder(_cutscenesFolder))
            {
                Debug.LogError($"[{name}] Папка не найдена: {_cutscenesFolder}", this);
                return;
            }

            var found = new List<VideoCutsceneSO>();
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(VideoCutsceneSO)}",
                new[] { _cutscenesFolder });

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoCutsceneSO>(path);

                if (asset != null)
                {
                    found.Add(asset);
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(
                UnityEditor.AssetDatabase.GetAssetPath(a),
                UnityEditor.AssetDatabase.GetAssetPath(b)));

            _cutscenes = found;

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);

            Debug.Log($"[{name}] Собрано катсцен: {_cutscenes.Count}", this);
        }
#endif
    }
}
