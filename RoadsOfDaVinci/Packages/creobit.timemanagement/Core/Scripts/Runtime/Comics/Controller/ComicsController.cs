using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Creobit.AddressablesController;
using Creobit.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller
{
    public class ComicsController : IComicsController
    {
        private IComicsService _comicsService;
        private IAddressablesController _addressablesController;
        private IUIController _uiController;
        private IMapController _mapController;
        private ISaveController _saveController;

        private List<ComicsData> _comicsData;

        public event Action OnComicsShowed;
        public event Action OnComicsCompleted;
        public IComicsService ComicsService => _comicsService;
        public List<ComicsData> ComicsData => _comicsData;

        [Inject]
        public void Construct(IAddressablesController addressablesController,
            IUIController uiController,
            IMapController mapController,
            ISaveController saveController)
        {
            _addressablesController = addressablesController;
            _uiController = uiController;
            _mapController = mapController;
            _saveController = saveController;
        }
        
        public UniTask Load(ComicsJson comicsJson)
        {
            _comicsService = new ComicsService(_addressablesController, comicsJson, _uiController);
            _comicsService.OnCompleted += HandleComicsCompleted;
            TryPreloadNextComics();

            return UniTask.CompletedTask;
        }

        public void Initialize(List<ComicsData> comicsData)
        {
            _comicsData = comicsData;
        }

        private void TryPreloadNextComics()
        {
            if (_comicsData == null || _comicsData.Count == 0)
            {
                return;
            }

            // Ищем последнюю пройденную запись, которая ЕСТЬ в текущем списке комиксов.
            // Раньше здесь был First() по имени из сейва: любое незнакомое имя
            // (переименованный комикс или чужая запись в PassedComics) роняло загрузку меты.
            var passed = _saveController.CurrentSaveData?.PassedComics;
            var lastIndex = -1;

            if (passed != null)
            {
                for (var i = passed.Count - 1; i >= 0 && lastIndex < 0; i--)
                {
                    lastIndex = _comicsData.FindIndex(comics => comics != null && comics.ComicsName == passed[i]);
                }
            }

            var nextIndex = lastIndex + 1;

            if (nextIndex < _comicsData.Count)
            {
                PreloadComics(_comicsData[nextIndex]).Forget();
            }
        }

        public UniTask PreloadComics(ComicsData comicsData)
        {
            return _comicsService.PreloadComics(comicsData);
        }

        public bool IsComicsReady(ComicsData comicsData)
        {
            var isPassedComics = _saveController.Service.IsComicsPassed(comicsData.ComicsName);
            
            if (isPassedComics)
                return false;

            if (comicsData.ComicsCondition == ComicsConditions.AnyCondition)
            {
                return true;
            }

            if (comicsData.ComicsCondition == ComicsConditions.BeforeLevel)
            {
                if (_mapController.CurrentLevel == comicsData.LevelToShow)
                    return true;
            }
            else if(comicsData.ComicsCondition == ComicsConditions.AfterLevel)
            {
                if (_saveController.CurrentSaveData.LastPassedLevel == comicsData.LevelToShow)
                    return true;
            }

            return false;
        }

        public bool IsComicsReady(out ComicsData comicsData)
        {
            if (_comicsData == null)
            {
                comicsData = null;
                return false;
            }

            foreach (var comics in _comicsData)
            {
                if (comics.ComicsCondition == ComicsConditions.AnyCondition)
                    continue;

                if (IsComicsReady(comics))
                {
                    comicsData = comics;
                    return true;
                }
            }

            foreach (var comics in _comicsData)
            {
                if (IsComicsReady(comics))
                {
                    comicsData = comics;
                    return true;
                }
            }

            comicsData = null;
            return false;
        }

        public async UniTask ShowComics(ComicsData comicsData)
        {
            _saveController.Service.SaveComics(comicsData.ComicsName);
            await _comicsService.ShowComics(comicsData);

            OnComicsShowed?.Invoke();
        }

        private void HandleComicsCompleted()
        {
            OnComicsCompleted?.Invoke();
        }

        public void EndComics()
        {
            _comicsService.EndComics();
        }

        public void TryOpenNextPage()
        {
            _comicsService.TryOpenNextPage();
        }
    }
}