using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    public class CheatsPanel : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField]
        private CheatView _viewPrefab;
        [SerializeField]
        private GameObject _panel;
        [SerializeField]
        private Button _closePanelButton;
        [SerializeField]
        private RectTransform _viewsParent;

        private List<CheatView> _views;

        public event Action Open;
        public event Action Close;

        private void OnEnable()
        {
            _closePanelButton.onClick.AddListener(OnCloseButtonClicked);

            SceneManager.activeSceneChanged += OnSceneChanged;   
        }


        private void OnDisable()
        {
            _closePanelButton.onClick.RemoveListener(OnCloseButtonClicked);

            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        public void SetVisible(bool isVisible)
        {
            _panel.SetActive(isVisible);

            if (isVisible)
            {
                OnOpen();

                Open?.Invoke();
            }
            else
            {
                Close?.Invoke();
            }
        }

        private void SpawnViews(ICheat[] cheats)
        {
            _views = new List<CheatView>();

            for (int i = 0; i < cheats.Length; i++)
            {
                CheatView view = Instantiate(_viewPrefab, _viewsParent);

                view.Initialize(cheats[i]);

                _views.Add(view);

                UpdateViewVisible(view);
            }
        }

        private void UpdateViewVisible(CheatView view)
        {
            view.gameObject.SetActive(view.Info.IsExecutable);
        }

        private void UpdateViewsVisible()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                UpdateViewVisible(_views[i]);
            }
        }

        private void OnOpen()
        {
            if (_views == null)
            {
                SpawnViews(new ProjectCheats().GetCheats());
                return;
            }

            UpdateViewsVisible();
        }

        private void OnCloseButtonClicked()
        {
            SetVisible(false);
        }

        private void OnSceneChanged(Scene arg0, Scene arg1)
        {
            UpdateViewsVisible();
        }
    }
}