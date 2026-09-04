using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Audio;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production.Repair
{
    public class BreakableController
    {
        private readonly IAudioService _audioController;
        private readonly ObjectSpecialTagController _objectSpecialTagController;
        private readonly ILevelLoader _levelLoader;

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private LevelBaseSO _levelData;
        private bool _sharedTimerRunning;

        // Все грейды всех зданий уровня, у которых в принципе разрешена поломка (CanBeBroken +
        // нашлась ремонтная мастерская) — включая пока неактивные (не проапгрейженные) грейды.
        // Фактическая пригодность (активен ли грейд СЕЙЧАС на сцене, не сломан ли уже) проверяется
        // заново в момент срабатывания общего таймера (см. TryBreakRandomBuilding) — это надёжнее,
        // чем держать отдельный реактивный пул, который приходится вручную поддерживать в актуальном
        // состоянии.
        private readonly List<StaticObjectView> _allCandidates = new();

        public BreakableController(
            IAudioService audioController,
            ObjectSpecialTagController objectSpecialTagController,
            ILevelLoader levelLoader)
        {
            _audioController = audioController;
            _objectSpecialTagController = objectSpecialTagController;
            _levelLoader = levelLoader;
        }

        /// <summary>
        /// Активирует поломку для объекта, если это разрешено дизайнером (productionData.CanBeBroken)
        /// И на уровне нашлась ремонтная мастерская. Раньше сам факт наличия мастерской на уровне
        /// принудительно включал CanBeBroken на объекте, игнорируя реальную настройку в инспекторе —
        /// теперь CanBeBroken не трогается, а результат активации возвращается вызывающему коду.
        /// Вызывается для КАЖДОГО грейда каждого здания при старте уровня, в том числе для ещё
        /// неактивных — все попадают в общий список кандидатов, а реальная активность на сцене
        /// проверяется заново при каждом выборе (см. TryBreakRandomBuilding).
        /// </summary>
        public bool Initialize(StaticObjectView objectView)
        {
            if (!objectView.productionData.CanBeBroken || objectView.productionData.BreakableView == null)
            {
                return false;
            }

            if (TryFindRepairStation(objectView, out var repairTag) == false)
            {
                Log.Gameplay.Warning($"[Breakable] {objectView.name}: CanBeBroken включён, но на уровне не нашёлся объект с тегом '{RuntimeConstants.SpecialObjects.RepairTag}' — поломка не активирована.");
                return false;
            }

            objectView._currentBreakableView = objectView.productionData.BreakableView;

            if (!objectView.productionData.IsValidRepairTag(repairTag))
            {
                Log.Gameplay.Error("Not activated breakable object: " + objectView._currentBreakableView.transform.parent.name);
                return false;
            }

            if (objectView._currentBreakableView.transform.parent.TryGetComponent(out Collider parentCollider) == false)
            {
                Log.Gameplay.Error("Not found collider for breakable object: " + objectView._currentBreakableView.transform.parent.name);
                return false;
            }

            objectView.breakObjectCollider = parentCollider;

            var coc = objectView._currentBreakableView.GetComponentInParent<ComplexObject>();

            if (coc != null)
            {
                objectView.breakCocCollider = coc.GetComponent<Collider>();
            }

            // Клик по активному грейду физически ловит его СОБСТВЕННЫЙ коллайдер (гарантирован
            // ObjectView), а не обязательно тот же, что breakCocCollider/breakObjectCollider —
            // без его отключения клик проваливается в обычное взаимодействие/апгрейд здания.
            objectView.breakOwnCollider = objectView.GetComponent<Collider>();

            // BreakableView — полноценный интерактивный объект: игрок кликает по нему как по обычной
            // задаче (CanInteract/InteractionTime/UnitTypeCount настраиваются на его собственном
            // StaticObjectDataSO), юнит идёт и работает. Чиним объект-владелец не по клику, а по факту
            // завершения этой работы — OnEndInteract сработает, когда юнит реально доделает починку.
            if (objectView._currentBreakableView is StaticObjectView breakableStaticView)
            {
                breakableStaticView.overlayOwner = objectView;
                breakableStaticView.OnEndInteract += _ => objectView.RepairProduction();
            }

            _levelData ??= _levelLoader.LevelBaseSO.CurrentValue;

            _allCandidates.Add(objectView);

            EnsureSharedTimerRunning();

            return true;
        }

        private void EnsureSharedTimerRunning()
        {
            if (_sharedTimerRunning || _levelData == null)
            {
                return;
            }

            if (!_levelData.EnableRepair)
            {
                Log.Gameplay.Info("[Breakable] EnableRepair выключен на уровне — общий таймер поломки не запущен.");
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
                    var interval = UnityEngine.Random.Range(_levelData.BreakableBaseMinInterval, _levelData.BreakableBaseMaxInterval);

                    Log.Gameplay.Info($"[Breakable] Общий таймер поломки запущен, сработает через {interval:F1} сек (уровневый интервал {_levelData.BreakableBaseMinInterval}-{_levelData.BreakableBaseMaxInterval}).");

                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: _cancellationTokenSource.Token);

                    TryBreakRandomBuilding();
                }
            }
            catch (OperationCanceledException)
            {
                // Уровень выгружен/Dispose() — штатное завершение общего таймера.
            }
        }

        private void TryBreakRandomBuilding()
        {
            var candidates = _allCandidates.Where(ov =>
                ov != null
                && ov.gameObject.activeInHierarchy
                && !ov.IsBroke
                && !ov.NeedStopBrake).ToList();

            if (candidates.Count == 0)
            {
                Log.Gameplay.Info("[Breakable] Нет доступных зданий для поломки в этом цикле.");
                return;
            }

            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            // NeedBreak — существующий флаг, который уже опрашивается ProductionController'ом
            // (SubscribeBreakable/EveryUpdate) и учитывает состояние производства (заспавнен ли
            // продукт и т.п.) перед тем, как реально вызвать Break().
            chosen.NeedBreak = true;
        }

        public void Repair(StaticObjectView objectView)
        {
            // Идемпотентность: если OnEndInteract на BreakableView срабатывает больше одного раза
            // за одну задачу, повторный вызов — no-op.
            if (!objectView.IsBroke)
            {
                return;
            }

            objectView._currentBreakableView.gameObject.SetActive(false);

            if (objectView.breakOwnCollider != null)
            {
                objectView.breakOwnCollider.enabled = true;
            }

            if (objectView.breakCocCollider != null)
            {
                objectView.breakCocCollider.enabled = true;
            }

            if (objectView.breakObjectCollider != null)
            {
                objectView.breakObjectCollider.enabled = true;
            }

            objectView.IsRepaired = true;
            objectView.NeedBreak = false;

            objectView.StartProduction();
            objectView.productionData.OnRepaired?.Invoke();
        }

        public void Break(StaticObjectView objectView)
        {
            if (objectView._currentBreakableView == null || objectView._currentBreakableView.gameObject == null)
            {
                Log.Gameplay.Warning($"[Breakable] {objectView.name}: _currentBreakableView пуст — Break() не может показать сломанный вид.");
                objectView.NeedBreak = false;
                return;
            }

            Log.Gameplay.Info($"[Breakable] {objectView.name}: сломалось.");

            objectView._currentBreakableView.gameObject.SetActive(true);

            objectView.NeedBreak = false;

            if (objectView.breakOwnCollider != null)
            {
                objectView.breakOwnCollider.enabled = false;
            }

            if (objectView.breakCocCollider != null)
            {
                objectView.breakCocCollider.enabled = false;
            }

            if (objectView.breakObjectCollider != null)
            {
                objectView.breakObjectCollider.enabled = false;
            }

            if (objectView.productionData.BreakSounds != null && objectView.productionData.BreakSounds.AudioClips.Length > 0)
            {
                _audioController.PlayRandomSfx(objectView.productionData.BreakSounds);
            }

            objectView.productionData.OnBroke?.Invoke();
        }

        private bool TryFindRepairStation(StaticObjectView objectView, out GameplayTagSO repairTag)
        {
            repairTag = null;

            if (_objectSpecialTagController.IsExistsObjectTag(RuntimeConstants.SpecialObjects.RepairTag, out var foundObjects))
            {
                objectView._repairStationView = (ObjectView)foundObjects.Last();
                repairTag = objectView._repairStationView.ObjectDataSO.ObjectTypeTags.FirstOrDefault(
                        x => x.name == RuntimeConstants.SpecialObjects.RepairTag);

                return repairTag != null;
            }

            return false;
        }

        public void Dispose()
        {
            _allCandidates.Clear();

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
