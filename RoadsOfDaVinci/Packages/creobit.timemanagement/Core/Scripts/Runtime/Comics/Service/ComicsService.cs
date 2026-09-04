using System;
using System.Threading;
using Creobit.AddressablesController;
using Creobit.Localization;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller
{
    public class ComicsService : IComicsService
    {
        public event Action OnCompleted;
        
        private readonly IAddressablesController _addressablesController;
        private readonly IUIController _uiController;
        
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private int _currentPage;
        private ComicsReference _currentComics;
        private ComicsData _currentComicsData;
        private ComicsUIReference _comicsUI;
        private ComicsJson _comicsJson;
        public ComicsService(IAddressablesController addressablesController,
            ComicsJson comicsJson,
            IUIController uiController)
        {
            _addressablesController = addressablesController;
            _comicsJson = comicsJson;
            _uiController = uiController;
        }
        
        public async UniTask ShowComics(ComicsData comicsData)
        {
            EndComics();
            
            var comicsPrefab = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(
                comicsData.ComicsReference);
            
            var comics = (await comicsPrefab.InstantiateAsync(_comicsJson.ComicsRoot, 
                _cancellationTokenSource.Token)).GetComponent<ComicsReference>();

            _uiController.ShowPanel(_comicsJson.ComicsUI, null);
            _comicsJson.ComicsRoot.gameObject.SetActive(true);
            
            _comicsUI = comics.gameObject.GetComponent<ComicsUIReference>();

            _currentComics = comics;
            _currentComicsData = comicsData;
            _currentPage = 0;

            UpdateComicsPages();
        }

        public UniTask PreloadComics(ComicsData comicsData)
        {
            return _addressablesController.LoadAssetByReferenceAsync<GameObject>(
                comicsData.ComicsReference).AsUniTask();
        }

        public void EndComics()
        {
            _uiController.HidePanel(_comicsJson.ComicsUI, _comicsJson.MetaCanvas.transform);
            if (_currentComics == null)
                return;
            
            _currentComics.Destroy();
            _addressablesController.UnloadAssetReference(_currentComicsData.ComicsReference);
            _comicsJson.ComicsRoot.gameObject.SetActive(false);
            OnCompleted?.Invoke();
        }

        public void TryOpenNextPage()
        {
            _currentPage++;

            if (_currentPage >= _currentComics.pages.Count)
            {
                _uiController.HidePanel(_comicsJson.ComicsUI, _comicsJson.MetaCanvas.transform);
                EndComics();
                return;
            }
            
            UpdateComicsPages();
        }

        private void UpdateComicsPages()
        {
            foreach (var page in _currentComics.pages)
            {
                if (page.page.activeSelf && page.page != _currentComics.pages[_currentPage].page)
                {
                    page.page.SetActive(false);
                }
            }
            
            _comicsUI ??= GameObject.FindAnyObjectByType<ComicsUIReference>(FindObjectsInactive.Include);
            if (!_currentComics.pages[_currentPage].page.activeSelf)
            {
                _currentComics.pages[_currentPage].page.SetActive(true);
            }
            _comicsUI.contentText.text = LocalizationService.Instance.GetText(_currentComics.pages[_currentPage].text);
        }
        
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            _cancellationTokenSource.Dispose();
        }
    }
}