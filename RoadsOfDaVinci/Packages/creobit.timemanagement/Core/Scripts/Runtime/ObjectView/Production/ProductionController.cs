using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production.Repair;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.Diseasable;
using Creobit.Audio;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production
{
	public class ProductionController
	{
		private readonly IGameplayIntervalsController _gameplayIntervalsController;
		private readonly IAudioService _audioController;
		private readonly ILevelController _levelController;
		private readonly ILevelLoader _levelLoader;
		private readonly StaticObjectController _staticObjectController;
		private readonly ObjectSpecialTagController _objectSpecialTagController;
		private readonly UnitAvailabilityController _unitAvailabilityController;
		
		private CompositeDisposable _disposable;
		private BreakableController _breakableController;
		private DiseasableController _diseasableController;
		private BreakableBridge _breakableBridge;
		private DiseasableBridge _diseasableBridge;

		private CancellationTokenSource _cancellationTokenSource;

		// Контроллер один на все статик-объекты, поэтому состояние производства хранится
		// по объектам: общие флаги глушили производство на всём уровне.
		private readonly HashSet<StaticObjectView> _activeProduction = new();
		private readonly HashSet<StaticObjectView> _disabledProduction = new();
		
		public ProductionController(
			IGameplayIntervalsController gameplayIntervalsController,
			IAudioService audioController,
			ILevelController levelController,
			StaticObjectController staticObjectController,
			ObjectSpecialTagController objectSpecialTagController,
			ILevelLoader levelLoader,
			UnitAvailabilityController unitAvailabilityController)
		{
			_gameplayIntervalsController = gameplayIntervalsController;
			_audioController = audioController;
			_staticObjectController = staticObjectController;
			_levelController = levelController;
			_objectSpecialTagController = objectSpecialTagController;
			_levelLoader = levelLoader;
			_unitAvailabilityController = unitAvailabilityController;
		}

		public void Load()
		{
			_disposable = new CompositeDisposable();
			_cancellationTokenSource = new CancellationTokenSource();

			_activeProduction.Clear();
			_disabledProduction.Clear();
			
			_breakableController = new BreakableController(_audioController, _objectSpecialTagController, _levelLoader);
			_diseasableController = new DiseasableController(_audioController, _levelLoader,
				_unitAvailabilityController, _staticObjectController);

			_diseasableController.Load();

			// Bridge — единственный на уровень адаптер клика (StaticObjectView.OnHeal/OnRepair) к
			// контроллеру, подписывается сразу на ВСЕ здания уровня через AddedStaticObject.
			_breakableBridge = new BreakableBridge(_staticObjectController, _breakableController);
			_diseasableBridge = new DiseasableBridge(_staticObjectController, _diseasableController);

			_breakableBridge.Load();
			_diseasableBridge.Load();

			_levelController.IsLevelStarted
				.Skip(1)
				.Subscribe(SetupAllProduction)
				.AddTo(_disposable);
		}

		public void Dispose()
		{
			if (_cancellationTokenSource != null)
			{
				_cancellationTokenSource.Cancel();
				_cancellationTokenSource.Dispose();
			}

			_disposable?.Dispose();

			_breakableBridge?.Dispose();
			_diseasableBridge?.Dispose();
		}

		public DiseasableController GetDiseasableController() => _diseasableController;

		public BreakableController GetBreakableController() => _breakableController;

		public DiseasableBridge GetDiseasableBridge() => _diseasableBridge;

		public BreakableBridge GetBreakableBridge() => _breakableBridge;

		private void SetupAllProduction(bool isLevelStarted)
		{
			if (!isLevelStarted || _cancellationTokenSource.IsCancellationRequested)
				return;
			
			foreach (var staticObject in _staticObjectController.StaticObjects)
			{
				// CanBeBroken живёт на productionData, а сам productionData в инспекторе скрыт
				// (ShowIf) за CanProduceObjects — но это только видимость поля, не связь по смыслу.
				// Здание может быть ломающимся и НЕ производящим ресурсы (например, уже готовое
				// строение без собственного производства) — Breakable должен активироваться
				// независимо от CanProduceObjects, иначе такие здания никогда не попадут в пул.
				SetupBreakable(staticObject);

				if (staticObject.CanProduceObjects)
					SetupProduction(staticObject);
			}
		}

		private void SetupBreakable(StaticObjectView objectView)
		{
			objectView.breakableActivated = _breakableController?.Initialize(objectView) ?? false;

			if (objectView.breakableActivated)
			{
				SubscribeBreakable(objectView);
			}
		}

		private void SetupProduction(StaticObjectView objectView)
		{
			if (objectView.productionData.StartProductionImmediately)
			{
				StartProduction(objectView).Forget();
			}

			objectView.OnStartProduction += ov => StartProduction(ov);
			objectView.OnStopProduction += StopProduction;
			objectView.OnSetProductionSpeed += SetSpeed;
			objectView.OnDisableProduction += DisableProduction;
		}

		public async UniTaskVoid StartProduction(StaticObjectView objectView)
		{
			if (objectView == null || _cancellationTokenSource.IsCancellationRequested)
				return;

			if (objectView.productionData.UseObjectSpawn)
			{
				if (!objectView.productionData.SpawnPoint)
					return;

				await UniTask.WhenAll(WaitUntilObjectState(objectView.productionData.SpawnPoint.gameObject, 
						true, true),
					WaitUntilObjectState(objectView.productionData.SpawnedSpitObject.gameObject, false));
			}
			else
			{
				await UniTask.WhenAll(WaitUntilObjectState(
						objectView.productionData.SpawnedCollectibleObject.transform.parent.gameObject, true,
						true),
					WaitUntilObjectState(objectView.productionData.SpawnedCollectibleObject.gameObject, false));
			}
			
			// IsCancellationRequested безопасно читать даже после Dispose() (в отличие от .Token,
			// который в этом случае кидает ObjectDisposedException) — старый ProductionController
			// (пересоздаётся заново при каждом Reload/рестарте) мог быть задиспоужен, пока этот
			// вызов ждал предыдущий await; без проверки здесь .Token ниже упал бы необработанным
			// исключением, которое, похоже, сбивает обработку остальных UniTask-задач в этом кадре.
			if (_cancellationTokenSource.IsCancellationRequested)
				return;

			await UniTask.WaitUntil(() => !objectView.productionData.BlockProduction, cancellationToken: _cancellationTokenSource.Token);

			if (objectView == null || _cancellationTokenSource.IsCancellationRequested)
				return;
			
			if (_disabledProduction.Contains(objectView) || objectView.IsBroke)
			{
				return;
			}

			if (objectView.productionData.UseObjectSpawn && objectView.productionData.SpawnedSpitObject)
			{
				if (objectView.productionData.SpawnedSpitObject.gameObject.activeSelf) return;
			}

			// Ожидания выше могут разрешиться через несколько апгрейдов, когда объект уже заменён другим
			// стейтом COC: ожидание "продукт неактивен" снимает как раз следующий апгрейд, когда гасит
			// перенесённый ресурс. Скрытый стейт не должен поднимать интервал — прогресс-вью у стейтов
			// общий, и кольцо крутилось бы у здания, которого на сцене уже нет.
			await UniTask.WaitUntil(() => objectView && objectView.gameObject.activeInHierarchy,
				cancellationToken: _cancellationTokenSource.Token);

			_gameplayIntervalsController.CancelInterval(objectView.currentProductionIntervalId);

			objectView.currentProductionIntervalId = _gameplayIntervalsController.StartInterval(new GameplayIntervalSpecificParameters(
					null,
					() => IntervalStarted(objectView),
					objectView.PauseProductionWorkingAnimation,
					objectView.ResumeProductionWorkingAnimation,
					null,
					objectView.StopProductionVisuals,
					() => IntervalCompleted(objectView),
					null,
					objectView.ProductionProgressView,
					null),
				objectView.productionData.GameplayIntervalGeneralParameters);

			_gameplayIntervalsController.SetIntervalSpeed(objectView.currentProductionIntervalId, objectView.productionSpeed);
		}

		public void StopProduction(StaticObjectView objectView)
		{
			objectView.StopProductionVisuals();
			_gameplayIntervalsController.CancelInterval(objectView.currentProductionIntervalId);
			_activeProduction.Remove(objectView);
		}

		public void SetSpeed(StaticObjectView objectView, float speed)
		{
			objectView.productionSpeed = speed;
			_gameplayIntervalsController.SetIntervalSpeed(objectView.currentProductionIntervalId, speed);
		}

		public void DisableProduction(StaticObjectView objectView)
		{
			objectView.StopProductionVisuals();
			_disabledProduction.Add(objectView);
			_activeProduction.Remove(objectView);
		}

		private void SubscribeBreakable(StaticObjectView objectView)
		{
			// Dispose() навсегда хоронит CompositeDisposable — AddTo ниже на задиспоуженный
			// контейнер молча диспоузит саму подписку сразу же, и Observable.EveryUpdate() никогда
			// не долетает до Subscribe. Поэтому не переиспользуем старый контейнер, а создаём новый.
			objectView.BreakableDisposable?.Dispose();
			objectView.BreakableDisposable = new CompositeDisposable();

			Observable.EveryUpdate()
				.Where(_ => objectView.NeedBreak)
				.Subscribe(_ =>
				{
					if (_activeProduction.Contains(objectView))
					{
						var producedObject = objectView.productionData.SpawnedSpitObject;
						if (producedObject && producedObject.gameObject.activeSelf)
						{
							_breakableController.Break(objectView);
						}
					}
					else
					{
						_breakableController.Break(objectView);
					}
				})
				.AddTo(objectView.BreakableDisposable); 
		}
		
		private void IntervalStarted(StaticObjectView objectView)
		{
			_activeProduction.Add(objectView);
			objectView.StartProductionWorkingAnimation();
			objectView.productionData.IntervalStarted?.Invoke();
		}

		private async UniTaskVoid IntervalCompleted(StaticObjectView objectView)
		{
			if (objectView != null)
			{
				objectView.StopProductionVisuals();
			}

			if (objectView == null || _disabledProduction.Contains(objectView) || _cancellationTokenSource.IsCancellationRequested)
				return;

			objectView.productionData.IntervalCompleted?.Invoke();
			
			_gameplayIntervalsController.CancelInterval(objectView.currentProductionIntervalId);
			
			if (objectView.productionData.IntervalEndedSound)
			{
				_audioController.PlaySfx(objectView.productionData.IntervalEndedSound);
			}

			if (objectView.productionData.UseObjectSpawn && objectView.productionData.SpawnedSpitObject != null)
			{
				var producedObject = objectView.productionData.SpawnedSpitObject;

				await WaitUntilObjectState(producedObject.transform.parent.gameObject, true, true);

				if (objectView == null || _cancellationTokenSource.IsCancellationRequested)
					return;

				if (producedObject.gameObject.activeSelf) return;

				producedObject.gameObject.SetActive(true);
				producedObject.CanInteract = producedObject.ObjectDataSO.CanInteract;
				producedObject.SetPrimaryActionState(producedObject.ObjectDataSO.CanReactToPrimaryAction);
				producedObject.ResetInteractionState();

				if (objectView.breakableActivated && objectView.IsBroke)
				{
					StopProduction(objectView);

					objectView.productionData.OnBroke?.Invoke();

					_activeProduction.Remove(objectView);
					return;
				}

				await WaitUntilObjectState(producedObject.gameObject, false);

				if (objectView == null || _cancellationTokenSource.IsCancellationRequested)
				{
					_activeProduction.Remove(objectView);
					return;
				}

				if (producedObject) producedObject.gameObject.SetActive(false);
			}

			if (objectView == null || _cancellationTokenSource.IsCancellationRequested || (objectView.breakableActivated && objectView.IsBroke))
			{
				_activeProduction.Remove(objectView);
				return;
			}

			StartProduction(objectView).Forget();
		}

		private async UniTask WaitUntilObjectState(GameObject objectView, bool state, bool useActiveInHierarchy = false)
		{
			// См. комментарий в StartProduction: тот же самый источник контроллера мог быть
			// задиспоужен рестартом, пока предыдущий await ещё висел. Без этой проверки .Token
			// ниже кинет ObjectDisposedException — try/catch его поймает, но это лишний шум
			// в логе и лишняя попытка ждать на заведомо мёртвом токене.
			if (_cancellationTokenSource.IsCancellationRequested)
				return;

			try
			{
				await UniTask.WaitUntil(() =>
				{
					if (!objectView || !objectView.gameObject)
					{
						return true;
					}

					if (useActiveInHierarchy)
					{
						return objectView.activeInHierarchy == state;
					}

					return objectView.activeSelf == state;
				}, cancellationToken: _cancellationTokenSource.Token);
			}
			catch (System.OperationCanceledException)
			{
			}
			catch (System.Exception e)
			{
				Log.Gameplay.Error(e);
			}
		}
	}
}
