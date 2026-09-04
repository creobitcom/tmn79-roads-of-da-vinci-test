using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using Creobit.Audio;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.Diseasable
{
    public class DiseasableController : IDisposable
    {
        private readonly IAudioService _audioController;
        private readonly ILevelLoader _levelLoader;
        private readonly StaticObjectController _staticObjectController;
        private readonly UnitAvailabilityController _unitAvailabilityController;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        // Общий на весь уровень таймер интервалов (start_condition уровня) — не мутируется
        // по объектам, поэтому безопасно живёт на контроллере.
        private LevelBaseSO _levelData;
        private bool _sharedTimerRunning;

        // Все грейды всех зданий уровня, у которых в принципе разрешена болезнь (CanBeDiseased
        // + есть DiseasableView/юниты) — включая пока неактивные (не проапгрейженные) грейды.
        // Фактическая пригодность (активен ли грейд СЕЙЧАС на сцене, есть ли ещё место под
        // больных, не пуста ли база) проверяется заново в момент срабатывания общего таймера
        // (см. TryDiseaseRandomBuilding) — это надёжнее, чем держать отдельный реактивный пул,
        // который приходится вручную поддерживать в актуальном состоянии.
        private readonly List<StaticObjectView> _allCandidates = new();

        public DiseasableController(
            IAudioService audioController,
            ILevelLoader levelLoader,
            UnitAvailabilityController unitAvailabilityController,
            StaticObjectController staticObjectController)
        {
            _audioController = audioController;
            _levelLoader = levelLoader;
            _unitAvailabilityController = unitAvailabilityController;
            _staticObjectController = staticObjectController;
        }

        public void Load()
        {
            _staticObjectController.AddedStaticObject += InitializeObjectView;
        }

        /// <summary>
        /// Активирует болезнь для объекта, если это разрешено дизайнером (BaseData.CanBeDiseased).
        /// Больница на присутствие/постройку не проверяется — юниты могут заболеть и до того, как
        /// больница построена; HealTag нужен только чтобы найти лекаря/больницу для самого лечения,
        /// а не как предусловие для активации болезни.
        /// Вызывается для КАЖДОГО грейда каждого здания при старте уровня, в том числе для ещё
        /// неактивных (не проапгрейженных) — все попадают в общий список кандидатов, а реальная
        /// активность на сцене проверяется заново при каждом выборе (см. TryDiseaseRandomBuilding).
        /// </summary>
        private async void InitializeObjectView(StaticObjectView objectView)
        {
            if (!objectView.BaseData.CanBeDiseased || objectView.BaseData.DiseasableView == null)
            {
                return;
            }

            objectView.diseaseActivated = true;
            _levelData ??= _levelLoader.LevelBaseSO.CurrentValue;

            InitializeReferences(objectView);

            if (objectView.diseaseUnitsView == null)
            {
                Log.Gameplay.Error($"[Diseasable] {objectView.name}: не найден BaseUnitsStateView рядом с DiseasableView — болезнь не активирована.");
                objectView.diseaseActivated = false;
                return;
            }

            await WaitForUnitIcons(objectView);

            _allCandidates.Add(objectView);

            EnsureSharedTimerRunning();
        }

        private void InitializeReferences(StaticObjectView objectView)
        {
            var currentBaseTransform = objectView.BaseData.DiseasableView.transform.parent;

            var coc = currentBaseTransform.GetComponentInParent<ComplexObject>();
            if (coc == null)
            {
                Log.Gameplay.Error($"Not found COC for diseasable base: {currentBaseTransform.name}");
                return;
            }

            objectView.diseaseCocCollider = coc.GetComponent<Collider>();

            // Клик по активному грейду ловит его СОБСТВЕННЫЙ коллайдер (гарантирован ObjectView),
            // не обязательно тот же, что diseaseCocCollider — без его отключения клик по больному
            // зданию проваливался бы в обычное взаимодействие вместо лечения.
            objectView.diseaseOwnCollider = objectView.GetComponent<Collider>();

            // includeInactive: true — CanBeDiseased/DiseasableView настраиваются per-грейд (Grade1/2/3),
            // а неактивные на старте грейды (COC ещё не проапгрейжен) без этого не найдут
            // BaseUnitsStateView и diseaseUnitsView молча останется null (WaitForUnitIcons упадёт).
            objectView.diseaseUnitsView = currentBaseTransform.GetComponentInChildren<BaseUnitsStateView>(true);

            // DiseasableView — полноценный интерактивный объект: игрок кликает по нему как по обычной
            // задаче (CanInteract/InteractionTime/UnitTypeCount настраиваются на его собственном
            // StaticObjectDataSO — как правило, это лекарь), юнит идёт и лечит. Лечим владельца не по
            // клику, а по факту завершения этой работы — OnEndInteract сработает, когда юнит доделает.
            objectView.BaseData.DiseasableView.overlayOwner = objectView;
            objectView.BaseData.DiseasableView.OnEndInteract += _ => objectView.HealBase();
        }

        private async UniTask WaitForUnitIcons(StaticObjectView objectView)
        {
            await UniTask.WaitUntil(() => objectView.diseaseUnitsView.UnitIcons.Count > 0);
        }

        private void EnsureSharedTimerRunning()
        {
            if (_sharedTimerRunning || _levelData == null)
            {
                return;
            }

            if (!_levelData.EnableDisease)
            {
                Log.Gameplay.Info("[Diseasable] EnableDisease выключен на уровне — общий таймер болезни не запущен.");
                return;
            }

            _sharedTimerRunning = true;
            RunSharedTimerLoop().Forget();
        }

        private async UniTaskVoid RunSharedTimerLoop()
        {
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var interval = UnityEngine.Random.Range(_levelData.DiseasableBaseMinInterval, _levelData.DiseasableBaseMaxInterval);

                    Log.Gameplay.Info($"[Diseasable] Общий таймер болезни запущен, сработает через {interval:F1} сек (уровневый интервал {_levelData.DiseasableBaseMinInterval}-{_levelData.DiseasableBaseMaxInterval}).");

                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: _cancellationTokenSource.Token);

                    TryDiseaseRandomBuilding();
                }
            }
            catch (OperationCanceledException)
            {
                // Уровень выгружен/Dispose() — штатное завершение общего таймера.
            }
        }

        private void TryDiseaseRandomBuilding()
        {
            var candidates = _allCandidates.Where(ov =>
                ov != null
                && ov.gameObject.activeInHierarchy
                && ov.currentDiseasedUnits < ov.BaseData.MaxDiseasedUnits
                && !ov.diseaseUnitsView.IsBaseEmpty()).ToList();

            if (candidates.Count == 0)
            {
                Log.Gameplay.Info("[Diseasable] Нет доступных зданий для болезни в этом цикле.");
                return;
            }

            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            SetDisease(chosen);
        }

        public void Heal(StaticObjectView objectView)
        {
            // currentDiseasedUnits == 0 — идемпотентность: если OnEndInteract на DiseasableView
            // срабатывает больше одного раза за одну задачу лекаря, второй вызов должен быть no-op.
            if (!objectView.diseaseActivated || objectView.BaseData.DiseasableView == null || objectView.currentDiseasedUnits == 0)
            {
                return;
            }

            objectView.BaseData.DiseasableView.gameObject.SetActive(false);
            if (objectView.diseaseOwnCollider != null)
            {
                objectView.diseaseOwnCollider.enabled = true;
            }
            if (objectView.diseaseCocCollider != null)
            {
                objectView.diseaseCocCollider.enabled = true;
            }

            ResetDiseasedUnits(objectView);

            if (objectView.BaseData.HealSounds != null && objectView.BaseData.HealSounds.AudioClips.Length > 0)
            {
                _audioController.PlayRandomSfx(objectView.BaseData.HealSounds);
            }

            objectView.BaseData.OnHealed?.Invoke();
        }

        private void ResetDiseasedUnits(StaticObjectView objectView)
        {
            var unitsToHeal = objectView.diseaseUnitsView.UnitIcons.Take(objectView.currentDiseasedUnits);
            foreach (var unit in unitsToHeal)
            {
                unit.color = Color.white;
            }

            _unitAvailabilityController.RemoveRestriction(objectView, objectView.currentDiseasedUnits);
            objectView.currentDiseasedUnits = 0;
        }

        private void SetDisease(StaticObjectView objectView)
        {
            var diseasableView = objectView.BaseData.DiseasableView;
            if (diseasableView == null || diseasableView.gameObject == null)
            {
                return;
            }

            Log.Gameplay.Info($"[Diseasable] {objectView.name}: заболело ({objectView.currentDiseasedUnits + 1}/{objectView.BaseData.MaxDiseasedUnits}).");

            diseasableView.gameObject.SetActive(true);
            if (objectView.diseaseOwnCollider != null)
            {
                objectView.diseaseOwnCollider.enabled = false;
            }
            if (objectView.diseaseCocCollider != null)
            {
                objectView.diseaseCocCollider.enabled = false;
            }

            objectView.currentDiseasedUnits++;
            ApplyDiseaseToUnits(objectView);

            objectView.BaseData.OnDiseased?.Invoke();
        }

        private void ApplyDiseaseToUnits(StaticObjectView objectView)
        {
            var unitsToDisease = objectView.diseaseUnitsView.UnitIcons.Take(objectView.currentDiseasedUnits);
            foreach (var unit in unitsToDisease)
            {
                unit.color = objectView.BaseData.DiseaseIconColor;
            }

            _unitAvailabilityController.AddRestriction(objectView, 1);
        }

        public void Dispose()
        {
            _staticObjectController.AddedStaticObject -= InitializeObjectView;

            _allCandidates.Clear();

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
