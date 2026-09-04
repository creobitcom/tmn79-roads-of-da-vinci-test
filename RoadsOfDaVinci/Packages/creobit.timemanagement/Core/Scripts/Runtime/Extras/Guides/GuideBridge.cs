using System;
using System.Collections.Generic;
using Creobit.Logger;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuideBridge : MonoBehaviour
    {
        [SerializeField] private List<GuideSettings> _guideSettingsCached = new List<GuideSettings>();
        [SerializeField] private GuideStepView _guideStepViewPreset;
        [SerializeField] private Transform _guidePanelTransform;
        [SerializeField] private List<GuideStepView> _guideStepViewsCached = new List<GuideStepView>();

        [SerializeField] private List<PanelReference> _uiPanels = new List<PanelReference>();
        
        private IUIController _uiController;

        [Inject]
        private void Construct(IUIController uiController)
        {
            _uiController = uiController;
        }

        private void Awake()
        {
            var objects = FindObjectsByType<GuideTargetView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var guideStepView in objects)
            {
                var guidesSettings = guideStepView.GuideSettings;
                guidesSettings.Position = guideStepView.transform.position;
                _guideSettingsCached.Add(guidesSettings);
            }

            GenerateGuidePrefabs();
        }

        private void GenerateGuidePrefabs()
        {
            foreach (var guideSettings in _guideSettingsCached)
            {
                var guideStepView = Instantiate(_guideStepViewPreset,_guidePanelTransform);
                if (Camera.main == null)
                {
                    DestroyImmediate(guideStepView.gameObject);
                    Log.Gameplay.Error("Camera not found.");
                    break;
                }
                guideStepView.transform.position = Camera.main.WorldToScreenPoint(guideSettings.Position) + new Vector3(guideSettings.Offset.x, guideSettings.Offset.y, 0);
                guideStepView.SetText(guideSettings.UseLocalization ? "ShowLocalizedText" : guideSettings.Step.ToString());
                _guideStepViewsCached.Add(guideStepView);
            }
        }

        private void CalculatePositions()
        {
            for (int i = 0; i < _guideStepViewsCached.Count; i++)
            {
                var settings = _guideSettingsCached[i];
                _guideStepViewsCached[i].transform.position = Camera.main.WorldToScreenPoint(settings.Position) + new Vector3(settings.Offset.x, settings.Offset.y, 0);
            }
        }

        /// <summary>
        /// TODO Упразднить и заменить в 7 части артефактов и последующих проектах на модулях, заглушка на время тестирования
        /// </summary>
        [ContextMenu("TestShowGuide")]
        public async void ShowGuide()
        {
            CalculatePositions();
            _guidePanelTransform.gameObject.SetActive(true);

            await AwaitPanelsTask(false);
            Time.timeScale = 0f;
        }

        public async void HideGuide()
        {
            _guidePanelTransform.gameObject.SetActive(false);
            Time.timeScale = 1.0f;
            await AwaitPanelsTask(true);
        }

        private async UniTask AwaitPanelsTask(bool isShow)
        {
            foreach (var uiPanel in _uiPanels)
            {
                if (isShow)
                {
                    _uiController.ShowPanel(uiPanel,null);
                }
                else
                {
                    _uiController.HidePanel(uiPanel,null);
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1f));
        }
        
        // [Obsolete]
        // public void ShowLocationGuideView(int locationNum)
        // {
        //     _guidesController.ShowLocationGuideView(locationNum);
        // }
        // [Obsolete]
        // public void ShowNextLocation() => _guidesController.ShowNextLocation();
        // [Obsolete]
        // public void ShowPrevLocation() => _guidesController.ShowPrevLocation();
    }
}