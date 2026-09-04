using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Bridge
{
    public class ComicsBridge : MonoBehaviour
    {
        [SerializeField] private UltEvent _onComicsShowed;
        [SerializeField] private UltEvent _onComicsCompleted;

        [SerializeField] private ComicsData[] _comicsData;

        private IComicsController _comicsController;

        [Inject]
        private void Construct(IComicsController comicsController)
        {
            _comicsController = comicsController;
        }

        private void Start()
        {
            _comicsController.OnComicsShowed += _onComicsShowed.InvokeSafe;
            _comicsController.OnComicsCompleted += _onComicsCompleted.InvokeSafe;

            if (_comicsData.Length > 0)
            {
                _comicsController.Initialize(_comicsData.ToList());
            }
        }

        public void ShowComics(ComicsData comicsData)
        {
            _comicsController.ShowComics(comicsData);
        }

        public void TryShowComics(ComicsData comicsData)
        {
            if(_comicsController.IsComicsReady(comicsData)) 
            {
                _comicsController.ShowComics(comicsData);
            }
        }

        public void TryShowComics()
        {
            foreach(var comics in _comicsData)
            {
                if (_comicsController.IsComicsReady(comics))
                {
                    _comicsController.ShowComics(comics);
                }
            }
        }

        public void TryOpenNextPage()
        {
            _comicsController.TryOpenNextPage();
        }

        public void EndComics()
        {
            _comicsController.EndComics();
        }

        private void OnDestroy()
        {
            _comicsController.OnComicsShowed -= _onComicsShowed.InvokeSafe;
            _comicsController.OnComicsCompleted -= _onComicsCompleted.InvokeSafe;
        }
    }
}