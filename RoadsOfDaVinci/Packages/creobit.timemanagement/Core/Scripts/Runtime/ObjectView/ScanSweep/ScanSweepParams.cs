using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.ScanSweep
{
    [Serializable]
    public class ScanSweepParams
    {
        [Header("Цвет и прозрачность")]
        [SerializeField, FormerlySerializedAs("bandColor")]
        [Tooltip("Цвет и прозрачность (альфа) полосы сканирования.")]
        private Color color = new Color(0.42f, 0.92f, 1f, 0.85f);

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Множитель яркости/интенсивности.")]
        private float intensity = 1f;

        [Header("Полоса")]
        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Ширина полосы в долях размера объекта.")]
        private float bandWidth = 0.25f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Мягкость краёв полосы. 0 — резкая граница, 1 — плавное затухание.")]
        private float softness = 0.8f;

        [SerializeField]
        [Tooltip("Угол движения в градусах. 0 — слева направо, 90 — снизу вверх.")]
        private float angle = 45f;

        [Header("Анимация")]
        [SerializeField]
        [Tooltip("Повторять сканирование непрерывно, пока юнит взаимодействует с объектом.")]
        private bool loopWhileInteracting = true;

        [SerializeField, Min(0.05f)]
        [Tooltip("Длительность одного прохода (секунды).")]
        private float passDuration = 0.8f;

        [SerializeField, Min(0f)]
        [Tooltip("Пауза между проходами (секунды).")]
        private float passGap = 0.1f;

        [SerializeField, Min(1)]
        [Tooltip("Количество проходов (если выключен Loop While Interacting).")]
        private int passes = 1;

        [SerializeField]
        [Tooltip("Растянуть проходы на полное время взаимодействия.")]
        private bool syncWithInteractionTime;

        [SerializeField]
        [Tooltip("Двигать каждый следующий проход в обратном направлении.")]
        private bool pingPong;

        [SerializeField]
        [Tooltip("Кривая скорости движения полосы.")]
        private AnimationCurve passCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Финал — вспышка")]
        [SerializeField]
        [Tooltip("Вспышка объекта в конце сканирования.")]
        private bool flashOnFinish = true;

        [SerializeField]
        [Tooltip("Гасить полосу перед вспышкой.")]
        private bool cutBandBeforeFlash = true;

        [SerializeField]
        [Tooltip("Цвет вспышки.")]
        private Color flashColor = Color.white;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Яркость вспышки на пике.")]
        private float flashIntensity = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Длительность нарастания вспышки (сек).")]
        private float flashAttack = 0.03f;

        [SerializeField, Min(0f)]
        [Tooltip("Длительность удержания пика (сек).")]
        private float flashHold = 0.05f;

        [SerializeField, Min(0f)]
        [Tooltip("Длительность затухания (сек).")]
        private float flashDecay = 0.22f;

        [Header("Сортировка")]
        [SerializeField]
        [Tooltip("Смещение Order in Layer относительно объекта.")]
        private int sortingOrderOffset = 1;

        public Color Color => color;

        public Color BandColor => color;

        public float Intensity => intensity;

        public float BandWidth => bandWidth;

        public float Softness => softness;

        public float Angle => angle;

        public bool LoopWhileInteracting => loopWhileInteracting;

        public float PassDuration => Mathf.Max(0.05f, passDuration);

        public float PassGap => Mathf.Max(0f, passGap);

        public int Passes => Mathf.Max(1, passes);

        public bool SyncWithInteractionTime => syncWithInteractionTime;

        public bool PingPong => pingPong;

        public bool FlashOnFinish => flashOnFinish;

        public bool CutBandBeforeFlash => cutBandBeforeFlash;

        public Color FlashColor => flashColor;

        public float FlashIntensity => flashIntensity;

        public float FlashAttack => Mathf.Max(0f, flashAttack);

        public float FlashHold => Mathf.Max(0f, flashHold);

        public float FlashDecay => Mathf.Max(0f, flashDecay);

        public float FlashDuration => FlashAttack + FlashHold + FlashDecay;

        public int SortingOrderOffset => sortingOrderOffset;

        public float EvaluatePass(float normalizedTime)
        {
            if (passCurve == null || passCurve.length == 0)
            {
                return normalizedTime;
            }

            return passCurve.Evaluate(normalizedTime);
        }

        public float EvaluateFlash(float elapsed)
        {
            if (elapsed < FlashAttack)
            {
                return FlashAttack <= 0.0001f ? 1f : elapsed / FlashAttack;
            }

            if (elapsed < FlashAttack + FlashHold)
            {
                return 1f;
            }

            if (FlashDecay <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - (elapsed - FlashAttack - FlashHold) / FlashDecay);
        }
    }
}
