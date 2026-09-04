using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.SeedEffect
{
    /// <summary>
    /// Параметры эффекта семечки волшебного дерева GG2 (game-agnostic).
    /// Спрайты вариантов задаются один раз; тулса адресует их по индексу = JSON additionalInfo_2.
    /// Сортировку рендера НЕ трогаем здесь — её задаёт сам префаб визуала (SortingGroup/SpriteRenderer).
    /// </summary>
    [CreateAssetMenu(fileName = "SeedFlightSettings", menuName = "TimeManagement/Seed Flight Settings")]
    public class SeedFlightSettingsSO : ScriptableObject
    {
        [field: Tooltip("Префаб летящего визуала (семечка + частицы). Спавнится при вызове Play. " +
            "Спрайт семечки и ParticleSystem ищутся в его детях.")]
        [field: SerializeField] public GameObject FlightVisualPrefab { get; private set; }

        [field: Tooltip("Спрайты семечек по индексу варианта. Элемент [n] = seed_magic_tree_{n}, " +
            "[0] обычно пустой (варианты с 1). Выбирается напрямую по additionalInfo_2.")]
        [field: SerializeField] public Sprite[] SeedVariants { get; private set; } = Array.Empty<Sprite>();

        [field: Header("Появление")]
        [field: Tooltip("Смещение точки появления от корня дерева (мировые ед.). " +
            "Подними Y, чтобы семечка стартовала из центра кроны.")]
        [field: SerializeField] public Vector3 SpawnOffset { get; private set; } = Vector3.zero;

        [field: Tooltip("Множитель размера семечки. >1 — крупнее, <1 — мельче.")]
        [field: Min(0.01f)]
        [field: SerializeField] public float Scale { get; private set; } = 1f;

        [field: Tooltip("Длительность пружинной анимации появления, сек (оригинал ~0.83). " +
            "Семечка растёт и пружинит 0.1→1.0→1.1→0.95→1.025→1.0. 0 — появиться сразу без анимации.")]
        [field: Min(0f)]
        [field: SerializeField] public float AppearSeconds { get; private set; } = 0.83f;

        [field: Header("Полёт к центру экрана")]
        [field: Tooltip("Скорость полёта, мировые ед/сек (оригинал 900 px/с / 100 PPU).")]
        [field: Min(0.01f)]
        [field: SerializeField] public float Speed { get; private set; } = 9f;

        [field: Tooltip("Высота дуги полёта над линией старт→финиш, мировые ед. (оригинал 200 px / 100 PPU).")]
        [field: Min(0f)]
        [field: SerializeField] public float ArcHeight { get; private set; } = 2f;

        [field: Header("Удержание и удаление")]
        [field: Tooltip("Сколько секунд семечка висит в центре экрана перед остановкой частиц.")]
        [field: Min(0f)]
        [field: SerializeField] public float HoldSeconds { get; private set; } = 2f;

        [field: Tooltip("Задержка после остановки частиц до самоудаления эффекта, сек.")]
        [field: Min(0f)]
        [field: SerializeField] public float ParticleTailSeconds { get; private set; } = 0.5f;

        [field: Header("Звук (опционально)")]
        [field: Tooltip("Звук при появлении семечки. Можно оставить пустым.")]
        [field: SerializeField] public AudioClip Sound { get; private set; }

        /// <summary>Спрайт семечки по индексу варианта; null (с ошибкой), если индекс вне массива.</summary>
        public Sprite GetSeedSprite(int variant)
        {
            if (SeedVariants == null || variant < 0 || variant >= SeedVariants.Length || SeedVariants[variant] == null)
            {
                Debug.LogError($"{nameof(SeedFlightSettingsSO)} '{name}': нет спрайта для варианта {variant}. " +
                    $"Заполни элемент [{variant}] (seed_magic_tree_{variant}) в SeedVariants.", this);
                return null;
            }

            return SeedVariants[variant];
        }
    }
}
