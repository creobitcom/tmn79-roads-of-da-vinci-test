using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI;
using Cysharp.Threading.Tasks;
using ObservableCollections;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.CraftController
{
    public class CraftController : ICraftController
    {
        private readonly GameplaySceneReferences _sceneReferences;
        private readonly IGameplayIntervalsController _intervalsController;
        private readonly IGameResourcesSystem _resourcesSystem;
        private readonly IReloadController _reloadController;
        private readonly IGameplayInputSystem _inputSystem;

        // Окно крафта открыто в данный момент.
        private bool _isWindowShown;
        // Идёт процесс крафта (запущен интервал, ещё не завершён).
        private bool _isCrafting;

        // Рецепты открытого сейчас окна: по ним пересчитываются карточки при изменении ресурсов.
        private RecipesData _shownRecipes;

        // Живёт только пока окно открыто.
        private CompositeDisposable _resourceSubscriptions;

        private MovableObjectTaskView _craftingTaskView;

        public CraftController(GameplaySceneReferences sceneReferences,
            IGameplayIntervalsController intervalsController,
            IGameResourcesSystem resourcesSystem,
            IReloadController reloadController,
            IGameplayInputSystem inputSystem)
        {
            _sceneReferences = sceneReferences;
            _intervalsController = intervalsController;
            _resourcesSystem = resourcesSystem;
            _reloadController = reloadController;
            _inputSystem = inputSystem;
        }

        public UniTask Load()
        {
            _isWindowShown = false;
            _isCrafting = false;

            _reloadController.AddReloadableObject(this);
            _inputSystem.OnMainButtonPressed += OnScreenClicked;

            return UniTask.CompletedTask;
        }

        public UniTask Reload()
        {
            HidePanel(true);
            ClearCraftingIcon();
            _isCrafting = false;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _inputSystem.OnMainButtonPressed -= OnScreenClicked;
            UnsubscribeFromResources();

            _reloadController.RemoveReloadableObject(this);
        }

        // Клик мимо книжки — закрываем окно. Клик по самой панели (карточки, крестик, фон)
        // не трогаем: UI-кнопки обработают его сами.
        private void OnScreenClicked()
        {
            if (!_isWindowShown || IsPointerOverCraftPanel())
            {
                return;
            }

            HideCraftWindow();
        }

        private bool IsPointerOverCraftPanel()
        {
            var panel = _sceneReferences.CraftPanel;
            if (panel == null || EventSystem.current == null)
            {
                return false;
            }

            var screenPos = Pointer.current != null
                ? Pointer.current.position.ReadValue()
                : Vector2.zero;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPos
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            var panelTransform = panel.transform;

            foreach (var result in results)
            {
                if (result.gameObject.transform == panelTransform
                    || result.gameObject.transform.IsChildOf(panelTransform))
                {
                    return true;
                }
            }

            return false;
        }

        public void ShowCraftWindow(RecipesData recipesData, Transform parent, MovableObjectTaskView taskView)
        {
            if (recipesData?.recipes == null || _isWindowShown || _isCrafting)
            {
                return;
            }

            var panel = _sceneReferences.CraftPanel;
            if (panel == null || panel.cards == null || panel.cards.Length == 0)
            {
                return;
            }

            var count = Mathf.Min(recipesData.recipes.Count, panel.cards.Length);
            if (count <= 0)
            {
                return;
            }

            _isWindowShown = true;
            _shownRecipes = recipesData;

            panel.gameObject.SetActive(true);

            BindCloseButton();

            panel.SetRecipeCount(count);

            for (var i = 0; i < count; i++)
            {
                FillCard(panel.cards[i], recipesData.recipes[i], parent, taskView);
            }

            SubscribeToResources();

            GetPanelAnimator(panel)?.PlayShow();
        }

        public void HideCraftWindow() => HidePanel(false);

        private void HidePanel(bool immediately)
        {
            UnsubscribeFromResources();

            _isWindowShown = false;
            _shownRecipes = null;

            var panel = _sceneReferences.CraftPanel;
            if (panel == null)
            {
                return;
            }

            var animator = GetPanelAnimator(panel);
            if (animator == null)
            {
                panel.gameObject.SetActive(false);
                return;
            }

            void Deactivate() => panel.gameObject.SetActive(false);

            if (immediately)
            {
                animator.HideImmediately(Deactivate);
                return;
            }

            animator.PlayHide(Deactivate);
        }

        private PanelTransitionAnimator GetPanelAnimator(CraftPanel panel)
        {
            return panel == null ? null : panel.GetComponent<PanelTransitionAnimator>();
        }

        // Пока окно открыто, ресурсы могут измениться под ним: рабочий принёс еду, завершился
        // другой крафт, игрок подобрал предмет. Тогда карточки надо перекрасить и разблокировать.
        // SetResourceAmount всегда пишет в словарь через индексатор, поэтому ловим Replace.
        private void SubscribeToResources()
        {
            UnsubscribeFromResources();

            _resourceSubscriptions = new CompositeDisposable();

            _resourcesSystem.Resources
                .ObserveReplace()
                .Subscribe(_ => RefreshCards())
                .AddTo(_resourceSubscriptions);

            _resourcesSystem.InventoryResources
                .ObserveReplace()
                .Subscribe(_ => RefreshCards())
                .AddTo(_resourceSubscriptions);
        }

        private void UnsubscribeFromResources()
        {
            _resourceSubscriptions?.Dispose();
            _resourceSubscriptions = null;
        }

        private void RefreshCards()
        {
            if (!_isWindowShown || _shownRecipes?.recipes == null)
            {
                return;
            }

            var panel = _sceneReferences.CraftPanel;
            if (panel == null || panel.cards == null)
            {
                return;
            }

            var count = Mathf.Min(_shownRecipes.recipes.Count, panel.cards.Length);

            for (var i = 0; i < count; i++)
            {
                RefreshCard(panel.cards[i], _shownRecipes.recipes[i]);
            }
        }

        // Разовая настройка карточки: раскладка входа и обработчик клика. Всё, что зависит
        // от количества ресурсов, считает RefreshCard — его же зовём при каждом их изменении.
        private void FillCard(RecipeRefs card, RecipeData recipe, Transform parent, MovableObjectTaskView taskView)
        {
            if (card == null || recipe == null)
            {
                return;
            }

            card.data = recipe;

            card.SetInputLayout(recipe.inputResources?.Count ?? 0);

            if (card.button != null)
            {
                card.button.onClick.RemoveAllListeners();
                card.button.onClick.AddListener(() =>
                    StartRecipe(_sceneReferences.CraftPanel.craftIntervalParameters, recipe, parent, taskView));
            }

            RefreshCard(card, recipe);
        }

        // Пересчитывает только то, что зависит от текущих запасов: цвет чисел, красные круги,
        // синяя/серая рамка результата и доступность кнопки. Раскладку и клик не трогает.
        private void RefreshCard(RecipeRefs card, RecipeData recipe)
        {
            if (card == null || recipe == null)
            {
                return;
            }

            var inputCount = recipe.inputResources?.Count ?? 0;
            var allEnough = recipe.inputResources != null;

            for (var i = 0; i < inputCount; i++)
            {
                var resourceAmount = recipe.inputResources[i];

                var enough = resourceAmount?.Resource != null
                             && _resourcesSystem.GetResourceAmount(resourceAmount.Resource) >= resourceAmount.Amount;

                if (!enough)
                {
                    allEnough = false;
                }

                if (card.inputSlots != null && i < card.inputSlots.Length && card.inputSlots[i] != null)
                {
                    card.inputSlots[i].SetData(resourceAmount, enough, false);
                }
            }

            if (card.outputSlot != null && recipe.outputResources != null && recipe.outputResources.Count > 0)
            {
                card.outputSlot.SetData(recipe.outputResources[0], true, true);
            }

            card.SetCraftable(allEnough);
        }

        // Кнопка закрытия окна крафта: вешаем HideCraftWindow напрямую из контроллера
        // (панель не проходит DI-инъекцию, поэтому bridge-подход тут не подходит).
        private void BindCloseButton()
        {
            var closeButton = _sceneReferences.CraftPanel.closeButton;

            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HideCraftWindow);
        }

        public void StartRecipe(GameplayIntervalGeneralParameters parameters, RecipeData data, Transform position, MovableObjectTaskView taskView)
        {
            if (data.inputResources == null || !_resourcesSystem.IsEnoughResources(data.inputResources.ToArray()))
            {
                return;
            }

            // Закрываем окно до списания: иначе подписка на ресурсы дёрнет перерисовку карточек
            // прямо посреди вычитания, на полупустых запасах.
            HideCraftWindow();

            foreach (var resource in data.inputResources)
            {
                _resourcesSystem.SubtractResource(resource);
            }

            _isCrafting = true;
            _craftingTaskView = taskView;

            if (taskView != null)
            {
                taskView.SetIcon(GetOutputSprite(data));
            }

            _intervalsController.StartInterval(new GameplayIntervalSpecificParameters(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    () => RecipeComplete(data, position),
                    null,
                    taskView,
                    null),
                parameters);
        }

        private void RecipeComplete(RecipeData recipeData, Transform position)
        {
            List<ResourceAmount> resourcesAmounts = new();

            if (recipeData.outputResources != null)
            {
                foreach (var resource in recipeData.outputResources)
                {
                    _resourcesSystem.AddResource(resource);
                    resourcesAmounts.Add(resource);
                }
            }

            var popupAmounts = new List<ResourceAmount>();
            var specialItemsPanel = _sceneReferences.InventoryResourcesViewRefs as SpecialItemsPanelView;

            foreach (var resourceAmount in resourcesAmounts)
            {
                if (specialItemsPanel != null && position != null
                    && resourceAmount.Resource is InventoryResource inventoryResource
                    && specialItemsPanel.PlayItemDelivery(inventoryResource, position.position,
                        _sceneReferences.MainCamera))
                {
                    continue;
                }

                popupAmounts.Add(resourceAmount);
            }

            if (popupAmounts.Count > 0)
            {
                _sceneReferences.ResourceAmountAdded
                    .ShowResourcesAmounts(popupAmounts.ToArray(),
                        _sceneReferences.ResourcesView, position, Vector3.up)
                    .Forget();
            }

            ClearCraftingIcon();

            _isCrafting = false;
        }

        private static Sprite GetOutputSprite(RecipeData data)
        {
            if (data.outputResources == null || data.outputResources.Count == 0)
            {
                return null;
            }

            var output = data.outputResources[0];

            return output?.Resource != null ? output.Resource.Image : null;
        }

        private void ClearCraftingIcon()
        {
            if (_craftingTaskView != null)
            {
                _craftingTaskView.ClearIcon();
            }

            _craftingTaskView = null;
        }
    }
}
