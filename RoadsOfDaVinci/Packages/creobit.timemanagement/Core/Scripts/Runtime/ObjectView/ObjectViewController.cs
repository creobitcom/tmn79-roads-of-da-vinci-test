using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Audio;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using ObservableCollections;
using Pathfinding;
using R3;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    public class ObjectViewController : IObjectViewController
    {
        private readonly IGameResourcesSystem _gameResourcesSystem;
        private readonly GameplaySceneReferences _gameplaySceneReferences;
        private UnitBaseController _unitBaseController;
        private readonly IAudioService _audioController;
        private readonly IReloadController _reloadController;
        private readonly IObjectResolver _objectResolver;
        private readonly ILevelController _levelController;
        private readonly ILevelLoader _levelLoader;
        private readonly IGameplayIntervalsController _intervalsController;
        private readonly IMovableObjectTaskBadgeController _badgeController;
        
        private ObjectSpecialTagController _objectSpecialTagController;
        private StaticObjectController _staticObjectController;
        private MovableObjectController _movableObjectController;
        private MovableObjectInventoryController _movableObjectInventoryController;
        private IMovableObjectTaskManager _movableObjectTaskManager;
        private UnitAvailabilityController _unitAvailabilityController;
        
        private List<ObjectView> _objectViews = new();

        private readonly CompositeDisposable _resourceSubscriptions = new();

        /// <summary>
        /// Состояние открытой панели выбора альтернативы. Живёт в полях, а не в замыкании,
        /// переданном в Show: замыкание нельзя пересчитать, а панель обязана обновляться,
        /// пока висит на экране.
        /// </summary>
        private ObjectView _alternativeSelectionOwner;

        /// <summary>Юнит, которому адресован клик (перетаскивание конкретного рабочего), или null.</summary>
        private MovableObjectView _alternativeSelectionUnit;

        /// <summary>Проходимость пути по индексу альтернативы. Считается асинхронно, поэтому кешируется.</summary>
        private readonly Dictionary<int, bool> _alternativeReachable = new();

        private bool _alternativeRefreshInFlight;
        private bool _alternativeRefreshQueued;


        public event Action<ObjectView> OnObjectViewAdded;
        public event Action OnAlternativeSelectionOpened;
        public event Action<ObjectView> OnAlternativeSelectionShown;
        public event Action<ObjectView> OnAlternativeSelectionClosed;
        public event Action<ObjectView> OnNotResourcesTooltip;
        public event Action<ObjectView> OnNotPathTooltip;
        public event Action<GameplayTagSO, Vector3> OnNotTagTooltip;
        public event Action<ObjectView> OnInitTooltip;

        [Inject]
        public ObjectViewController(IGameResourcesSystem gameResourcesSystem, 
            GameplaySceneReferences gameplaySceneReferences,
            IAudioService audioController,
            IReloadController reloadController,
            IObjectResolver objectResolver,
            ILevelController levelController,
            ILevelLoader levelLoader,
            IGameplayIntervalsController intervalsController,
            IMovableObjectTaskBadgeController badgeController)
        {
            _gameResourcesSystem = gameResourcesSystem;
            _gameplaySceneReferences = gameplaySceneReferences;
            _audioController = audioController;
            _reloadController = reloadController;
            _objectResolver = objectResolver;
            _levelController = levelController;
            _levelLoader = levelLoader;
            _intervalsController = intervalsController;
            _badgeController = badgeController;
        }

        public UniTask Load()
        {
            _movableObjectController = new MovableObjectController(_reloadController, _objectResolver, _levelController,
                _levelLoader, _intervalsController, _gameResourcesSystem, _gameplaySceneReferences, _badgeController);
            _staticObjectController = new StaticObjectController(_reloadController, _movableObjectController,
                _levelController, this, _intervalsController, _audioController, _levelLoader);
            _objectSpecialTagController = new ObjectSpecialTagController(_staticObjectController, _reloadController);
            
            _objectSpecialTagController.Load(); 
            _movableObjectController.Load();
            _staticObjectController.Load();
                
            _movableObjectTaskManager = _movableObjectController.GetTaskManager();
            _movableObjectInventoryController = _movableObjectController.GetInventoryController();
            _unitAvailabilityController = _movableObjectController.GetUnitAvailabilityController();
            
            _unitBaseController = new UnitBaseController(_staticObjectController, _movableObjectController,
                _movableObjectController.GetPathFindable(), _levelLoader, _reloadController,
                _unitAvailabilityController, _gameplaySceneReferences, _objectResolver,
                _movableObjectInventoryController, this);
            
            _unitBaseController.Load();

            SubscribeAlternativeSelectionOnResourceChanged(_gameResourcesSystem.Resources);
            SubscribeAlternativeSelectionOnResourceChanged(_gameResourcesSystem.InventoryResources);

            // Доступность альтернативы зависит не только от ресурсов: нужен ещё свободный юнит
            // нужного типа и проходимый до объекта путь. Раньше эта половина считалась ОДИН раз,
            // в момент открытия панели, и залипала: дрон занят на момент клика -> иконка серая ->
            // дрон вернулся -> иконка так и осталась серой.
            _movableObjectController.UnitStateChanged += OnUnitsChanged;
            _unitAvailabilityController.AvailabilityChanged += OnBaseAvailabilityChanged;
            _unitBaseController.BasementUpdated += OnBasementUpdated;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_movableObjectController != null)
            {
                _movableObjectController.UnitStateChanged -= OnUnitsChanged;
            }

            if (_unitAvailabilityController != null)
            {
                _unitAvailabilityController.AvailabilityChanged -= OnBaseAvailabilityChanged;
            }

            if (_unitBaseController != null)
            {
                _unitBaseController.BasementUpdated -= OnBasementUpdated;
            }

            _resourceSubscriptions.Dispose();
        }

        /// <summary>
        /// Пока панель выбора висит на экране, ресурсы на уровне могут измениться: юнит донёс
        /// добычу до базы, доделалась постройка, потратились на другой объект. Одна подписка на
        /// всю жизнь контроллера, а панель сама решает, есть ли что перерисовывать.
        ///
        /// Подписываемся на ОБА словаря: обычные ресурсы и спец-ресурсы (InventoryResource) лежат
        /// в разных коллекциях, а стоимость альтернативы может быть в любой из них. И на все три
        /// вида изменений: раньше слушали только Replace, а ресурс, впервые появившийся на уровне,
        /// приходит через Add (см. GameResourcesSystem.CheckResourceIsLoaded) — такое изменение
        /// панель просто не замечала.
        /// </summary>
        private void SubscribeAlternativeSelectionOnResourceChanged(
            IReadOnlyObservableDictionary<ResourceBaseSO, int> resources)
        {
            resources.ObserveReplace().Subscribe(_ => RefreshAlternativeSelectionIcons())
                .AddTo(_resourceSubscriptions);

            resources.ObserveAdd().Subscribe(_ => RefreshAlternativeSelectionIcons())
                .AddTo(_resourceSubscriptions);

            resources.ObserveRemove().Subscribe(_ => RefreshAlternativeSelectionIcons())
                .AddTo(_resourceSubscriptions);
        }

        /// <summary>
        /// Перекрасить иконки под текущие данные. Дешёвый синхронный путь: сам предикат
        /// (<see cref="IsAlternativeAvailableNow"/>) каждый раз заново считает и ресурсы,
        /// и свободных юнитов, поэтому отдельно ничего пересчитывать не надо.
        /// </summary>
        private void RefreshAlternativeSelectionIcons()
        {
            // Та же дешёвая отсечка, что в RefreshAlternativeSelection: ресурсы на уровне
            // меняются постоянно, а панель открыта редко.
            if (ReferenceEquals(_alternativeSelectionOwner, null))
            {
                return;
            }

            // Сравнение через ==, а не ?. : подписки живут дольше сцены, и на перезагрузке
            // уровня здесь может лежать уже уничтоженный объект.
            var selectionView = _gameplaySceneReferences.ObjectAlternativeSelectionView;

            if (selectionView == null)
            {
                return;
            }

            selectionView.RefreshAffordability();
        }

        private void OnUnitsChanged(MovableObjectView unit)
        {
            RefreshAlternativeSelection();
        }

        private void OnBaseAvailabilityChanged(StaticObjectView baseView)
        {
            RefreshAlternativeSelection();
        }

        private void OnBasementUpdated(StaticObjectView baseView,
            IReadOnlyDictionary<StaticObjectView, List<MovableObjectView>> basements)
        {
            RefreshAlternativeSelection();
        }

        /// <summary>
        /// На уровне закончилось взаимодействие какого-то объекта. Раньше здесь панель выбора
        /// просто закрывалась — по событию ЛЮБОГО объекта, даже постороннего, — и исчезала
        /// у игрока под руками. Теперь пересобираем: у объекта-владельца мог измениться сам набор
        /// доступных вариантов (потратилась альтернатива с InteractionsAmount, сработал
        /// DeactivateAllOthers), а у посторонних — только доступность.
        /// </summary>
        private void OnObjectInteractionStateChanged(ObjectView objectView)
        {
            RefreshAlternativeSelection();
        }

        /// <summary>
        /// Единственный вход в пересчёт панели. Дешёвая синхронная отсечка стоит здесь, а не
        /// внутри async-метода: сюда стучатся события, которые идут пачками весь уровень —
        /// смена состояния каждого юнита, конец каждого взаимодействия. Панель же открыта
        /// редкими четырёхсекундными окнами, и в 99% случаев работать не над чем.
        ///
        /// Сверка через ReferenceEquals, а не Unity-овский `== null`: это чистая проверка ссылки
        /// без обращения в нативную часть, и именно она отсекает подавляющее большинство вызовов.
        /// </summary>
        public void RefreshAlternativeSelection()
        {
            if (ReferenceEquals(_alternativeSelectionOwner, null))
            {
                return;
            }

            RefreshAlternativeSelectionAsync().Forget();
        }

        /// <summary>
        /// Полный пересчёт панели: состав вариантов + проходимость пути (асинхронная) + иконки.
        ///
        /// Повторные вызовы во время работы не накапливаются: пока идёт один пересчёт, остальные
        /// сворачиваются в один отложенный прогон. Иначе всплеск событий (юнит вернулся на базу —
        /// это сразу и смена состояния юнита, и BasementUpdated, и изменение ресурсов) запустил бы
        /// несколько параллельных поисков пути.
        /// </summary>
        private async UniTaskVoid RefreshAlternativeSelectionAsync()
        {
            if (_alternativeRefreshInFlight)
            {
                _alternativeRefreshQueued = true;

                return;
            }

            _alternativeRefreshInFlight = true;

            try
            {
                do
                {
                    _alternativeRefreshQueued = false;

                    await RefreshAlternativeSelectionOnce();
                }
                while (_alternativeRefreshQueued);
            }
            catch (OperationCanceledException)
            {
                // Сцена выгружается посреди поиска пути — штатный случай, не ошибка.
            }
            catch (Exception exception)
            {
                Log.Gameplay.Error($"Не удалось обновить панель выбора альтернативы: {exception.Message}");
            }
            finally
            {
                _alternativeRefreshInFlight = false;
            }
        }

        private async UniTask RefreshAlternativeSelectionOnce()
        {
            var selectionView = _gameplaySceneReferences.ObjectAlternativeSelectionView;

            if (selectionView == null || !selectionView.IsShown)
            {
                return;
            }

            var owner = _alternativeSelectionOwner;

            if (owner == null || !owner.gameObject.activeInHierarchy)
            {
                selectionView.Close();

                return;
            }

            var indices = GetAvailableAlternativeIndices(owner);

            if (indices.Count == 0)
            {
                // Вариантов не осталось — показывать нечего, это единственный случай,
                // когда панель закрывается не по воле игрока.
                selectionView.Close();

                return;
            }

            var reachable = new Dictionary<int, bool>(indices.Count);

            foreach (var index in indices)
            {
                // Сверка через ReferenceEquals, а НЕ через Unity-овский `== null`: поиск пути
                // асинхронный, и следующая итерация может продолжиться уже не на главном потоке,
                // где обращаться к Unity-объектам нельзя. Здесь достаточно понять, что панель
                // всё ещё про тот же объект.
                if (!ReferenceEquals(_alternativeSelectionOwner, owner))
                {
                    return;
                }

                reachable[index] = await IsAlternativeReachableAsync(owner,
                    owner.ObjectDataSO.AlternativeInteractions[index], _alternativeSelectionUnit);
            }

            await UniTask.SwitchToMainThread();

            // Пока считали путь, панель могли закрыть или открыть для другого объекта.
            if (selectionView == null || !selectionView.IsShown
                || !ReferenceEquals(_alternativeSelectionOwner, owner))
            {
                return;
            }

            _alternativeReachable.Clear();

            foreach (var pair in reachable)
            {
                _alternativeReachable[pair.Key] = pair.Value;
            }

            if (selectionView.HasSameIndices(indices))
            {
                selectionView.RefreshAffordability();
            }
            else
            {
                selectionView.Rebuild(indices);
            }
        }

        private List<int> GetAvailableAlternativeIndices(ObjectView objectView)
        {
            var indices = new List<int>();

            for (var i = 0; i < objectView.ObjectDataSO.AlternativeInteractions.Count; i++)
            {
                if (objectView.CanUseAlternative(i)) indices.Add(i);
            }

            return indices;
        }

        /// <summary>
        /// Доступна ли альтернатива ПРЯМО СЕЙЧАС. Читает поля, а не замыкание: замыкание
        /// фиксировало результат на момент открытия панели, и обновить его было нечем.
        ///
        /// Проходимость берём из <see cref="_alternativeReachable"/> — она асинхронная и
        /// пересчитывается отдельно (<see cref="RefreshAlternativeSelectionOnce"/>);
        /// ресурсы и юнитов считаем здесь же, на каждый вызов.
        /// </summary>
        private bool IsAlternativeAvailableNow(int index)
        {
            var owner = _alternativeSelectionOwner;

            return owner != null
                   && _alternativeReachable.GetValueOrDefault(index, false)
                   && IsAlternativeAffordable(owner, index);
        }

        /// <summary>
        /// Сбросить состояние закрывшейся панели — но только если она всё ещё «наша».
        ///
        /// Проверка обязательна: Show(), если панель уже висела, дёргает колбэк отмены ПРЕДЫДУЩЕГО
        /// показа уже после того, как поля переставлены на новый объект. Без сверки владельца
        /// старый колбэк обнулил бы состояние только что открытой панели, и все её иконки
        /// стали бы серыми.
        /// </summary>
        private void ClearAlternativeSelectionState(ObjectView objectView)
        {
            if (!ReferenceEquals(_alternativeSelectionOwner, objectView))
            {
                return;
            }

            _alternativeSelectionOwner = null;
            _alternativeSelectionUnit = null;
            _alternativeReachable.Clear();

            OnAlternativeSelectionClosed?.Invoke(objectView);
        }

        private static bool RequiresSelectionPanel(ObjectView objectView)
        {
            return objectView.TryGetComponent<IAlternativeSelectionPolicy>(out var policy)
                   && policy.ForceSelectionPanel;
        }

        public StaticObjectController GetStaticObjectController()
        {
            return _staticObjectController;
        }

        public MovableObjectController GetMovableObjectController()
        {
            return _movableObjectController;
        }
        
        public ObjectSpecialTagController GetObjectSpecialTagController()
        {
            return _objectSpecialTagController;
        }
        
        public UnitBaseController GetUnitBaseController()
        {
            return _unitBaseController;
        }

        private bool TryGetRelevantBasesForAlternative(ObjectView objectView, InteractionAlternative alternative,
            out List<StaticObjectView> relevantBases)
        {
            relevantBases = new List<StaticObjectView>();

            var unitTypes = alternative.UnitTypeCount;
            if (unitTypes == null || unitTypes.Count == 0)
            {
                return true;
            }

            foreach (var typeCount in unitTypes)
            {
                var typeBases = GetRelevantBases(objectView, typeCount);
                if (typeBases == null)
                {
                    relevantBases.Clear();
                    return false;
                }

                relevantBases.AddRange(typeBases);
            }

            return relevantBases.Count > 0;
        }

        private bool IsAlternativeAffordable(ObjectView objectView, int alternativeIndex)
        {
            var alternatives = objectView.ObjectDataSO.AlternativeInteractions;
            if (alternativeIndex < 0 || alternativeIndex >= alternatives.Count)
            {
                return false;
            }

            var alternative = alternatives[alternativeIndex];

            var resources = alternative.InputResources;
            if (resources != null && resources.Length > 0
                && !_gameResourcesSystem.IsEnoughResources(resources))
            {
                return false;
            }

            return TryGetRelevantBasesForAlternative(objectView, alternative, out _);
        }

        private async UniTask<bool> IsAlternativeReachableAsync(ObjectView objectView,
            InteractionAlternative alternative, MovableObjectView specificUnit)
        {
            if (specificUnit != null)
            {
                var seeker = specificUnit.GetComponent<Seeker>();
                var pathResult = await _movableObjectController.GetPathFindable().IsPathWalkable(
                    specificUnit.transform.position,
                    objectView.interactionPosition,
                    seeker);

                return pathResult.Item1;
            }

            if (!TryGetRelevantBasesForAlternative(objectView, alternative, out var relevantBases))
            {
                return false;
            }

            if (relevantBases.Count == 0)
            {
                return true;
            }

            var pathCheck = await _unitBaseController.IsPathWalkable(objectView.interactionPosition, relevantBases);
            return pathCheck.Item1;
        }

        /// </summary>
        /// <param name="isProcessing">Indicates whether the task registration should process additional validation feedback.</param>
        /// <returns>A task containing a boolean value indicating success (true) or failure (false) of the task registration.</returns>
        private async void TryRegisterTask(ObjectView objectView, MovableObjectView specificUnit)
        {
            // Объявлено снаружи try: finally решает по нему, снимать ли признак занятости.
            var taskRegistered = false;

            try
            {
                var destroyToken = objectView.GetCancellationTokenOnDestroy();
                var availableIndices = GetAvailableAlternativeIndices(objectView);

                if (objectView.ActiveAlternativeIndex == -1 && objectView.ObjectDataSO.AlternativeInteractions.Count > 0)
                {
                    if (availableIndices.Count == 0) return; 

                    if (availableIndices.Count == 1 && !RequiresSelectionPanel(objectView))
                    {
                        objectView.SelectAlternative(availableIndices[0]);
                    }
                    else
                    {
                        var selectionView = _gameplaySceneReferences.ObjectAlternativeSelectionView;
                        if (selectionView != null)
                        {
                            // Проходимость асинхронная (поиск пути), поэтому кешируется. Но кеш
                            // теперь лежит в поле, а не в замыкании: пока панель висит, путь может
                            // открыться или закрыться (дрон вернулся на базу, достроился мост),
                            // и RefreshAlternativeSelectionAsync пересчитывает его на лету.
                            var reachableByIndex = new Dictionary<int, bool>();

                            foreach (var idx in availableIndices)
                            {
                                if (destroyToken.IsCancellationRequested)
                                {
                                    Interlocked.Exchange(ref objectView.isRegisteringTask, 0);
                                    return;
                                }

                                var alternative = objectView.ObjectDataSO.AlternativeInteractions[idx];
                                reachableByIndex[idx] =
                                    await IsAlternativeReachableAsync(objectView, alternative, specificUnit);
                            }

                            await UniTask.SwitchToMainThread();

                            if (destroyToken.IsCancellationRequested)
                            {
                                Interlocked.Exchange(ref objectView.isRegisteringTask, 0);
                                return;
                            }

                            // Строго ДО Show: панель спрашивает предикат уже при сборке кнопок,
                            // а он читает эти поля.
                            _alternativeSelectionOwner = objectView;
                            _alternativeSelectionUnit = specificUnit;
                            _alternativeReachable.Clear();

                            foreach (var pair in reachableByIndex)
                            {
                                _alternativeReachable[pair.Key] = pair.Value;
                            }

                            OnAlternativeSelectionOpened?.Invoke();

                            selectionView.Show(objectView, availableIndices,
                                IsAlternativeAvailableNow,
                                (idx) =>
                                {
                                    ClearAlternativeSelectionState(objectView);
                                    objectView.SelectAlternative(idx);
                                    objectView.TryRegisterTask(specificUnit);
                                },
                                () =>
                                {
                                    ClearAlternativeSelectionState(objectView);
                                    objectView.ResetAlternative();
                                },
                                _gameplaySceneReferences.MainCamera, _gameplaySceneReferences.MainCanvas);

                            OnAlternativeSelectionShown?.Invoke(objectView);
                        }
                        Interlocked.Exchange(ref objectView.isRegisteringTask, 0);
                        return;
                    }
                }
                

                // Early synchronous guards
                if (!_gameplaySceneReferences.GameplaySettings.CanCancelTaskDuringRunning &&
                    _movableObjectTaskManager.IsTaskStarted(objectView))
                {
                    return;
                }
                

                if (TryCancelTask(objectView))
                {
                    return;
                }

                if (!objectView.CanInteract || !objectView.gameObject.activeInHierarchy ||
                    objectView.IsFinalInteraction())
                {
                    objectView.ResetAlternative(); 
                    return;
                }
                
                bool anyPathWalkable = false;
                List<StaticObjectView> closestTransform = new List<StaticObjectView>();

                // Units/resources basic validation
                if (specificUnit != null)
                {
                    if (!objectView.IsEnoughResources())
                    {
                        // Тултип строится по ВЫБРАННОЙ альтернативе (человек или дрон):
                        // сбрасываем выбор только ПОСЛЕ показа, иначе он читает объединение всех.
                        OnNotResourcesTooltip?.Invoke(objectView);
                        objectView.ResetAlternative();
                        return;
                    }

                    var seeker = specificUnit.GetComponent<Seeker>();
                    var pathResult = await _movableObjectController.GetPathFindable().IsPathWalkable(
                        specificUnit.transform.position,
                        objectView.interactionPosition,
                        seeker);

                    anyPathWalkable = pathResult.Item1;
                    if (specificUnit.CurrentBasement != null)
                    {
                        closestTransform.Add(specificUnit.CurrentBasement);
                    }
                }
                else
                {
                    if (!GetRelevantBases(objectView, out var relevantBases) || !objectView.IsEnoughResources())
                    {
                        // Тултип строится по ВЫБРАННОЙ альтернативе (человек или дрон):
                        // сбрасываем выбор только ПОСЛЕ показа, иначе он читает объединение всех.
                        OnNotResourcesTooltip?.Invoke(objectView);
                        objectView.ResetAlternative();
                        return;
                    }

                    var pathResult =
                        await _unitBaseController.IsPathWalkable(objectView.interactionPosition, relevantBases);
                    anyPathWalkable = pathResult.Item1;
                    closestTransform = pathResult.Item2;
                }

                await UniTask.SwitchToMainThread();

                if (destroyToken.IsCancellationRequested || objectView == null)
                {
                    if (objectView != null) objectView.ResetAlternative(); 
                    return;
                }

                if (!anyPathWalkable || closestTransform == null || closestTransform.Count == 0)
                {
                    if (specificUnit != null)
                    {
                        var seeker = specificUnit.GetComponent<Seeker>();
                        _unitBaseController.ShowPathFromUnit(specificUnit.transform.position, objectView.interactionPosition, seeker).Forget();
                    }
                    else
                    {
                        if (closestTransform == null || closestTransform.Count == 0)
                        {
                            var typeCount = objectView.GetCurrentUnitTypeCount().FirstOrDefault();
                            if (typeCount != null)
                            {
                                closestTransform = _unitBaseController.GetRelevantBases(typeCount.unitType, typeCount.tagMode).relevantBases;
                            }
                        }

                        if (closestTransform != null && closestTransform.Count > 0)
                        {
                            _unitBaseController.ShowPath(objectView.interactionPosition, closestTransform).Forget();
                        }
                    }

                    objectView.ResetAlternative(); 
                    OnNotPathTooltip?.Invoke(objectView);
                    return;
                }

                var objectViewList = new List<ITaskObject>();
                var lastPoint = objectView.Position;

                foreach (var gameplayTagSO in objectView.ObjectDataSO.TaskTypeTags)
                {
                    if (gameplayTagSO.TagType != TagType.Object)
                        continue;

                    var newPoint = _objectSpecialTagController.GetObjectWithTag(gameplayTagSO, lastPoint);
                    if (newPoint != null)
                    {
                        var canUse = await CanUse(objectView);
                        await UniTask.SwitchToMainThread();

                        if (destroyToken.IsCancellationRequested || objectView == null)
                        {
                            if (objectView != null) objectView.ResetAlternative(); 
                            return;
                        }

                        if (canUse)
                        {
                            lastPoint = newPoint.Position;
                            objectViewList.Add(newPoint);
                            continue;
                        }
                    }

                    objectView.ResetAlternative(); 
                    OnNotTagTooltip?.Invoke(gameplayTagSO, 
                        objectView.transform.position + objectView.localPathTooltipOffset);
                    return;
                }

                objectViewList.Add(objectView);


                // Re-validate critical conditions right before commit (avoid TOCTOU issues)
                if (!objectView.gameObject.activeInHierarchy || !objectView.CanInteract ||
                    objectView.IsFinalInteraction())
                {
                    objectView.ResetAlternative(); 
                    return;
                }

                if (!_gameplaySceneReferences.GameplaySettings.CanCancelTaskDuringRunning &&
                    _movableObjectTaskManager.IsTaskStarted(objectView))
                {
                    objectView.ResetAlternative(); 
                    return;
                }

                if (!objectView.IsEnoughResources())
                {
                    // Тултип строится по ВЫБРАННОЙ альтернативе (человек или дрон):
                    // сбрасываем выбор только ПОСЛЕ показа, иначе он читает объединение всех.
                    OnNotResourcesTooltip?.Invoke(objectView);
                    objectView.ResetAlternative();
                    return;
                }

                // Commit phase: subtract resources, register task, then side-effects
                var didSubtract = false;

                // Объект считается занятым с начала фазы коммита, а не с момента списания:
                // во-первых, списание само дёргает обновление тултипа, и без флага он успел бы
                // перекраситься в красное «не хватает» — хотя за взаимодействие уже заплачено;
                // во-вторых, у бесплатных объектов списания нет вовсе, а прятать тултип на время
                // работы надо и им.
                //
                // Снимается в finally, если задача так и не оформилась.
                objectView.SetInteractionPending(true);

                try
                {
                    if (objectView.GetCurrentInteractionNeedInputResources())
                    {
                        // Make a final resources check and subtract atomically
                        if (_gameResourcesSystem.IsEnoughResources(objectView.GetCurrentInputResources()))
                        {
                            _gameResourcesSystem.SubtractResource(objectView.GetCurrentInputResources());
                            objectView.resourcesSubtracted = didSubtract = true;
                        }
                        else
                        {
                            // Тултип строится по ВЫБРАННОЙ альтернативе (человек или дрон):
                            // сбрасываем выбор только ПОСЛЕ показа, иначе он читает объединение всех.
                            OnNotResourcesTooltip?.Invoke(objectView);
                            objectView.ResetAlternative();
                            return;
                        }
                    }

                    var task = await GetTask(objectView, closestTransform, objectViewList, specificUnit);
                    await UniTask.SwitchToMainThread();

                    if (destroyToken.IsCancellationRequested || objectView == null || task == null)
                    {
                        if (didSubtract) TryRestoreInputResources(objectView);
                        if (objectView != null) objectView.ResetAlternative();

                        return;
                    }

                    // Задача оформлена: с этого места объект занят по-настоящему,
                    // и finally уже не снимет признак.
                    taskRegistered = true;

                    // Side-effects AFTER successful registration
                    objectView.onRegisterTask?.Invoke();

                    if (objectView.GetCurrentInteractionNeedInputResources() && didSubtract)
                    {
                        _gameplaySceneReferences.ResourceAmountSubtracted
                            .ShowResourcesAmounts(objectView.GetCurrentInputResources(),
                                _gameplaySceneReferences.ResourcesView, objectView.transform, Vector3.up)
                            .Forget();
                    }

                    _audioController.PlaySfx(objectView.ObjectDataSO.TaskRegisterSound);

                    // Initialize per-interaction counters
                    objectView.CurrentUnitInteractCount.Clear();
                    foreach (var typeCount in objectView.GetCurrentUnitTypeCount())
                    {
                        var typeCountCopy = new UnitTypeCount
                        {
                            count = 0, // current count starts at 0
                            tagMode = typeCount.tagMode,
                            unitType = typeCount.unitType
                        };
                        objectView.CurrentUnitInteractCount.Add(typeCountCopy);
                    }
                }
                catch (Exception)
                {
                    // Roll back resources on any error
                    if (didSubtract)
                    {
                        TryRestoreInputResources(objectView);
                    }

                    objectView.ResetAlternative();
                }
            }
            finally
            {
                // Allow future registrations
                Interlocked.Exchange(ref objectView.isRegisteringTask, 0);

                if (objectView != null)
                {
                    // Задача так и не оформилась (не хватило ресурсов, нет пути, отменили
                    // повторным кликом, открылась панель выбора, упало исключение) — объект
                    // не занят, тултип должен вернуться.
                    if (!taskRegistered)
                    {
                        objectView.SetInteractionPending(false);
                    }

                    // Одно сообщение на ВСЕ выходы. Пока взведён флаг регистрации, объект
                    // считается занятым и тултип спрятан; снимается флаг строкой выше, и без
                    // этого сообщения подсказка осталась бы скрытой до постороннего события.
                    objectView.NotifyInteractionStateChanged();
                }
            }
        }
        public void TryRestoreInputResources(ObjectView objectView)
        {
            if (!objectView.GetCurrentInteractionNeedInputResources() || !objectView.resourcesSubtracted)
            {
                return;
            }

            _gameResourcesSystem.AddResource(objectView.GetCurrentInputResources());

            objectView.resourcesSubtracted = false;
        }
        

        /// <summary>
        /// Check enough relevant units and fill relevant home points.
        /// </summary>
        /// <param name="relevantBases">Home points to fill.</param>
        /// <returns>True if enough, otherwise false.</returns>
        private bool GetRelevantBases(ObjectView objectView, out List<StaticObjectView> relevantBases)
        {
            relevantBases = new List<StaticObjectView>();

            foreach (var typeCount in objectView.GetCurrentUnitTypeCount())
            {
                var typeBases = GetRelevantBases(objectView, typeCount);

                if (typeBases == null)
                {
                    Log.Gameplay.Info("Not enough relevant units");
                    return false;
                }

                relevantBases.AddRange(typeBases);
            }

            return relevantBases.Count > 0;
        }
        

        /// <summary>
        /// Get relevant home points and unit count for this object task
        /// </summary>
        /// <returns>Pair: list of relevant home points and count of relevant units.</returns>
        protected virtual List<StaticObjectView> GetRelevantBases(ObjectView objectView, UnitTypeCount typeCount)
        {
            var relevantBases = _unitBaseController.GetRelevantBases(typeCount.unitType, typeCount.tagMode);

            if (relevantBases.relevantUnitsCount < typeCount.count)
                return null;

            var availableBases = new List<StaticObjectView>();
            var totalAvailable = 0;

            foreach (var baseTransform in relevantBases.relevantBases)
            {
                var unitCount = _unitAvailabilityController.GetAvailableUnitCount(baseTransform);
                if (unitCount > 0)
                {
                    availableBases.Add(baseTransform);
                    totalAvailable += unitCount;
                }
            }

            return totalAvailable >= typeCount.count ? availableBases : null;
        }
        
        public async UniTask<bool> CanUse(ObjectView objectView)
        {
            var canUse = GetRelevantBases(objectView, out var relevantBases)
                         && !objectView.IsFinalInteraction()
                         && objectView.IsEnoughResources()
                         && objectView.gameObject.activeInHierarchy;

            if (!canUse)
                return false;

            var (anyPathWalkable, closestTransform) =
                await _unitBaseController.IsPathWalkable(objectView.interactionPosition, relevantBases);

            return anyPathWalkable;
        }

        /// <summary>
        /// Register task and get units who are busy for this task
        /// </summary>
        /// <param name="closestTransform">List of home points sorted by priority (distance to object).</param>
        /// <param name="objectsNeedSequence">Sequence of objects to activate this (if null contains only final object).</param>
        /// <returns>List of units who are busy for this task.</returns>
        protected virtual async UniTask<MovableObjectTask> GetTask(ObjectView objectView,
            List<StaticObjectView> closestTransform,
            List<ITaskObject> objectsNeedSequence,
            MovableObjectView specificUnit = null) 
        {
            var specificUnitsList = specificUnit != null ? new List<MovableObjectView> { specificUnit } : null;
            var waitUntilStarted = objectView is not MovableObjectView movableObject ||
                                   !movableObject.MovableObjectDataSO.UseResourcesStealer;

            return await _movableObjectTaskManager.RegisterNewTask(closestTransform,
                objectView,
                objectsNeedSequence,
                null,
                isNeedToReturn: objectView.ObjectDataSO.InteractionHaveOutputResources,
                specificUnits: specificUnitsList,
                waitUntilStarted: waitUntilStarted);
        }

        public void AddObjectView(ObjectView objectView)
        {
            SetupObjectView(objectView);
            objectView.OnProvideOutputResources += ProvideOutputResources;
            objectView.OnEndInteract += UpdateBadgesOnInteractionEnd;

            // Именно OnInteractionStateChanged, а не OnEndInteract: последний стреляет посреди
            // мутаций (ActiveAlternativeIndex ещё не сброшен), и пересобранная по нему панель
            // читала бы промежуточное состояние.
            objectView.OnInteractionStateChanged += OnObjectInteractionStateChanged;

            _objectViews.Add(objectView);
            
            OnInitTooltip?.Invoke(objectView);
            OnObjectViewAdded?.Invoke(objectView);
        }
        
        private void SetupObjectView(ObjectView objectView)
        {
            objectView.OnRegisterTask += TryRegisterTask;
            objectView.OnStartInteract += OnStartInteraction;
            
            if (objectView is StaticObjectView staticObject)
            {
                var useTroll = staticObject.CanProduceObjects && staticObject.productionData.UseObjectSpawn;
                var trollExist = _objectSpecialTagController.IsExistsBaseTag("SpeedTrollBase", out _);
                objectView.CanInteract = objectView.ObjectDataSO.CanInteract && (!useTroll || trollExist);
            }
        }
        
        /// <summary>
        /// Try cancel task.
        /// </summary>
        /// <returns>True if cancel successful, otherwise false.</returns>
        public bool TryCancelTask(ObjectView objectView)
        {
            if (_movableObjectTaskManager.CancelTask(objectView.CurrentTask, objectView.interactionStarted,
                    objectView.GetCurrentInputResources()))
            {
                // Restore before ResetAlternative(), while the cancelled interaction data is current.
                TryRestoreInputResources(objectView);
                CancelInteract(objectView);

                return true;
            }

            return false;
        }
        
        private void CancelInteract(ObjectView objectView)
        {
            objectView._cancellationTokenSource?.Cancel();
            // Full reset (busy flags + arrival lists): covers cancellation before the
            // interaction started, when the token cancel has no awaiter to clean up.
            // Заодно снимает InteractionPending — задачу отменили, ресурсы вернули.
            objectView.ResetInteractionState();

            objectView.ResetAlternative();

            // ResetInteractionState правит поля молча, поэтому сообщаем отдельно:
            // тултип должен вернуться сразу, а не когда что-то ещё дёрнет обновление.
            objectView.NotifyInteractionStateChanged();
        }
        
        private void UpdateBadgesOnInteractionEnd(ObjectView objectView)
        {
            // IsUsing was just cleared; badges hidden for the duration of the interaction
            // (e.g. the badge of a back-to-back task on the same object) must be re-rendered,
            // otherwise the queued->current swap is never shown.
            _movableObjectTaskManager.UpdateBadges();
        }

        private void OnStartInteraction(ObjectView objectView)
        {
            objectView.IsUsing = true;
            _movableObjectTaskManager.UpdateBadges();

            if (objectView.GetCurrentUnitTypeCount().Count > 0 && objectView.CurrentTask?.Units != null)
            {
                foreach (var taskUnit in objectView.CurrentTask.Units)
                {
                    if (objectView.ObjectDataSO.MovableObjectHideOnInteract)
                    {
                        taskUnit.modelTransform.gameObject.SetActive(false);
                    }
                    taskUnit.StartUnitInteract();
                }
            }
        }
        
        private void ProvideOutputResources(ObjectView objectView)
        {
            if (!objectView.ObjectDataSO.InteractionHaveOutputResources)
            {
                return;
            }

            if (objectView.ObjectDataSO.GivesResourcesImmediatelyAfterInteraction)
            {
                _gameplaySceneReferences.ResourceAmountAdded
                    .ShowResourcesAmounts(objectView.GetCurrentInputResources(), 
                        _gameplaySceneReferences.ResourcesView, objectView.transform, Vector3.up)
                    .Forget();

                _gameResourcesSystem.AddResource(objectView.ObjectDataSO.OutputResources);

                return;
            }

            if (objectView.CurrentTask?.Units == null || objectView.CurrentTask.Units.Count == 0)
            {
                Log.Gameplay.Error($"ProvideOutputResources: no task units on {objectView.name}, output resources skipped");
                return;
            }

            _movableObjectInventoryController.AddResources(objectView.CurrentTask.Units[0],
                objectView.ObjectDataSO.OutputResources);

        }
    }
}
