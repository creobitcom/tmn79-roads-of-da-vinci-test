using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.AddressablesController;
using Creobit.Localization;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using ObservableCollections;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller
{
    public class TooltipController : ITooltipController
    {
        private readonly GameplaySceneReferences _gameplaySceneReferences;
        private readonly IObjectResolver _resolver;
        private readonly IReloadController _reloadController;
        private readonly IGameResourcesSystem _gameResourcesSystem;
        private readonly IAddressablesController _addressablesController;
        private readonly IObjectViewController _objectViewController;
        private UnitBaseController _unitBaseController;
        
        /// <summary>
        /// Что СЕЙЧАС НАРИСОВАНО на экране. Обнуляется любым скрытием.
        /// </summary>
        public IReactToActions CurrentPrimaryTooltipObject { get; set; }

        /// <summary>
        /// Кого игрок ДЕРЖИТ ПОД КУРСОРОМ. Снимается только по-настоящему: курсор ушёл
        /// с объекта (OnHideTooltip), объект уничтожен, перезагрузился уровень.
        ///
        /// Раньше такого состояния не было вовсе — был только CurrentPrimaryTooltipObject,
        /// и любое скрытие (конец взаимодействия, показ служебной подсказки, достройка COC)
        /// стирало контекст навсегда: вернуть тултип было нечем, пока игрок не уводил
        /// и не наводил курсор заново. Именно поэтому подсказка «исчезала сама».
        /// </summary>
        private IReactToActions _hoverTarget;


        private TooltipView _tooltipView;
        private ResourceNotEnoughTooltipView _resourceNotEnoughView;
        private NoPathTooltipView _noPathView;
        private CantReachAnotherObjectTooltipView _cantReachAnotherObjectView;
        private TooltipResourceView _tooltipResourceView;
        private ExchangeTooltipView _exchangeTooltipView;

        private ObjectPool<TooltipResourceView> _resourceViews;

        /// <summary>
        /// Инстансы новых (LA8) карточек: шаблон → вьюха. Заполняется в Load() предзагрузкой,
        /// потому что показ тултипа синхронный и грузить на месте не может.
        /// </summary>
        /// <summary>
        /// Карточки по GUID префаба, а НЕ по ссылке на TooltipTemplate.
        ///
        /// В билде один и тот же шаблон существует в ДВУХ экземплярах: копия внутри сцены
        /// Gameplay (она ссылается на GameplaySettings напрямую, а тот держит список шаблонов —
        /// значит всё это зашивается в player data) и копия из бандла (её видит ObjectDataSO,
        /// приезжающий с уровнем). Unity не шарит ассет между player data и бандлом: бандл
        /// обязан быть самодостаточным. Сравнение по ссылке эти копии не отождествляет, и
        /// карточка молча не находилась — объект уходил на старый тултип.
        ///
        /// В редакторе такого нет: AssetDatabase отдаёт один экземпляр на файл, поэтому баг
        /// виден ТОЛЬКО в билде.
        ///
        /// Это лечение симптома. По-хорошему шаблоны не должны лежать в GameplaySettings —
        /// тогда они существовали бы только в мире бандлов, в единственном экземпляре.
        /// </summary>
        private readonly Dictionary<string, ITooltipCardView> _cardViews = new();

        /// <summary>
        /// ВСЕ первичные тултипы (старые + карточки) — по нему идут Hide/Active.
        /// Заполняется руками, а НЕ проверкой `is IPrimaryTooltipView`: этот интерфейс не разделяет
        /// первичные и вторичные — ResourceNotEnoughTooltipView реализует ОБА, и авто-сбор
        /// затянул бы вторичный тултип в список первичных.
        /// </summary>
        private readonly List<ITooltipView> _primaryViews = new();

        private readonly Dictionary<ITooltipView, TooltipCardAnimator> _animators = new();

        private readonly List<TooltipResourceView> _activeInputResources = new();
        private readonly List<TooltipResourceView> _activeOutputResources = new();
        private readonly List<TooltipResourceView> _activeSecondaryInputResources = new();

        /// <summary>
        /// Подписки на изменение ресурсов. Теперь их ровно шесть на весь контроллер
        /// (два словаря × Add/Remove/Replace), а не по две на каждый объект уровня.
        ///
        /// Раньше InitTooltip заводил свою пару подписок на КАЖДЫЙ объект: сотня объектов — двести
        /// подписок, и любое изменение ресурса прогоняло их все, чтобы 199 раз сравнить ссылки
        /// и ничего не сделать. Плюс InitTooltip зовётся заново после каждой перезагрузки уровня,
        /// а Reload эти подписки не чистил — они копились между рестартами.
        /// Обновлять надо ровно одно место: тот тултип, что сейчас под курсором.
        /// </summary>
        private readonly CompositeDisposable _resourceSubscriptions = new();

        private Canvas _tooltipCanvas;

        /// <summary>Буфер под GetWorldCorners — чтобы не аллоцировать массив на каждый показ.</summary>
        private readonly Vector3[] _anchorCorners = new Vector3[4];

        private RectTransform _uiCardAnchor;

        /// <summary>
        /// Карточка служебных подсказок («нет пути» / «не хватает ресурсов» / «не дотянуться»).
        /// Одна на все три: карточка прячет пустые блоки сама, поэтому разница только в данных.
        /// null — шаблон не задан, работают старые префабы.
        ///
        /// Сознательно НЕ в _primaryViews: этот список гасит HidePrimaryTooltip, а служебная
        /// подсказка вторична — её бы сносило показом любого тултипа объекта.
        /// </summary>
        private ITooltipCardView _notEnoughResourcesCard;
        private ITooltipCardView _messageCard;

        /// <summary>
        /// Какая служебная карточка сейчас на экране. Нужна отдельно от двух полей выше:
        /// прятать и проверять активность надо ту, что реально показана, а не обе подряд.
        /// </summary>
        private ITooltipCardView _activeSecondaryCard;

        /// <summary>
        /// Таймер служебной подсказки: по нему она снимается и на её место возвращается основной
        /// тултип. Один на оба вида подсказок — и на новые карточки, и на старые префабы.
        ///
        /// У старых префабов есть и свой внутренний отсчёт (Duration + DelayHide), он остаётся:
        /// вьюха по-прежнему гасит себя сама. Но полагаться на него для ВОЗВРАТА нельзя —
        /// подробности в <see cref="ScheduleSecondaryHide"/>.
        /// </summary>
        private CancellationTokenSource _secondaryHide;

        [Inject]
        private TooltipController(IObjectResolver resolver,
            IReloadController reloadController,
            GameplaySceneReferences gameplaySceneReferences,
            IGameResourcesSystem gameResourcesSystem,
            IAddressablesController addressablesController,
            IObjectViewController objectViewController)
        {
            _gameplaySceneReferences = gameplaySceneReferences;
            _resolver = resolver;
            _reloadController = reloadController;
            _gameResourcesSystem = gameResourcesSystem;
            _addressablesController = addressablesController;
            _objectViewController = objectViewController;
        }

        public async UniTask Load()
        {
            _unitBaseController = _objectViewController.GetUnitBaseController();
            
            _tooltipCanvas = _gameplaySceneReferences.GameplayCanvasLayers[2];

            var settings = _gameplaySceneReferences.GameplaySettings;

            // Карточки грузим ПЕРВЫМИ, потому что от них зависит, нужен ли старый префаб.
            // Порядок заодно даёт запасной путь: если шаблон задан, но сломан, карточка
            // останется null — и старый префаб всё-таки загрузится, подсказка не пропадёт.
            await LoadCardTemplates();

            _notEnoughResourcesCard = await LoadSecondaryCard(
                settings.NotEnoughResourcesTooltipTemplate, "не хватает ресурсов");

            _messageCard = await LoadSecondaryCard(
                settings.MessageTooltipTemplate, "нет пути / не дотянуться");

            _tooltipView = await SpawnFromReference<TooltipView>(settings.TooltipPrefab);

            // Обмен есть не в каждом проекте: в LA8 объектов с IsExchangeTooltip нет вовсе.
            // Пустая ссылка в настройках = не грузим и не спавним.
            _exchangeTooltipView = await SpawnFromReference<ExchangeTooltipView>(
                settings.ExchangeTooltipPrefab);

            if (_notEnoughResourcesCard == null)
            {
                _resourceNotEnoughView = await SpawnFromReference<ResourceNotEnoughTooltipView>(
                    settings.NotEnoughResourceTooltipPrefab);
            }

            if (_messageCard == null)
            {
                _noPathView = await SpawnFromReference<NoPathTooltipView>(settings.NoPathTooltipPrefab);

                _cantReachAnotherObjectView = await SpawnFromReference<CantReachAnotherObjectTooltipView>(
                    settings.CantReachAnotherObjectTooltipPrefab);
            }

            // Пул ячеек нужен только старым тултипам; карточки рисуют свои ячейки из префаба.
            _tooltipResourceView = await LoadFromReference<TooltipResourceView>(
                settings.TooltipResourcePrefab);

            _resourceViews = new ObjectPool<TooltipResourceView>(CreateTooltipResourceView);

            if (_tooltipView != null)
            {
                _primaryViews.Add(_tooltipView);
            }

            if (_exchangeTooltipView != null)
            {
                _primaryViews.Add(_exchangeTooltipView);
            }

            _objectViewController.OnInitTooltip += InitTooltip;
            _objectViewController.OnAlternativeSelectionOpened += HideForAlternativeSelection;
            _objectViewController.OnNotResourcesTooltip += ShowResourceNotEnough;
            _objectViewController.OnNotPathTooltip += ShowNoPath;
            _objectViewController.OnNotTagTooltip += ShowCantReachAnotherObjectTooltip;

            SubscribeOnResourcesChanged(_gameResourcesSystem.Resources);
            SubscribeOnResourcesChanged(_gameResourcesSystem.InventoryResources);

            // Число рабочих в тултипе («нужно 2 / есть 1») берётся из состава баз, а не из ресурсов,
            // и об его изменении словари ресурсов ничего не сообщают. Без этих двух подписок
            // строка с рабочими оставалась той, что была на момент наведения.
            if (_unitBaseController != null)
            {
                _unitBaseController.BasementUpdated += OnBasementUpdated;
                _unitBaseController.UnitMaxStateChanged += OnUnitCountChanged;
            }
            else
            {
                // Штатно сюда не попасть: ObjectViewController грузится раньше. Но если порядок
                // загрузки когда-нибудь поменяют, тултип молча перестанет обновлять рабочих —
                // такое лучше увидеть в логе, чем ловить как «иногда не то число».
                Log.Gameplay.Error("Тултип: UnitBaseController недоступен на момент загрузки. "
                                   + "Число рабочих в подсказке обновляться не будет.");
            }

            _reloadController.AddReloadableObject(this);
        }

        public UniTask Reload()
        {
            // Уровень перезапускается — объект под курсором уничтожен вместе со сценой.
            _hoverTarget = null;

            HideEverythingInstantly();

            // Пула может не быть: старые тултипы отключены целиком (все на карточках).
            if (_tooltipResourceView != null)
            {
                _tooltipResourceView.gameObject.SetActive(false);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Хэндлы Addressables здесь сознательно НЕ освобождаются: ими владеет
        /// AddressablesController (Singleton, кэш по guid, переиспользуется всеми потребителями).
        /// Release отсюда рассинхронизировал бы его кэш — он отдал бы следующему потребителю
        /// уже освобождённый хэндл, и падало бы в чужом месте (комиксы, артефакты, гайды).
        /// Префабы карточек намеренно живут всю сессию: они нужны на каждом уровне.
        /// Понадобится выгрузка — только через _addressablesController.UnloadAssetReference().
        ///
        /// Раньше тут стоял цикл Addressables.Release по списку _asyncOperationHandles, который
        /// НИКОГДА не заполнялся (LoadFromReference получает от контроллера готовый GameObject,
        /// а не хэндл). Код выглядел как работающая очистка и вводил в заблуждение.
        /// </summary>
        public void Dispose()
        {
            // Без отмены таймер доживёт до выгрузки сцены и попробует показать тултип
            // на уже уничтоженных вьюхах.
            CancelSecondaryHide();

            CancelAnimations();

            _resourceSubscriptions.Dispose();

            _objectViewController.OnInitTooltip -= InitTooltip;
            _objectViewController.OnAlternativeSelectionOpened -= HideForAlternativeSelection;
            _objectViewController.OnNotResourcesTooltip -= ShowResourceNotEnough;
            _objectViewController.OnNotPathTooltip -= ShowNoPath;
            _objectViewController.OnNotTagTooltip -= ShowCantReachAnotherObjectTooltip;

            if (_unitBaseController != null)
            {
                _unitBaseController.BasementUpdated -= OnBasementUpdated;
                _unitBaseController.UnitMaxStateChanged -= OnUnitCountChanged;
            }

            _hoverTarget = null;

            _reloadController.RemoveReloadableObject(this);
        }

        /// <summary>
        /// Незаданная ссылка — это НЕ ошибка: так проект отключает ненужный ему тултип
        /// (например, обмен в LA8). Возвращаем null, вызывающий решает, что с этим делать.
        /// </summary>
        private async UniTask<T> LoadFromReference<T>(AssetReference reference) where T : Object
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                return null;
            }

            var handle = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(reference);

            return handle != null ? handle.GetComponent<T>() : null;
        }

        private async UniTask<T> SpawnFromReference<T>(AssetReference reference) where T : Component, ITooltipView
        {
            var prefab = await LoadFromReference<T>(reference);

            return prefab == null ? null : SpawnTooltip<T>(prefab);
        }

        private T SpawnTooltip<T>(ITooltipView tooltipView) where T : ITooltipView
        {
            var instance = (ITooltipView)_resolver.Instantiate((Component)tooltipView, _tooltipCanvas.transform);

            instance.CurrentGameObject.SetActive(false);

            RegisterAnimator(instance);

            return (T)instance;
        }

        private void RegisterAnimator(ITooltipView tooltipView)
        {
            if (!tooltipView.CurrentGameObject.TryGetComponent<TooltipCardAnimator>(out var animator))
            {
                return;
            }

            if (tooltipView is not ITooltipCardView)
            {
                Log.Gameplay.Error($"На префабе '{tooltipView.CurrentGameObject.name}' висит TooltipCardAnimator, "
                                   + "но это не карточка тултипа. Анимации показа работают только у карточек.");

                return;
            }

            _animators[tooltipView] = animator;
        }

        public void ShowTooltip(IReactToActions target,
            TooltipSettings tooltipSettings,
            ResourceAmount[] inputResources,
            ResourceAmount[] outputResources,
            Vector3 position,
            ResourceAmount[] haveResources = null)
        {
            ShowPrimaryTooltip(_tooltipView, target, tooltipSettings, inputResources,
                outputResources, position, haveResources, true);
        }

        public void ShowExchangeTooltip(IReactToActions target,
            TooltipSettings tooltipSettings, ResourceAmount[] inputResources,
            ResourceAmount[] outputResources, Vector3 position,
            ResourceAmount[] haveResources = null)
        {
            ShowPrimaryTooltip(_exchangeTooltipView, target, tooltipSettings, inputResources,
                outputResources, position, haveResources, false);
        }

        public void ShowResourceNotEnoughTooltip(TooltipSettings tooltipSettings,
            Vector3 position, ResourceAmount[] inputResources, ResourceAmount[] haveResources)
        {
            var resourcesNotEnough = ResourcesNotEnough(inputResources, haveResources);

            // Не хватает — ничего. Такое приходит, когда объекту для действия не нужно ни ресурсов,
            // ни рабочих (например, фабрика на максимальном уровне), а отказ всё равно объявлен:
            // ShowResourceNotEnough — общий ответ на несколько разных сбоев, он не различает причины.
            // Показывать в этом случае нечего: подсказка вышла бы с заголовком «не хватает ресурсов»
            // и пустым списком, то есть утверждала бы неправду. Молчим — ровно как молчали бы,
            // если бы отказ вообще не дошёл до тултипов.
            if (resourcesNotEnough.Count == 0)
            {
                return;
            }

            if (_notEnoughResourcesCard != null)
            {
                // haveResources пустой намеренно: показываем НЕДОСТАЮЩЕЕ, поэтому каждая ячейка
                // должна быть красной. BuildGroup при пустом «есть» ровно это и даёт.
                ShowSecondaryCard(_notEnoughResourcesCard,
                    BuildSecondaryCardData("game_hnt_no_resources",
                        BuildGroup(resourcesNotEnough, Array.Empty<ResourceAmount>())),
                    tooltipSettings.ResourcesTooltipOffset, position);

                return;
            }

            ClearSecondaryTooltip();

            SetResourceViews(resourcesNotEnough, _activeSecondaryInputResources,
                Array.Empty<ResourceAmount>());

            _resourceNotEnoughView?.SetTooltipData(null, _activeSecondaryInputResources, null);

            ShowSecondaryTooltip(_resourceNotEnoughView, "game_hnt_no_resources",
                tooltipSettings.ResourcesTooltipOffset, position);
        }

        public void ShowNoPathTooltip(TooltipSettings tooltipSettings, Vector3 position)
        {
            if (_messageCard != null)
            {
                ShowSecondaryCard(_messageCard, BuildSecondaryCardData("game_hnt_no_way"),
                    tooltipSettings.PathTooltipOffset, position);

                return;
            }

            ShowSecondaryTooltip(_noPathView, "game_hnt_no_way",
                tooltipSettings.PathTooltipOffset, position);
        }

        public void ShowCantReachAnotherObjectTooltip(GameplayTagSO tooltipSettings, Vector3 position)
        {
            //TODO: Добавить offset
            if (_messageCard != null)
            {
                ShowSecondaryCard(_messageCard, BuildSecondaryCardData(tooltipSettings.TagNameLocalizeKey),
                    Vector2.zero, position);

                return;
            }

            ShowSecondaryTooltip(_cantReachAnotherObjectView, tooltipSettings.TagNameLocalizeKey,
                Vector2.zero, position);
        }

        private void ShowPrimaryTooltip(IPrimaryTooltipView tooltipView, IReactToActions target,
            TooltipSettings tooltipSettings, ResourceAmount[] inputResources,
            ResourceAmount[] outputResources, Vector3 position,
            ResourceAmount[] haveResources, bool useOutput)
        {
            // Префаб не задан в настройках — проект этот вид тултипа не использует
            // (в LA8 так с обменным). Сюда мы попадаем, только если объект его всё-таки просит,
            // то есть это рассогласование данных: молчать нельзя, иначе тултип пропадёт без следа.
            if (tooltipView == null)
            {
                Log.Gameplay.Error($"Объект '{GetNameKey(tooltipSettings)}' просит тултип, префаб которого "
                                   + "не задан в Gameplay Settings. Проверь Tooltip Prefab / Exchange Tooltip Prefab.");

                return;
            }

            // Служебную подсказку здесь НЕ гасим и не проверяем: до сюда дело не доходит, пока
            // она на экране — отсечка стоит выше, в ShowTooltip.
            //
            // Раньше проверка была здесь и была мёртвой: сравнение шло с позицией, которую
            // строкой выше обнулял HideSecondaryTooltip, и условие не выполнялось никогда.
            HidePrimaryTooltip();

            ClearPrimaryTooltip();

            CurrentPrimaryTooltipObject = target;

            SetResourceViews(inputResources, _activeInputResources, haveResources);
            SetResourceViews(outputResources, _activeOutputResources, isOutput: useOutput);

            tooltipView.SetTooltipData(
                tooltipSettings,
                _activeInputResources,
                _activeOutputResources);

            SetLocalizedData(tooltipView, tooltipSettings.TooltipData);

            SetTooltipRect(tooltipView, tooltipSettings.BaseTooltipOffset, position);

        }

        private void ShowSecondaryTooltip(ISecondaryTooltipView tooltipView,
            string tooltipText, Vector2 offset, Vector3 position)
        {
            // Вьюху не загрузили: у неё нет ни карточки, ни префаба в настройках.
            // Одна проверка здесь закрывает все три старые подсказки разом.
            if (tooltipView == null)
            {
                Log.Gameplay.Error($"Тултип '{tooltipText}': не задан ни шаблон карточки, "
                                   + "ни префаб в Gameplay Settings. Подсказка не покажется.");

                return;
            }

            // Гасим предыдущую служебную подсказку (заодно снимая её таймер возврата),
            // потом основной тултип: подсказка ЗАМЕНЯЕТ его на своё время.
            HideSecondaryTooltip();
            HidePrimaryTooltip();

            tooltipView.SetTooltipText(tooltipText, null);
            SetTooltipRect(tooltipView, offset, position);
            tooltipView.Show();

            // Длительность берём с самой вьюхи: она же гасит себя по этому Duration изнутри.
            ScheduleSecondaryHide(tooltipView.Duration);
        }

        /// <summary>
        /// Служебная подсказка на экране. Пока это так, она владеет местом тултипа и тултип
        /// не рисуется: цель наведения запомнена, и когда подсказка отыграет, тултип встанет
        /// обратно (см. HideSecondaryAndRestore).
        ///
        /// Этим же закрыт стык двух таймингов: клик приходит мгновенно, а тултип ховера — через
        /// HoverDelay. Без проверки «нет ресурсов» показалось бы на сотню миллисекунд и было бы
        /// сбито подоспевшим тултипом, не дав себя прочитать.
        /// </summary>
        private bool SecondaryTooltipActive()
        {
            return (_cantReachAnotherObjectView != null && _cantReachAnotherObjectView.gameObject.activeSelf)
                || (_noPathView != null && _noPathView.gameObject.activeSelf)
                || (_resourceNotEnoughView != null && _resourceNotEnoughView.gameObject.activeSelf)
                || (_activeSecondaryCard != null && IsViewVisible(_activeSecondaryCard));
        }

        private void SetLocalizedData(ITooltipView tooltipView, TooltipData currentData)
        {
            tooltipView.SetTooltipText(
                LocalizationService.Instance.GetText(currentData.TooltipObjectName),
                LocalizationService.Instance.GetText(currentData.TooltipObjectDescription));
        }

        private List<ResourceAmount> ResourcesNotEnough(ResourceAmount[] inputResources,
            ResourceAmount[] haveResources)
        {
            var resourcesNotEnough = new List<ResourceAmount>();

            foreach (var resourceAmount in inputResources)
            {
                var resource = haveResources.FirstOrDefault(res =>
                    res.Resource.name == resourceAmount.Resource.name);

                if (resource == null)
                {
                    resourcesNotEnough.Add(resourceAmount);
                }
                else if (resource.Amount < resourceAmount.Amount)
                {
                    var res = new ResourceAmount(resource.Resource, resourceAmount.Amount - resource.Amount);
                    resourcesNotEnough.Add(res);
                }
            }

            return resourcesNotEnough;
        }

        private void SetTooltipRect(ITooltipView tooltipView, Vector2 offset, Vector3 position)
        {
            var calculatedPosition = UIHelper.ConvertWorldToLocalCanvasPosition(position,
                _gameplaySceneReferences.MainCamera, _gameplaySceneReferences.MainCanvas, Vector2.up,
                offset, tooltipView.Size.x, tooltipView.Size.y);

            tooltipView.SetPosition(calculatedPosition);

            tooltipView.ForceUpdate();

            tooltipView.CurrentGameObject.SetActive(true);
        }

        private void ClearPrimaryTooltip()
        {
            ClearResources(_activeInputResources);
            ClearResources(_activeOutputResources);
        }

        private void ClearSecondaryTooltip()
        {
            ClearResources(_activeSecondaryInputResources);
        }

        private void SetResourceViews(IReadOnlyList<ResourceAmount> resourceAmounts,
            IList<TooltipResourceView> resourceList,
            ResourceAmount[] haveResources = null,
            bool isOutput = false)
        {
            foreach (var resourceAmount in resourceAmounts)
            {
                var resourceEnough = true;
                
                if (haveResources != null)
                {
                    
                    var resource = haveResources.FirstOrDefault(res =>
                        res.Resource.name == resourceAmount.Resource.name);
                    resourceEnough = resource != null && resource.Amount >= resourceAmount.Amount;
                }

                var resourceView = GetResourceView();

                // Пул не смог создать ячейку (нет префаба) — ошибку уже залогировали, дальше нечего делать.
                if (resourceView == null)
                {
                    return;
                }

                resourceView.SetData(resourceAmount, resourceEnough, isOutput);

                resourceView.gameObject.SetActive(true)
;
                resourceView.transform.SetAsLastSibling();

                resourceList.Add(resourceView);
            }
        }

        /// <summary>
        /// Ячейка старых тултипов. Родитель — обычный тултип, но его может не быть
        /// (префаб не задан), поэтому запасной вариант — сам канвас тултипов.
        /// </summary>
        private TooltipResourceView CreateTooltipResourceView()
        {
            if (_tooltipResourceView == null)
            {
                Log.Gameplay.Error("Не задан Tooltip Resource Prefab, а старый тултип просит ячейку. "
                                   + "Ресурсы в нём не отрисуются.");

                return null;
            }

            var parent = _tooltipView != null ? _tooltipView.transform : _tooltipCanvas.transform;

            var instance = _resolver.Instantiate(_tooltipResourceView, parent);

            instance.gameObject.SetActive(false);

            return instance;
        }

        private TooltipResourceView GetResourceView()
        {
            var resourceView = _resourceViews.Get();

            return resourceView;
        }

        private void ClearResources(List<TooltipResourceView> tooltipResourceViews)
        {
            foreach (var resourceView in tooltipResourceViews)
            {
                ReleaseResourceView(resourceView);
            }

            tooltipResourceViews.Clear();
        }

        private void ReleaseResourceView(TooltipResourceView tooltipResourceView)
        {
            tooltipResourceView.gameObject.SetActive(false);

            _resourceViews.Release(tooltipResourceView);
        }

        public void HidePrimaryTooltip()
        {
            HidePrimaryViews(null);
        }

        private void HidePrimaryViews(ITooltipView keep)
        {
            // Идём по списку, а не по двум именованным полям: иначе новая карточка
            // никогда бы не пряталась — старый код перечислял только _tooltipView и _exchangeTooltipView.
            foreach (var view in _primaryViews)
            {
                if (ReferenceEquals(view, keep))
                {
                    continue;
                }

                HideView(view);
            }

            CurrentPrimaryTooltipObject = null;
        }

        private void HideView(ITooltipView view)
        {
            if (!view.CurrentGameObject.activeSelf)
            {
                return;
            }

            if (_animators.TryGetValue(view, out var animator) && animator.PlayHide())
            {
                return;
            }

            view.CurrentGameObject.SetActive(false);
        }

        private void HideViewInstantly(ITooltipView view)
        {
            if (_animators.TryGetValue(view, out var animator))
            {
                animator.Cancel();
            }

            view.CurrentGameObject.SetActive(false);
        }

        private bool IsViewVisible(ITooltipView view)
        {
            return view.CurrentGameObject.activeSelf
                   && !(_animators.TryGetValue(view, out var animator) && animator.IsHiding);
        }

        private TooltipCardAnimator GetAnimator(ITooltipView view)
        {
            return _animators.TryGetValue(view, out var animator) ? animator : null;
        }

        public void HideSecondaryTooltip()
        {
            // Первым делом снимаем таймер возврата: подсказку убирают досрочно, значит и тултип
            // по её расписанию возвращать не надо.
            CancelSecondaryHide();

            // Любой из старых префабов может быть не загружен — его подсказку показывает карточка.
            _noPathView?.CurrentGameObject.SetActive(false);
            _cantReachAnotherObjectView?.CurrentGameObject.SetActive(false);
            _resourceNotEnoughView?.CurrentGameObject.SetActive(false);
            HideSecondaryCard();
        }

        private void HideEverythingInstantly()
        {
            CancelSecondaryHide();

            foreach (var view in _primaryViews)
            {
                HideViewInstantly(view);
            }

            CurrentPrimaryTooltipObject = null;

            _noPathView?.CurrentGameObject.SetActive(false);
            _cantReachAnotherObjectView?.CurrentGameObject.SetActive(false);
            _resourceNotEnoughView?.CurrentGameObject.SetActive(false);

            if (_activeSecondaryCard == null)
            {
                return;
            }

            HideViewInstantly(_activeSecondaryCard);

            _activeSecondaryCard = null;
        }

        private void CancelAnimations()
        {
            foreach (var animator in _animators.Values)
            {
                if (animator == null)
                {
                    continue;
                }

                animator.Cancel();
            }
        }

        /// <summary>
        /// Отписка перед подпиской — чтобы повторный вызов (перезагрузка уровня на тех же
        /// инстансах) не сложил обработчики в два экземпляра. Делегаты именованные, а не лямбды,
        /// именно ради этого: прежний `OnEndInteract += _ => HidePrimaryTooltip()` отписать
        /// было невозможно в принципе.
        /// </summary>
        public void InitTooltip(ObjectView.ObjectView objectView)
        {
            objectView.OnShowTooltip -= ShowTooltip;
            objectView.OnShowTooltip += ShowTooltip;

            objectView.OnHideTooltip -= HandleHideTooltip;
            objectView.OnHideTooltip += HandleHideTooltip;

            // Раньше здесь висел `OnEndInteract += _ => HidePrimaryTooltip()`, и он гасил тултип
            // ЛЮБОГО объекта, когда взаимодействие заканчивал какой угодно другой. Теперь объект
            // сообщает о смене состояния, а тултип обновляется — и только если наведён именно он.
            objectView.OnInteractionStateChanged -= HandleObjectStateChanged;
            objectView.OnInteractionStateChanged += HandleObjectStateChanged;
        }

        public void InitTooltip(ComplexObject coc)
        {
            coc.OnShowTooltip -= ShowTooltip;
            coc.OnShowTooltip += ShowTooltip;

            coc.OnHideTooltip -= HandleHideTooltip;
            coc.OnHideTooltip += HandleHideTooltip;

            coc.OnInteractionStateChanged -= HandleCocStateChanged;
            coc.OnInteractionStateChanged += HandleCocStateChanged;
        }

        /// <summary>
        /// Одна подписка на контроллер вместо пары на каждый объект уровня.
        /// Слушаем все три вида изменений: ресурс, впервые появившийся на уровне, приходит
        /// через Add (GameResourcesSystem.CheckResourceIsLoaded), и на одном лишь Replace
        /// тултип такого изменения не замечал.
        /// </summary>
        private void SubscribeOnResourcesChanged(IReadOnlyObservableDictionary<ResourceBaseSO, int> resources)
        {
            resources.ObserveReplace().Subscribe(_ => RefreshCurrentTooltip())
                .AddTo(_resourceSubscriptions);

            resources.ObserveAdd().Subscribe(_ => RefreshCurrentTooltip())
                .AddTo(_resourceSubscriptions);

            resources.ObserveRemove().Subscribe(_ => RefreshCurrentTooltip())
                .AddTo(_resourceSubscriptions);
        }

        private void OnBasementUpdated(StaticObjectView baseView,
            IReadOnlyDictionary<StaticObjectView, List<MovableObjectView>> basements)
        {
            RefreshCurrentTooltip();
        }

        private void OnUnitCountChanged(StaticObjectView baseView, GameplayTagSO[] tags, int count)
        {
            RefreshCurrentTooltip();
        }

        /// <summary>
        /// Курсор ушёл с объекта. Сверяем, с ТОГО ЛИ: событие приходит и от соседей
        /// (input дёргает OnSecondaryActionEnd у прошлого объекта при переходе на новый),
        /// и без сверки чужой уход гасил бы наш тултип.
        /// </summary>
        private void HandleHideTooltip(ObjectView.ObjectView objectView) => ClearHover(objectView);

        private void HandleHideTooltip(ComplexObject coc) => ClearHover(coc);

        private void HandleObjectStateChanged(ObjectView.ObjectView objectView) => RefreshIfHovered(objectView);

        private void HandleCocStateChanged(ComplexObject coc) => RefreshIfHovered(coc);

        /// <summary>
        /// Открылась панель выбора альтернативы — убираем нарисованный тултип.
        ///
        /// <see cref="_hoverTarget"/> при этом НЕ трогаем: курсор с объекта не уходил, и когда
        /// панель закроется, подсказка должна вернуться штатным путём (RefreshCurrentTooltip).
        /// Держать её скрытой всё время показа панели — задача <see cref="IsAlternativeSelectionOpen"/>.
        /// </summary>
        private void HideForAlternativeSelection()
        {
            HidePrimaryTooltip();
        }

        /// <summary>
        /// Панель выбора альтернативы на экране.
        ///
        /// Пока она висит, тултип не показываем вообще: панель занимает то же место над объектом,
        /// а её полноэкранный BackgroundBlocker делает IsPointerOverUI истинной в любой точке —
        /// поэтому наведение на саму панель приходит в input как «курсор на UI», а не как уход
        /// с объекта, и подсказка иначе всплывала бы поверх кнопок выбора.
        ///
        /// Ссылку читаем каждый раз, а не кешируем: панель живёт в сцене Gameplay и переживает
        /// перезагрузку уровня, но ссылка на неё в GameplaySceneReferences может быть и не задана
        /// (проект без альтернатив) — тогда проверка просто всегда false.
        /// </summary>
        private bool IsAlternativeSelectionOpen()
        {
            var selectionView = _gameplaySceneReferences.ObjectAlternativeSelectionView;

            return selectionView != null && selectionView.IsShown;
        }

        private void ClearHover(IReactToActions target)
        {
            if (!ReferenceEquals(_hoverTarget, target))
            {
                return;
            }

            _hoverTarget = null;

            HidePrimaryTooltip();
        }

        private void RefreshIfHovered(IReactToActions target)
        {
            if (!ReferenceEquals(_hoverTarget, target))
            {
                return;
            }

            RefreshCurrentTooltip();
        }

        /// <summary>
        /// Пересобрать тултип объекта, который сейчас под курсором. Единственная точка
        /// обновления: сюда сходятся изменения ресурсов, состава баз и состояния объектов.
        ///
        /// Ходим от _hoverTarget, а НЕ от CurrentPrimaryTooltipObject: второй обнуляется любым
        /// скрытием, и тултип, спрятанный по HideWhenNothingToDo или перебитый чем-то ещё,
        /// уже никогда бы не вернулся — хотя курсор всё это время стоит на объекте.
        /// </summary>
        private void RefreshCurrentTooltip()
        {
            switch (_hoverTarget)
            {
                case null:
                    return;

                case ObjectView.ObjectView objectView:
                    // Не только уничтожен, но и выключен: объект, доигравший финальное
                    // взаимодействие, уходит в SetActive(false) прямо под курсором. Раньше это
                    // не было видно — тултип и так гасился по OnEndInteract. Теперь он гаснуть
                    // перестал, и без этой проверки подсказка висела бы над пустым местом
                    // до следующего движения мыши.
                    if (objectView == null || !objectView.gameObject.activeInHierarchy)
                    {
                        DropDeadHoverTarget();

                        return;
                    }

                    ShowTooltip(objectView);

                    return;

                case ComplexObject coc:
                    if (coc == null || !coc.gameObject.activeInHierarchy)
                    {
                        DropDeadHoverTarget();

                        return;
                    }

                    ShowTooltip(coc);

                    return;
            }
        }

        /// <summary>
        /// Объект занят: по нему оформлена задача и за неё уже списаны ресурсы, либо
        /// взаимодействие прямо сейчас идёт. Тултип на это время не показываем.
        ///
        /// Три признака, а не один:
        /// - isRegisteringTask — регистрация ещё в полёте (поиск пути асинхронный); ресурсы
        ///   в этот момент уже могли уйти, а CurrentTask ещё не проставлен;
        /// - InteractionPending — задача оформлена, взаимодействие не закончилось;
        /// - interactionStarted — работа идёт (объекты, которым юниты не нужны, обходятся
        ///   вообще без задачи и до InteractionPending не доходят).
        ///
        /// CurrentTask в паре с InteractionPending — страховка: если задачу снял кто-то,
        /// мимо кого флаг не проходит, объект перестанет считаться занятым сам.
        /// Сверка с MainObjectView обязательна: CurrentTask проставляется ВСЕМ объектам цепочки,
        /// включая промежуточные точки, а им ничего не платили и их тултип не врёт.
        /// </summary>
        private static bool IsBusy(ObjectView.ObjectView objectView)
        {
            if (objectView.isRegisteringTask != 0 || objectView.interactionStarted)
            {
                return true;
            }

            return objectView.InteractionPending
                   && objectView.CurrentTask != null
                   && ReferenceEquals(objectView.CurrentTask.MainObjectView, objectView);
        }

        private static bool IsBusy(ComplexObject coc)
        {
            if (coc.buildingStarted || coc.transitionInProgress)
            {
                return true;
            }

            return coc.InteractionPending
                   && coc.CurrentTask != null
                   && ReferenceEquals(coc.CurrentTask.MainObjectView, coc);
        }

        /// <summary>
        /// Объект под курсором уничтожили, а событие ухода курсора прийти уже не могло:
        /// вместе с объектом умерли и его события.
        /// </summary>
        private void DropDeadHoverTarget()
        {
            _hoverTarget = null;

            HidePrimaryTooltip();
        }

        /// <summary>
        /// Наведение на объект. Только здесь запоминается цель наведения — COC рисует свой
        /// тултип через ShowObjectTooltip и остаётся целью сам.
        /// </summary>
        public void ShowTooltip(ObjectView.ObjectView objectView)
        {
            if (!objectView.showTooltip)
            {
                return;
            }

            // Запоминаем ДО всех ранних выходов: даже если показывать сейчас нечего
            // (объект занят, или ветка HideWhenNothingToDo ниже), курсор всё ещё на объекте —
            // и когда данные изменятся, RefreshCurrentTooltip обязан знать, кого пересобирать.
            _hoverTarget = objectView;

            // Цель наведения запомнена выше, а рисовать нечего: панель выбора альтернативы
            // открыта. Отсекаем здесь, а не в ShowObjectTooltip, — у COC своя развилка,
            // которая до ShowObjectTooltip доходит не всегда.
            if (IsAlternativeSelectionOpen())
            {
                return;
            }

            ShowObjectTooltip(objectView);
        }

        /// <summary>
        /// Отрисовка тултипа объекта БЕЗ смены цели наведения.
        ///
        /// Отдельно от публичного ShowTooltip ради COC: у фабрики без шаблона карточки тултип
        /// рисуется по её текущей стадии, но наведён игрок на сам COC — это его коллайдер под
        /// курсором и его HideTooltip придёт при уходе. Если бы целью стала стадия, уход курсора
        /// с COC не совпал бы с целью, тултип не погас бы вовсе, а смена стадии выключала бы
        /// объект-цель и обрывала наведение.
        /// </summary>
        private void ShowObjectTooltip(ObjectView.ObjectView objectView)
        {
            if (!objectView.showTooltip)
            {
                return;
            }

            // Служебная подсказка на экране — она владеет местом тултипа на своё время.
            if (SecondaryTooltipActive())
            {
                return;
            }

            // Объект занят: задача по нему уже оформлена и оплачена. Прячем на всё время
            // работы — и вернём, когда взаимодействие закончится (уже с новыми данными:
            // следующая стадия, оставшиеся альтернативы).
            if (IsBusy(objectView))
            {
                HidePrimaryTooltip();

                return;
            }

            // Делать больше нечего — показывать тоже нечего: все варианты исчерпаны, тултип
            // выродился бы в пустую карточку с одной шапкой.
            //
            // Признак сознательно НЕ CanInteract: StealService на время кражи ставит его в false
            // и потом возвращает, так что по нему тултип моргал бы на ровном месте.
            // Прячем явно: тултип мог остаться на экране с прошлого показа этого же объекта
            // (подписка на изменение ресурсов пересобирает его, пока курсор на месте).
            if (objectView.ObjectDataSO.TooltipSettings is { HideWhenNothingToDo: true }
                && !objectView.HasAnyAvailableAlternatives())
            {
                HidePrimaryTooltip();

                return;
            }

            // Новая (LA8) карточка — только если объекту явно назначен шаблон.
            // Шаблон не задан => ниже дословно старый код, поведение прежних тултипов не меняется.
            if (TryGetCard(objectView.ObjectDataSO.TooltipSettings, out var card))
            {
                ShowCard(card, objectView, objectView.ObjectDataSO.TooltipSettings,
                    BuildObjectCardData(objectView, objectView.ObjectDataSO.TooltipSettings),
                    objectView.transform.position + objectView.localBaseTooltipOffset);

                return;
            }

            if (!objectView.ObjectDataSO.TooltipSettings.IsExchangeTooltip)
            {
                ShowPrimaryTooltipWithAlternatives(_tooltipView, objectView,
                    objectView.ObjectDataSO.TooltipSettings,
                    GetInputResourcesNeedAlternatives(objectView),
                    GetOutputResources(objectView),
                    objectView.transform.position + objectView.localBaseTooltipOffset,
                    GetInputResourcesHave(objectView), true);
            }
            else
            {
                ShowPrimaryTooltipWithAlternatives(_exchangeTooltipView, objectView,
                    objectView.ObjectDataSO.TooltipSettings,
                    GetInputResourcesNeedAlternatives(objectView),
                    GetOutputResources(objectView),
                    objectView.transform.position + objectView.localBaseTooltipOffset,
                    GetInputResourcesHave(objectView), false);
            }
        }

        public void EnableDisableTooltip(ObjectView.ObjectView objectView, bool state)
        {
            objectView.showTooltip = state;
        }

        /// <summary>
        /// Get an array of object interaction output resources
        /// </summary> 
        /// <returns>Array of object interaction output resources.</returns>
        public virtual ResourceAmount[] GetOutputResources(ObjectView.ObjectView objectView)
        {
            var outputResources = objectView is StaticObjectView { CanProduceObjects: true } staticObject
                ? staticObject.productionData.UseObjectSpawn
                    ? staticObject.productionData.SpawnedSpitObject != null
                        ? staticObject.productionData.SpawnedSpitObject.ObjectDataSO.OutputResources
                        : staticObject.productionData.SpawnedCollectibleObject.ObjectDataSO.OutputResources
                    : objectView.ObjectDataSO.OutputResources
                : objectView.ObjectDataSO.OutputResources;

            return outputResources;
        }

        /// <summary>
        /// Get array of resources needed for interact with object (include units)
        /// </summary>
        /// <returns>Array of resources needed for interact with object.</returns>
        private ResourceAmount[] GetInputResourcesNeed(ObjectView.ObjectView objectView)
        {
            var result = new List<ResourceAmount>();

            if (objectView.ActiveAlternativeIndex == -1 && objectView.ObjectDataSO.AlternativeInteractions.Count > 0)
            {
                foreach (var alt in objectView.ObjectDataSO.AlternativeInteractions)
                {
                    foreach (var typeCount in alt.UnitTypeCount)
                    {
                        var workerTags = typeCount.unitType.Where(tag => tag.TagType == TagType.Unit).ToList();
                        var workerInfo = _gameResourcesSystem.workerResource.FirstOrDefault(pair => workerTags.Contains(pair.tag) || workerTags.Count == 0);
                        if (workerInfo.worker != null) result.Add(new ResourceAmount(workerInfo.worker, typeCount.count));
                    }
            
                    if (alt.InputResources != null) result.AddRange(alt.InputResources);
                }
            }
            else
            {
                foreach (var typeCount in objectView.GetCurrentUnitTypeCount())
                {
                    var workerTags = typeCount.unitType.Where(tag => tag.TagType == TagType.Unit).ToList();
                    var workerInfo = _gameResourcesSystem.workerResource.FirstOrDefault(pair => workerTags.Contains(pair.tag) || workerTags.Count == 0);
                    if (workerInfo.worker != null) result.Add(new ResourceAmount(workerInfo.worker, typeCount.count));
                }
                if (objectView.GetCurrentInputResources() != null) result.AddRange(objectView.GetCurrentInputResources());
            }

            return result.ToArray();


        }

        /// <summary>
        /// Get array of current resources (include units)
        /// </summary>
        /// <returns>Array of current resources.</returns>
        private ResourceAmount[] GetInputResourcesHave(ObjectView.ObjectView objectView)
        {
            var workersResources = new List<ResourceAmount>();

            IEnumerable<UnitTypeCount> unitCounts;
            if (objectView.ActiveAlternativeIndex == -1 && objectView.ObjectDataSO.AlternativeInteractions.Count > 0)
            {
                unitCounts = objectView.ObjectDataSO.AlternativeInteractions.SelectMany(a => a.UnitTypeCount);
            }
            else
            {
                unitCounts = objectView.GetCurrentUnitTypeCount();
            }

            foreach (var typeCount in unitCounts)
            {
                var workerTags = typeCount.unitType.Where(tag => tag.TagType == TagType.Unit).ToList();
                var workerInfo = _gameResourcesSystem.workerResource.FirstOrDefault(pair => workerTags.Contains(pair.tag) || workerTags.Count == 0);
        
                var amount = _unitBaseController.GetRelevantBases(typeCount.unitType, typeCount.tagMode).relevantUnitsCount;
                if (workerInfo.worker != null) workersResources.Add(new ResourceAmount(workerInfo.worker, amount));
            }

            workersResources.AddRange(_gameResourcesSystem.Resources.Keys
                .Select(resource => new ResourceAmount(resource, _gameResourcesSystem.Resources[resource])));

            workersResources.AddRange(_gameResourcesSystem.InventoryResources.Keys
                .Select(resource => new ResourceAmount(resource, _gameResourcesSystem.InventoryResources[resource])));

            return workersResources.ToArray();
        }

        public void ShowResourceNotEnough(ObjectView.ObjectView objectView)
        {
            if (!objectView.showTooltip)
                return;

            ShowResourceNotEnoughTooltip(
                objectView.ObjectDataSO.TooltipSettings,
                objectView.transform.position + objectView.localResourcesTooltipOffset,
                GetInputResourcesNeed(objectView),
                GetInputResourcesHave(objectView));
        }

        public void ShowNoPath(ObjectView.ObjectView objectView)
        {
            if (!objectView.showTooltip)
                return;

            ShowNoPathTooltip(
                objectView.ObjectDataSO.TooltipSettings, 
                objectView.transform.position + objectView.localPathTooltipOffset);
        }
        

        public ResourceAmount[] GetOutputResources(StaticObjectView objectView)
        {
            var outputResources = objectView.CanProduceObjects
                ? objectView.productionData.UseObjectSpawn
                    ? objectView.productionData.SpawnedSpitObject != null
                        ? objectView.productionData.SpawnedSpitObject.ObjectDataSO.OutputResources
                        : objectView.productionData.SpawnedCollectibleObject.ObjectDataSO.OutputResources
                    : objectView.ObjectDataSO.OutputResources
                : objectView.ObjectDataSO.OutputResources;

            if (objectView.IsBase && objectView.BaseData.UnitResource != null)
            {
                var outputResourcesList = outputResources.ToList();

                var workerResource = new ResourceAmount(objectView.BaseData.UnitResource, 
                    objectView.BaseData.MaxUnitCount);

                outputResourcesList.Add(workerResource);

                return outputResourcesList.ToArray();
            }

            return outputResources;

        }
        
        public void ShowTooltip(ComplexObject coc)
        {
            _hoverTarget = coc;

            // Панель выбора альтернативы открыта — место над объектом занято ею.
            if (IsAlternativeSelectionOpen())
            {
                return;
            }

            // Служебная подсказка на экране — она владеет местом тултипа на своё время.
            if (SecondaryTooltipActive())
            {
                return;
            }

            // Стройка идёт — прячем. Иначе тултип показал бы стоимость стадии 2 красным,
            // хотя стадия 2 в этот момент и строится.
            if (IsBusy(coc))
            {
                HidePrimaryTooltip();

                return;
            }

            // Проверяем шаблон ДО старой развилки. Иначе фабрика ушла бы в ShowTooltip(CurrentObjectView)
            // и нарисовалась бы обычной карточкой без уровней — старый код это дефолтный путь для COC.
            //
            // Настройки берём с ТОГО ЖЕ вью, что и данные уровней (GetStageView), а не со свойства
            // coc.CurrentObjectView: пока лежит выпавший продукт, свойство возвращает его, шаблон
            // не нашёлся бы и фабрика молча уехала бы на старый путь.
            var stageView = GetStageView(coc);

            var cocSettings = GetCocSettings(coc, stageView);

            if (TryGetCard(cocSettings, out var cocCard))
            {
                ShowCard(cocCard, coc, cocSettings, BuildCocCardData(coc, cocSettings), coc.transform.position);

                return;
            }

            if (coc.CurrentObjectView.ObjectDataSO.ShowTooltip)
            {
                // ShowObjectTooltip, а не ShowTooltip: целью наведения остаётся COC, а не его стадия.
                if (coc.CurrentObjectView.ObjectDataSO.CanInteract && !coc.UseBuildFirst)
                {
                    ShowObjectTooltip(coc.CurrentObjectView);
                }
                else
                {
                    if (coc.transitionStateData[coc.currentStateIndex].TransitionTo != null)
                    {
                        ShowObjectViewBuildingTooltip(coc);
                    }
                    else
                    {
                        ShowObjectTooltip(coc.CurrentObjectView);
                    }
                }
            }
            else
            {
                if (coc.transitionStateData[coc.currentStateIndex].TransitionTo == null) 
                {
                    return;
                }
                
                ShowTooltip(coc, coc.TooltipSettings,
                        GetInputResourcesNeed(coc),
                        GetOutputResources(coc),
                        coc.transform.position,
                        GetInputResourcesHave(coc));
            }
        }

        private void ShowObjectViewBuildingTooltip(ComplexObject coc) 
        {
            if (coc.CurrentObjectView.ObjectDataSO.TooltipSettings.IsExchangeTooltip) 
            {
                ShowExchangeTooltip(coc, coc.CurrentObjectView.ObjectDataSO.TooltipSettings,
                    GetInputResourcesNeed(coc),
                    GetOutputResources(coc),
                    coc.transform.position,
                    GetInputResourcesHave(coc));
            } 
            else 
            {
                ShowTooltip(coc, coc.CurrentObjectView.ObjectDataSO.TooltipSettings,
                    GetInputResourcesNeed(coc),
                    GetOutputResources(coc),
                    coc.transform.position,
                    GetInputResourcesHave(coc));
            }
        }

        private ResourceAmount[] GetInputResourcesNeed(ComplexObject coc) 
        {
            var workersResources = new List<ResourceAmount>();
            
            foreach (var typeCount in coc.transitionStateData[coc.currentStateIndex].BuildingSettings.UnitTypeCounts)
            {
                var workerTags = typeCount.unitType
                    .Where(tag => tag.TagType == TagType.Unit)
                    .ToList();

                var workersResourceNeed = new ResourceAmount(_gameResourcesSystem.workerResource
                        .First(pair => workerTags.Contains(pair.tag) || workerTags.Count == 0).worker,
                    typeCount.count);

                workersResources.Add(workersResourceNeed);
            }

            var inputResources = workersResources
                .Concat(coc.GetResourceCost())
                .ToArray();

            return inputResources;
        }

        private ResourceAmount[] GetInputResourcesHave(ComplexObject coc) 
        {
            var workersResources = new List<ResourceAmount>();

            foreach (var typeCount in coc.transitionStateData[coc.currentStateIndex].BuildingSettings.UnitTypeCounts)
            {
                var workerTags = typeCount.unitType
                    .Where(tag => tag.TagType == TagType.Unit)
                    .ToList();

                var workersResourceHave = new ResourceAmount(_gameResourcesSystem.workerResource
                        .First(pair => 
                            workerTags.Contains(pair.tag) || workerTags.Count == 0).worker,
                    _unitBaseController.GetRelevantBases(typeCount.unitType, typeCount.tagMode).relevantUnitsCount);
                
                workersResources.Add(workersResourceHave);
            }

            var inputResourcesHave = workersResources;

            inputResourcesHave.AddRange(_gameResourcesSystem.Resources.Keys
                .Select(resource => new ResourceAmount(resource, _gameResourcesSystem.Resources[resource])));

            inputResourcesHave.AddRange(_gameResourcesSystem.InventoryResources.Keys
                .Select(resource => new ResourceAmount(resource, _gameResourcesSystem.InventoryResources[resource])));

            return inputResourcesHave.ToArray();
        }

        private ResourceAmount[] GetOutputResources(ComplexObject coc) 
        {
            var nextStage = coc.transitionStateData[coc.currentStateIndex].TransitionTo ?? coc.CurrentObjectView;

            var outputResources = GetOutputResources(nextStage);

            return outputResources.ToArray();
        }
        
        private List<List<ResourceAmount>> GetInputResourcesNeedAlternatives(ObjectView.ObjectView objectView)
        {
            var alternatives = new List<List<ResourceAmount>>();

            if (objectView.ActiveAlternativeIndex == -1 && objectView.ObjectDataSO.AlternativeInteractions.Count > 0)
            {
                for (var index = 0; index < objectView.ObjectDataSO.AlternativeInteractions.Count; index++)
                {
                    // Исчерпанные варианты не показываем: раскопка «дроном 1 раз» после вылета
                    // дрона предлагала его снова, хотя выбрать его уже нельзя.
                    // CanUseAlternative учитывает и лимит InteractionsAmount, и DeactivateAllOthers.
                    if (!objectView.CanUseAlternative(index))
                    {
                        continue;
                    }

                    var alt = objectView.ObjectDataSO.AlternativeInteractions[index];
                    var altList = new List<ResourceAmount>();
                    foreach (var typeCount in alt.UnitTypeCount)
                    {
                        var workerTags = typeCount.unitType.Where(tag => tag.TagType == TagType.Unit).ToList();
                        var workerInfo = _gameResourcesSystem.workerResource.FirstOrDefault(pair => workerTags.Contains(pair.tag) || workerTags.Count == 0);
                        if (workerInfo.worker != null) altList.Add(new ResourceAmount(workerInfo.worker, typeCount.count));
                    }
                    if (alt.InputResources != null) altList.AddRange(alt.InputResources);
                    
                    if (altList.Count > 0) alternatives.Add(altList);
                }
            }
            else
            {
                var altList = new List<ResourceAmount>();
                foreach (var typeCount in objectView.GetCurrentUnitTypeCount())
                {
                    var workerTags = typeCount.unitType.Where(tag => tag.TagType == TagType.Unit).ToList();
                    var workerInfo = _gameResourcesSystem.workerResource.FirstOrDefault(pair => workerTags.Contains(pair.tag) || workerTags.Count == 0);
                    if (workerInfo.worker != null) altList.Add(new ResourceAmount(workerInfo.worker, typeCount.count));
                }
                if (objectView.GetCurrentInputResources() != null) altList.AddRange(objectView.GetCurrentInputResources());
                
                if (altList.Count > 0) alternatives.Add(altList);
            }

            return alternatives;
        }

        private void ShowPrimaryTooltipWithAlternatives(IPrimaryTooltipView tooltipView, IReactToActions target,
            TooltipSettings tooltipSettings, List<List<ResourceAmount>> inputAlternatives,
            ResourceAmount[] outputResources, Vector3 position, ResourceAmount[] haveResources, bool useOutput)
        {
            // Та же проверка, что в ShowPrimaryTooltip: префаб этого вида тултипа не задан.
            if (tooltipView == null)
            {
                Log.Gameplay.Error($"Объект '{GetNameKey(tooltipSettings)}' просит тултип, префаб которого "
                                   + "не задан в Gameplay Settings. Проверь Tooltip Prefab / Exchange Tooltip Prefab.");

                return;
            }

            // Служебную подсказку не гасим — см. ShowPrimaryTooltip.
            HidePrimaryTooltip();

            ClearPrimaryTooltip();
            CurrentPrimaryTooltipObject = target;

            for (int i = 0; i < inputAlternatives.Count; i++)
            {
                SetResourceViews(inputAlternatives[i].ToArray(), _activeInputResources, haveResources);
                
                if (i < inputAlternatives.Count - 1)
                {
                    var separatorView = GetResourceView();

                    if (separatorView == null)
                    {
                        break;
                    }

                    string orText = LocalizationService.Instance.GetText("game_txt_or");

                    separatorView.SetAsSeparator(orText);
                    separatorView.gameObject.SetActive(true);
                    separatorView.transform.SetAsLastSibling();
                    _activeInputResources.Add(separatorView);
                }
            }

            SetResourceViews(outputResources, _activeOutputResources, isOutput: useOutput);

            tooltipView.SetTooltipData(tooltipSettings, _activeInputResources, _activeOutputResources);
            SetLocalizedData(tooltipView, tooltipSettings.TooltipData);
            SetTooltipRect(tooltipView, tooltipSettings.BaseTooltipOffset, position);

        }
        public void ShowResourceNotEnough(ComplexObject coc)
        {
            if (coc.CocActions[CocActionType.Interact].IsAvailable() 
                && !(coc.UseBuildFirst && coc.CocActions[CocActionType.Build].IsAvailable()))
            {
                ShowResourceNotEnough(coc.CurrentObjectView);
            }
            else
            {
                ShowResourceNotEnoughTooltip(coc.TooltipSettings,
                    coc.transform.position,
                    GetInputResourcesNeed(coc),
                    GetInputResourcesHave(coc));
            }
        }

        public void ShowNoPath(ComplexObject coc)
        {
            if (coc.CurrentObjectView.ObjectDataSO.ShowTooltip)
            {
                ShowNoPath(coc.CurrentObjectView);
            }
            else
            {
                ShowNoPathTooltip(coc.TooltipSettings, coc.CurrentObjectView.transform.position);
            }
        }

        #region Новые карточки тултипов (LA8)

        /// <summary>
        /// Предзагрузка и спавн всех шаблонов из GameplaySettings.
        /// Показ тултипа синхронный, поэтому лениво грузить префаб на первом показе нельзя.
        /// </summary>
        private async UniTask LoadCardTemplates()
        {
            var templates = _gameplaySceneReferences.GameplaySettings.TooltipTemplates;

            if (templates == null)
            {
                return;
            }

            foreach (var template in templates)
            {
                // Дубликат в списке (или два шаблона на один префаб) породил бы ВТОРОЙ инстанс
                // на тот же префаб и навсегда осиротил бы первый.
                if (template == null || (template.ViewPrefab != null
                                         && _cardViews.ContainsKey(template.ViewPrefab.AssetGUID)))
                {
                    continue;
                }

                if (template.ViewPrefab == null || !template.ViewPrefab.RuntimeKeyIsValid())
                {
                    Log.Gameplay.Error($"Шаблон тултипа '{template.name}': не назначен ViewPrefab. Пропущен.");

                    continue;
                }

                var prefab = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(template.ViewPrefab);

                if (prefab == null)
                {
                    Log.Gameplay.Error($"Шаблон тултипа '{template.name}': префаб не загрузился. Пропущен.");

                    continue;
                }

                var prefabCard = prefab.GetComponent<ITooltipCardView>();

                if (prefabCard == null)
                {
                    Log.Gameplay.Error($"Шаблон тултипа '{template.name}': на префабе '{prefab.name}' " +
                                       "нет компонента ITooltipCardView. Пропущен.");

                    continue;
                }

                var instance = SpawnTooltip<ITooltipCardView>(prefabCard);

                _cardViews[template.ViewPrefab.AssetGUID] = instance;
                _primaryViews.Add(instance);
            }
        }

        /// <summary>
        /// Инстанс служебной карточки. Отдельно от LoadCardTemplates, потому что эта карточка
        /// НЕ должна попасть в _primaryViews — иначе её гасил бы каждый тултип объекта.
        /// </summary>
        private async UniTask<ITooltipCardView> LoadSecondaryCard(TooltipTemplate template, string label)
        {
            if (template == null)
            {
                // Штатный случай: подсказка живёт на старом префабе.
                return null;
            }

            if (template.ViewPrefab == null || !template.ViewPrefab.RuntimeKeyIsValid())
            {
                Log.Gameplay.Error($"Шаблон подсказки «{label}»: не назначен ViewPrefab. "
                                   + "Показываться будет старый тултип.");

                return null;
            }

            // Префаб уже подняли под другой шаблон: инстанс ОДИН на префаб, и две подсказки
            // (или подсказка и тултип объекта) начали бы делить GameObject, гася друг друга.
            if (_cardViews.ContainsKey(template.ViewPrefab.AssetGUID))
            {
                Log.Gameplay.Error($"Шаблон подсказки «{label}» ('{template.name}') указывает на префаб, "
                                   + "который уже занят другим шаблоном. Сделай отдельный префаб — иначе "
                                   + "подсказки будут перебивать друг друга.");

                return null;
            }

            var prefab = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(template.ViewPrefab);

            var prefabCard = prefab != null ? prefab.GetComponent<ITooltipCardView>() : null;

            if (prefabCard == null)
            {
                Log.Gameplay.Error($"Шаблон подсказки «{label}» ('{template.name}'): префаб не загрузился "
                                   + "или на нём нет ITooltipCardView.");

                return null;
            }

            var card = SpawnTooltip<ITooltipCardView>(prefabCard);

            _cardViews[template.ViewPrefab.AssetGUID] = card;

            return card;
        }

        /// <summary>
        /// Показ служебной карточки. Порядок тот же, что у ShowSecondaryTooltip: гасим предыдущую
        /// служебную (сняв её таймер возврата), потом основной тултип — карточка занимает
        /// его место на своё время.
        /// </summary>
        private void ShowSecondaryCard(ITooltipCardView card, TooltipCardData data, Vector2 offset,
            Vector3 position)
        {
            HideSecondaryTooltip();
            HidePrimaryTooltip();

            card.SetCardData(data);

            SetCardRect(card, offset, position, false);

            _activeSecondaryCard = card;

            ScheduleSecondaryHide(_gameplaySceneReferences.GameplaySettings.SecondaryTooltipDuration);
        }

        /// <summary>
        /// Завести таймер: через <paramref name="duration"/> убрать служебную подсказку и вернуть
        /// основной тултип, если курсор всё ещё стоит на объекте.
        ///
        /// Таймер держим ЗДЕСЬ, а не слушаем OnHide у самих вьюх. У старых префабов отсчёт живёт
        /// внутри вьюхи (DelayHide -> ForceHide -> OnHide), а HideSecondaryTooltip гасит их через
        /// SetActive(false), НЕ отменяя этот внутренний отсчёт: он всё равно дострелит OnHide
        /// позже — и возврат случился бы в произвольный момент, возможно поверх уже показанной
        /// новой подсказки. Свой таймер снимается вместе с подсказкой и такого не допускает.
        /// </summary>
        private void ScheduleSecondaryHide(float duration)
        {
            CancelSecondaryHide();

            _secondaryHide = new CancellationTokenSource();

            HideSecondaryAndRestore(duration, _secondaryHide.Token).Forget();
        }

        private async UniTaskVoid HideSecondaryAndRestore(float duration, CancellationToken token)
        {
            var cancelled = await UniTask.WaitForSeconds(duration, cancellationToken: token)
                .SuppressCancellationThrow();

            if (cancelled)
            {
                // Подсказку убрали раньше срока: показали следующую, ушёл курсор, перезагрузка.
                // Возврат в этих случаях делать нельзя — тултип мигнул бы между двумя подсказками.
                return;
            }

            // Сначала снимаем подсказку и только потом возвращаем тултип: ShowTooltip не рисует,
            // пока служебная подсказка на экране (см. SecondaryTooltipActive).
            HideSecondaryTooltip();

            // Курсор мог уйти или объект пропасть — тогда RefreshCurrentTooltip ничего не покажет.
            // Если курсор на месте, тултип вернётся пересобранным: за эти пару секунд ресурсы
            // могли измениться.
            RefreshCurrentTooltip();
        }

        private void CancelSecondaryHide()
        {
            if (_secondaryHide == null)
            {
                return;
            }

            _secondaryHide.Cancel();
            _secondaryHide.Dispose();
            _secondaryHide = null;
        }

        private void HideSecondaryCard()
        {
            if (_activeSecondaryCard == null)
            {
                return;
            }

            HideView(_activeSecondaryCard);

            _activeSecondaryCard = null;
        }

        /// <summary>
        /// Данные служебной карточки. Шапка всегда красная (IsAffordable = false): это сообщение
        /// о том, что действие невозможно. Ресурсы нужны только «нехватке» — у остальных
        /// подсказок групп нет, и карточка сама спрячет блок входа и стрелку.
        /// </summary>
        private static TooltipCardData BuildSecondaryCardData(string textKey, TooltipGroupData group = null)
        {
            var data = new TooltipCardData
            {
                NameKey = textKey,
                IsAffordable = false,
            };

            if (group != null)
            {
                data.InputGroups.Add(group);
            }

            return data;
        }

        /// <summary>
        /// Ключ имени для шапки. Пусто — карточка спрячет шапку целиком.
        /// ShowObjectImage и ShowDescription новая карточка не использует: в макете
        /// ни иконки объекта, ни описания нет.
        /// </summary>
        private static string GetNameKey(TooltipSettings settings)
        {
            if (!settings.ShowObjectName || settings.TooltipData == null)
            {
                return null;
            }

            return settings.TooltipData.TooltipObjectName;
        }

        private static string GetNameSuffixKey(TooltipSettings settings)
        {
            if (!settings.ShowObjectName || settings.TooltipData == null)
            {
                return null;
            }

            return settings.TooltipData.TooltipObjectNameSuffix;
        }

        private bool TryGetCard(TooltipSettings settings, out ITooltipCardView card)
        {
            card = null;

            var template = settings?.Template;

            if (template == null)
            {
                // Шаблон не задан — штатный случай, объект живёт на старой системе. Молчим.
                return false;
            }

            return TryGetCard(template, out card);
        }

        private bool TryGetCard(TooltipTemplate template, out ITooltipCardView card)
        {
            card = null;

            if (template == null)
            {
                return false;
            }

            if (template.ViewPrefab != null
                && _cardViews.TryGetValue(template.ViewPrefab.AssetGUID, out card))
            {
                return true;
            }

            // Раньше здесь был молчаливый откат на старый тултип, и из-за него причину
            // ловили два билда подряд: на экране просто старый тултип, в логе ничего.
            Log.Gameplay.Error($"Тултип: шаблон '{template.name}' задан на объекте, но его карточка "
                               + "не загружена. Проверь, что шаблон лежит в Gameplay Settings → "
                               + "Tooltip Templates и что у него заполнен View Prefab.");

            return false;
        }

        private void ShowCard(ITooltipCardView card, IReactToActions target, TooltipSettings settings,
            TooltipCardData data, Vector3 worldPosition)
        {
            var isRefresh = ReferenceEquals(CurrentPrimaryTooltipObject, target) && IsViewVisible(card);

            // Гасим только предыдущую первичную вьюху (она может быть другой карточкой или старым
            // префабом). Служебную подсказку не трогаем: пока она на экране, сюда не попадают —
            // отсечка стоит в ShowTooltip.
            HidePrimaryViews(isRefresh ? card : null);

            // Строго после Hide: HidePrimaryViews обнуляет CurrentPrimaryTooltipObject.
            CurrentPrimaryTooltipObject = target;

            card.SetCardData(data);

            // SetLocalizedData сознательно НЕ вызываем: он локализует ключ повторно
            // (GetText(GetText(key))) и перезатирает шапку, которую только что поставил SetCardData.

            SetCardRect(card, settings.BaseTooltipOffset, worldPosition, isRefresh);

        }

        /// <summary>
        /// Позиционирование карточки. Порядок ОТЛИЧАЕТСЯ от SetTooltipRect и это принципиально:
        /// размер карточки даёт layout, а LayoutRebuilder на ВЫКЛЮЧЕННОМ объекте не работает.
        /// Поэтому включаем и пересчитываем до чтения Size — иначе первый показ уедет мимо.
        /// </summary>
        private void SetCardRect(ITooltipCardView card, Vector2 offset, Vector3 position, bool isRefresh)
        {
            var animator = GetAnimator(card);

            if (!isRefresh)
            {
                animator?.PrepareShow();
            }

            card.CurrentGameObject.SetActive(true);

            card.ForceUpdate();

            var calculatedPosition = UIHelper.ConvertWorldToLocalCanvasPosition(position,
                _gameplaySceneReferences.MainCamera, _gameplaySceneReferences.MainCanvas, Vector2.up,
                offset, card.Size.x, card.Size.y);

            card.SetPosition(calculatedPosition);

            if (isRefresh)
            {
                animator?.RebaseHome();
            }
            else
            {
                animator?.PlayShow();
            }
        }

        public bool ShowCardAtRect(TooltipTemplate template, TooltipCardData data, RectTransform anchor,
            Vector2 offset)
        {
            if (anchor == null || !TryGetCard(template, out var card))
            {
                return false;
            }

            var isRefresh = CurrentPrimaryTooltipObject == null && ReferenceEquals(_uiCardAnchor, anchor)
                            && IsViewVisible(card);

            HidePrimaryViews(isRefresh ? card : null);
            HideSecondaryTooltip();

            // Владельца-IReactToActions здесь нет: тултип висит на UI, а не на объекте уровня.
            // Именно null, а не что-то фиктивное: по нему снаружи видно, что показана
            // НЕ мировая цель.
            CurrentPrimaryTooltipObject = null;

            // И цель наведения тоже снимаем: карточка UI заняла место мирового тултипа, и без
            // этого первое же изменение ресурсов пересобрало бы мировой тултип поверх неё.
            _hoverTarget = null;

            _uiCardAnchor = anchor;

            card.SetCardData(data);

            SetCardRectAtUI(card, anchor, offset, isRefresh);

            return true;
        }

        /// <summary>
        /// Позиционирование карточки под UI-элементом. Ни камеры, ни WorldToScreenPoint:
        /// карточки инстанцируются в GameplayCanvasLayers[2], и UI-панели этого слоя лежат
        /// в том же пространстве — хватает пересчёта через локальные координаты канваса.
        ///
        /// Порядок тот же, что в SetCardRect, и по той же причине: размер карточки даёт layout,
        /// а он не считается на выключенном объекте.
        /// </summary>
        private void SetCardRectAtUI(ITooltipCardView card, RectTransform anchor, Vector2 offset,
            bool isRefresh)
        {
            var animator = GetAnimator(card);

            if (!isRefresh)
            {
                animator?.PrepareShow();
            }

            card.CurrentGameObject.SetActive(true);

            card.ForceUpdate();

            var canvasRect = (RectTransform)_tooltipCanvas.transform;

            // По углам, а не по anchor.position: не зависит ни от пивота элемента, ни от масштаба —
            // а масштаб слота крутит DOTween-анимация появления, и в этот момент он не единичный.
            anchor.GetWorldCorners(_anchorCorners);

            var bottomLeft = (Vector2)canvasRect.InverseTransformPoint(_anchorCorners[0]);
            var topRight = (Vector2)canvasRect.InverseTransformPoint(_anchorCorners[2]);

            var size = card.Size;

            var position = new Vector2(
                (bottomLeft.x + topRight.x) * 0.5f + offset.x,
                bottomLeft.y - offset.y - size.y * 0.5f);

            // Панель особых предметов прижата к правому верхнему углу, а карточка заметно шире
            // слота — без ограничения она уезжает за край экрана.
            var limit = (canvasRect.rect.size - size) * 0.5f;

            if (limit.x > 0f)
            {
                position.x = Mathf.Clamp(position.x, -limit.x, limit.x);
            }

            if (limit.y > 0f)
            {
                position.y = Mathf.Clamp(position.y, -limit.y, limit.y);
            }

            card.SetPosition(position);

            if (isRefresh)
            {
                animator?.RebaseHome();
            }
            else
            {
                animator?.PlayShow();
            }
        }

        private TooltipCardData BuildObjectCardData(ObjectView.ObjectView objectView, TooltipSettings settings)
        {
            var data = new TooltipCardData
            {
                // Галка ShowObjectName управляет шапкой — она общая со старой системой,
                // и ГД справедливо ждёт, что она работает и здесь.
                NameKey = GetNameKey(settings),

                NameSuffixKey = GetNameSuffixKey(settings),

                // Превью «что даст действие» живёт на вьюхе, а не в настройках тултипа:
                // настройка нужна поштучно на каждом уровне (см. StaticObjectView).
                // У COC такого нет — там своя карточка со стадиями.
                PreviewIcon = objectView is StaticObjectView staticView
                    ? staticView.tooltipPreviewIcon
                    : null,
            };

            var have = GetInputResourcesHave(objectView);
            var anyAffordable = false;

            foreach (var alternative in GetInputResourcesNeedAlternatives(objectView))
            {
                var group = BuildGroup(alternative, have);

                if (group == null)
                {
                    continue;
                }

                anyAffordable |= group.IsAffordable;
                data.InputGroups.Add(group);
            }

            foreach (var output in GetOutputResources(objectView))
            {
                data.Output.Add(ToNeutralCell(output));
            }

            // Шапка зелёная, если хватает хотя бы на ОДИН вариант применения.
            // Входа нет вовсе (объект только выдаёт) — тоже зелёная.
            data.IsAffordable = data.InputGroups.Count == 0 || anyAffordable;

            return data;
        }

        /// <summary>
        /// Чьи настройки тултипа показывать у COC. Повторяет правило старой системы дословно:
        /// у стадии свой тултип выключен → показываем тултип, настроенный на самом COC.
        /// Так живёт непостроенная стадия (у неё ShowTooltip = 0 и нет своей TooltipData) —
        /// и так же она подхватит шаблон, положенный на COC.
        ///
        /// Без этого правила шаблон на COC не читался бы никогда: проверка шаблона стоит РАНЬШЕ
        /// старой развилки, и до ветки с coc.TooltipSettings управление просто не доходило.
        /// </summary>
        private static TooltipSettings GetCocSettings(ComplexObject coc, StaticObjectView stageView)
        {
            if (stageView == null || stageView.ObjectDataSO == null)
            {
                return coc.TooltipSettings;
            }

            return stageView.ObjectDataSO.ShowTooltip
                ? stageView.ObjectDataSO.TooltipSettings
                : coc.TooltipSettings;
        }

        /// <summary>
        /// Вью ТЕКУЩЕГО уровня фабрики. Сознательно не свойство coc.CurrentObjectView —
        /// оно, пока рядом лежит выпавший продукт, возвращает продукт, а не саму фабрику.
        /// </summary>
        private static StaticObjectView GetStageView(ComplexObject coc)
        {
            if (coc.transitionStateData != null
                && coc.currentStateIndex >= 0
                && coc.currentStateIndex < coc.transitionStateData.Length
                && coc.transitionStateData[coc.currentStateIndex].TransitionFrom != null)
            {
                return coc.transitionStateData[coc.currentStateIndex].TransitionFrom;
            }

            return coc.currentObjectView;
        }

        private TooltipCardData BuildCocCardData(ComplexObject coc, TooltipSettings settings)
        {
            var data = new TooltipCardData
            {
                NameKey = GetNameKey(settings),
                NameSuffixKey = GetNameSuffixKey(settings),
                IsAffordable = true,
            };

            if (coc.transitionStateData == null
                || coc.currentStateIndex < 0
                || coc.currentStateIndex >= coc.transitionStateData.Length)
            {
                return data;
            }

            var row = coc.transitionStateData[coc.currentStateIndex];

            var group = BuildGroup(GetInputResourcesNeed(coc), GetInputResourcesHave(coc));

            if (group != null)
            {
                data.InputGroups.Add(group);
                data.IsAffordable = group.IsAffordable;
            }

            var levelOffset = GetLevelOffset(coc);

            data.CurrentStage = BuildStage(GetStageView(coc), coc.currentStateIndex + levelOffset,
                row.TransitionTo);

            if (row.TransitionTo != null)
            {
                var nextIndex = coc.StateIndexes.TryGetValue(row.TransitionTo, out var index)
                    ? index
                    : coc.currentStateIndex + 1;

                data.NextStage = BuildStage(row.TransitionTo, nextIndex + levelOffset, null);
            }

            return data;
        }

        /// <summary>
        /// Сдвиг нумерации уровней. «Ур.0» — это непостроенное состояние: у палаток и DroneBase
        /// нулевая стадия именно такая, поэтому номер уровня совпадает с индексом состояния.
        /// А у лагеря и WorkerBase непостроенной стадии нет, нулевая — уже рабочий грейд,
        /// и ей полагается «Ур.1».
        ///
        /// Отличаем по данным, а не по имени префаба: непостроенная стадия ничего не производит
        /// и ничего не даёт.
        /// </summary>
        private int GetLevelOffset(ComplexObject coc)
        {
            if (coc.transitionStateData == null || coc.transitionStateData.Length == 0)
            {
                return 1;
            }

            return IsUnbuiltStage(coc.transitionStateData[0].TransitionFrom) ? 0 : 1;
        }

        private bool IsUnbuiltStage(StaticObjectView view)
        {
            if (view == null)
            {
                return false;
            }

            // Порядок важен: GetOutputResources лезет в productionData, поэтому спрашиваем её
            // только после того, как CanProduceObjects отсёк производящие стадии.
            return !view.CanProduceObjects && GetOutputResources(view).Length == 0;
        }

        /// <summary>
        /// Блок одного уровня. iconFallback — стадия, у которой брать иконку, если эта стадия
        /// не даёт ничего (непостроенная): рисуем ту же иконку с нулём, чтобы читалось
        /// «сейчас ноль этого ресурса → станет столько-то», а не пустой слот.
        /// </summary>
        private TooltipStageData BuildStage(StaticObjectView view, int displayLevel,
            StaticObjectView iconFallback)
        {
            if (view == null)
            {
                return null;
            }

            var stage = new TooltipStageData
            {
                DisplayLevel = displayLevel,
                Timer = new TooltipCellData(null, FormatProductionTime(view), TooltipCellState.Neutral),
            };

            foreach (var output in GetOutputResources(view))
            {
                stage.Output.Add(ToNeutralCell(output));
            }

            if (stage.Output.Count == 0 && iconFallback != null)
            {
                var next = GetOutputResources(iconFallback);

                if (next.Length > 0)
                {
                    stage.Output.Add(new TooltipCellData(
                        next[0].Resource != null ? next[0].Resource.Image : null,
                        "0",
                        TooltipCellState.Neutral));
                }
            }

            return stage;
        }

        /// <summary>
        /// Время производства. Берём productionData, а НЕ TransitionData.GameplayIntervalGeneralParameters:
        /// ComplexObjectBuildingController делит DurationSeconds у TransitionData прямо в объекте
        /// (это класс, не структура), поэтому то число уползает вниз с каждой стройкой.
        /// productionData применяет скорость через SetIntervalSpeed и не мутируется.
        ///
        /// Стадия не производит (непостроенная) → честный «0», а не пустая ячейка: рядом стоит
        /// иконка часов, и пустота под ней читается как «число не прогрузилось».
        /// Базам этот ноль не грозит: в их шаблоне ячейки таймера в префабе нет вовсе,
        /// поле остаётся пустым и код его молча пропускает.
        /// </summary>
        private static string FormatProductionTime(StaticObjectView view)
        {
            if (view == null
                || !view.CanProduceObjects
                || view.productionData == null
                || view.productionData.GameplayIntervalGeneralParameters == null)
            {
                return "0";
            }

            return Mathf.RoundToInt(view.productionData.GameplayIntervalGeneralParameters.DurationSeconds)
                .ToString();
        }

        private static TooltipGroupData BuildGroup(IReadOnlyList<ResourceAmount> need, ResourceAmount[] have)
        {
            if (need == null || need.Count == 0)
            {
                return null;
            }

            var group = new TooltipGroupData { IsAffordable = true };

            foreach (var resourceAmount in need)
            {
                var enough = IsEnough(resourceAmount, have);

                group.IsAffordable &= enough;

                group.Cells.Add(new TooltipCellData(
                    resourceAmount.Resource != null ? resourceAmount.Resource.Image : null,
                    resourceAmount.Amount.ToString(),
                    enough ? TooltipCellState.Enough : TooltipCellState.NotEnough));
            }

            return group;
        }

        /// <summary>
        /// Повторяет ровно ту же логику, что SetResourceViews у старых тултипов,
        /// чтобы карточка и старый тултип никогда не расходились в оценке "хватает".
        /// </summary>
        private static bool IsEnough(ResourceAmount need, ResourceAmount[] have)
        {
            if (have == null)
            {
                return true;
            }

            var resource = have.FirstOrDefault(res => res.Resource.name == need.Resource.name);

            return resource != null && resource.Amount >= need.Amount;
        }

        private static TooltipCellData ToNeutralCell(ResourceAmount resourceAmount)
        {
            return new TooltipCellData(
                resourceAmount.Resource != null ? resourceAmount.Resource.Image : null,
                resourceAmount.Amount.ToString(),
                TooltipCellState.Neutral);
        }

        #endregion
    }
}