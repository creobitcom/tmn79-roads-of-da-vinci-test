using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Guide.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using Creobit.Localization;
using Creobit.Logger;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuidesController : IGuidesController
    {
        public ReactiveProperty<GuidesStates> State { get; } = new(GuidesStates.None);

        public event Action<int> LevelGuideOpened;

        private IUIController _uiController;
        private GuidesControllerJson _json;
        private GuideContentLoader _loader;
        private GuidePagesPresenter _pagesPresenter;

        private readonly List<GuidesLevelRefs> _spawnedLevelRefs = new();
        private readonly List<GuideLocationRefs> _spawnedLocationRefs = new();

        private int _locationNum;
        private int _levelNum;

        [Inject]
        public void Construct(IUIController uiController)
        {
            _uiController = uiController;
        }

        public UniTask Load(GuidesControllerJson guidesControllerJson)
        {
            _json = guidesControllerJson;
            _loader = new GuideContentLoader(string.IsNullOrEmpty(_json.ContentFolder)
                ? "Guide"
                : _json.ContentFolder);

            _pagesPresenter = new GuidePagesPresenter(_loader);
            _pagesPresenter.CloseRequested += HandleCloseRequested;
            _pagesPresenter.LevelOpened += HandleLevelOpened;

            LocalizationService.Instance.OnLanguageChanged += OnLanguageChanged;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            LocalizationService.Instance.OnLanguageChanged -= OnLanguageChanged;

            if (_pagesPresenter != null)
            {
                _pagesPresenter.CloseRequested -= HandleCloseRequested;
                _pagesPresenter.LevelOpened -= HandleLevelOpened;
                _pagesPresenter.Clear();
            }

            _loader?.ClearPictures();
        }

        public void ChangeState(GuidesStates states)
        {
            State.Value = states;

            switch (states)
            {
                case GuidesStates.None:
                    CloseGuides();
                    break;
                case GuidesStates.MainPage:
                    OpenLocationsMenu().Forget();
                    break;
                case GuidesStates.LevelsMenu:
                    OpenLevelsMenu().Forget();
                    break;
                case GuidesStates.GuidePage:
                    OpenGuidePage().Forget();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(states), states, null);
            }
        }

        public void ShowLocationGuideView(int locationNum)
        {
            _locationNum = locationNum;
            ChangeState(GuidesStates.LevelsMenu);
        }

        public void ShowNextLocation()
        {
            var locations = AvailableLocations();
            var index = locations.IndexOf(_locationNum);

            if (index < 0 || index + 1 >= locations.Count)
            {
                return;
            }

            ShowLocationGuideView(locations[index + 1]);
        }

        public void ShowPrevLocation()
        {
            var locations = AvailableLocations();
            var index = locations.IndexOf(_locationNum);

            if (index <= 0)
            {
                return;
            }

            ShowLocationGuideView(locations[index - 1]);
        }

        public void ShowLevelView(int levelNum)
        {
            _levelNum = levelNum;
            ChangeState(GuidesStates.GuidePage);
        }




        public void ShowPage(int pageIndex) => _pagesPresenter?.ShowPage(pageIndex);

        public void ShowNextPage() => _pagesPresenter?.ShowNextPage();

        public void ShowPrevPage() => _pagesPresenter?.ShowPrevPage();

        private async UniTaskVoid OpenGuidePage()
        {
            HidePanel(_json.MainPagePanel);
            HidePanel(_json.SelectLevelPanel);

            _uiController.ShowPanel(_json.GuidePanel, _json.PanelsParent);

            var panelView = await GetView<GuidesPanelView>(_json.GuidePanel);

            if (_json.MetaGuideLevelRefs != null)
            {
                _json.MetaGuideLevelRefs.gameObject.SetActive(true);
            }

            var shown = await _pagesPresenter.Show(panelView, _levelNum);

            if (!shown)
            {
                ChangeState(GuidesStates.LevelsMenu);
            }
        }

        private void HandleCloseRequested()
        {
            ChangeState(GuidesStates.LevelsMenu);
        }

        private void HandleLevelOpened(int levelNumber)
        {
            LevelGuideOpened?.Invoke(levelNumber);
        }

        public void ShowGameplayGuide(int levelNum)
        {
            ShowGameplayGuideAsync(levelNum).Forget();
        }

        private async UniTaskVoid ShowGameplayGuideAsync(int levelNum)
        {
            if (_json.GameplayGuidePanel == null)
            {
                return;
            }

            var content = await _loader.LoadLevel(levelNum);

            if (content == null || !content.HasPages)
            {
                Log.Meta.Warning($"Gameplay guide has no pages for level {levelNum}");
                return;
            }

            var panel = await _uiController.ShowPanel<GuidePanelView>(_json.GameplayGuidePanel, _json.PanelsParent);
            var sprite = await _loader.LoadPicture(levelNum, content.GuideItems[0].PictureName);

            if (panel != null && sprite != null)
            {
                panel.SetGuide(sprite);
            }

            LevelGuideOpened?.Invoke(levelNum);
        }

        private async UniTaskVoid OpenLocationsMenu()
        {
            HidePanel(_json.SelectLevelPanel);
            HideGuidePage();

            _uiController.ShowPanel(_json.MainPagePanel, _json.PanelsParent);

            var menuRefs = await GetView<GuidesMenuRefs>(_json.MainPagePanel);

            if (menuRefs == null)
            {
                return;
            }

            if (menuRefs.titleText != null)
            {
                menuRefs.titleText.text = LocalizationService.Instance.GetText("guides_title");
            }

            var locations = _json.Locations ?? new List<GuideLocationData>();
            var locationViews = ResolveLocationViews(menuRefs, locations.Count);

            for (var i = 0; i < locationViews.Count; i++)
            {
                var locationRefs = locationViews[i];

                if (locationRefs == null)
                {
                    continue;
                }

                if (i >= locations.Count)
                {
                    locationRefs.gameObject.SetActive(false);
                    continue;
                }

                var location = locations[i];
                locationRefs.gameObject.SetActive(true);

                if (locationRefs.titleText != null)
                {
                    locationRefs.titleText.text = LocalizationService.Instance.GetText(location.LocationName);
                }

                BindButton(locationRefs.GetComponentInChildren<Button>(true), () => ShowLocationGuideView(location.Num));
            }
        }

        private async UniTaskVoid OpenLevelsMenu()
        {
            HidePanel(_json.MainPagePanel);
            HideGuidePage();

            _uiController.ShowPanel(_json.SelectLevelPanel, _json.PanelsParent);

            var levelsRefs = await GetView<GuidesLevelsMenuRefs>(_json.SelectLevelPanel);

            if (levelsRefs == null || levelsRefs.levelsRoot == null || _json.LevelRefsPreset == null)
            {
                return;
            }

            if (levelsRefs.titleText != null)
            {
                var location = _json.Locations?.FirstOrDefault(x => x.Num == _locationNum);
                levelsRefs.titleText.text = location == null
                    ? string.Empty
                    : LocalizationService.Instance.GetText(location.LocationName);
            }

            ClearSpawnedLevels();

            foreach (var levelReference in LevelsOfLocation(_locationNum))
            {
                var levelNum = levelReference.levelNum;
                var refs = Object.Instantiate(_json.LevelRefsPreset, levelsRefs.levelsRoot);

                if (refs.text != null)
                {
                    refs.text.text = levelNum.ToString();
                }

                BindButton(refs.levelButton, () => ShowLevelView(levelNum));
                _spawnedLevelRefs.Add(refs);
            }
        }




        private List<GuideLocationRefs> ResolveLocationViews(GuidesMenuRefs menuRefs, int required)
        {
            if (menuRefs.locations is { Count: > 0 })
            {
                return menuRefs.locations;
            }

            if (menuRefs.locationTemplate == null || menuRefs.locationsRoot == null)
            {
                return new List<GuideLocationRefs>();
            }

            ClearSpawnedLocations();
            menuRefs.locationTemplate.gameObject.SetActive(false);

            for (var i = 0; i < required; i++)
            {
                var view = Object.Instantiate(menuRefs.locationTemplate, menuRefs.locationsRoot);
                view.gameObject.SetActive(true);
                _spawnedLocationRefs.Add(view);
            }

            return _spawnedLocationRefs;
        }

        private void ClearSpawnedLocations()
        {
            foreach (var view in _spawnedLocationRefs)
            {
                if (view != null)
                {
                    Object.Destroy(view.gameObject);
                }
            }

            _spawnedLocationRefs.Clear();
        }




        private void CloseGuides()
        {
            HidePanel(_json.MainPagePanel);
            HidePanel(_json.SelectLevelPanel);
            HideGuidePage();
            ClearSpawnedLevels();
            ClearSpawnedLocations();

            _pagesPresenter?.Clear();
        }

        private void HideGuidePage()
        {
            HidePanel(_json.GuidePanel);

            if (_json.MetaGuideLevelRefs != null)
            {
                _json.MetaGuideLevelRefs.gameObject.SetActive(false);
            }
        }

        private void HidePanel(PanelReference panelReference)
        {
            if (panelReference != null)
            {
                _uiController.HidePanel(panelReference, _json.PanelsParent);
            }
        }

        private async UniTask<T> GetView<T>(PanelReference panelReference) where T : Component
        {
            if (panelReference == null)
            {
                return null;
            }

            var panel = await _uiController.GetPanel(panelReference);

            return panel == null ? null : panel.GetComponent<T>();
        }

        private void ClearSpawnedLevels()
        {
            foreach (var refs in _spawnedLevelRefs)
            {
                if (refs != null)
                {
                    Object.Destroy(refs.gameObject);
                }
            }

            _spawnedLevelRefs.Clear();
        }

        private List<int> AvailableLocations()
        {
            if (_json.AllLevels?.AllLevels == null)
            {
                return new List<int>();
            }

            return _json.AllLevels.AllLevels
                .Select(x => x.locationNum)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        private IEnumerable<LevelReferenceByNum> LevelsOfLocation(int locationNum)
        {
            if (_json.AllLevels?.AllLevels == null)
            {
                return Enumerable.Empty<LevelReferenceByNum>();
            }

            return _json.AllLevels.AllLevels
                .Where(x => x.locationNum == locationNum)
                .OrderBy(x => x.levelNum);
        }

        private static void BindButton(Button button, Action callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback());
        }

        private void OnLanguageChanged(string language)
        {
            if (State.Value == GuidesStates.GuidePage)
            {
                _pagesPresenter?.Refresh();
            }
        }
    }
}
