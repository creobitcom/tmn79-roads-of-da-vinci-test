using System;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller
{
    public interface IComicsService : IDisposable
    {
        public event Action OnCompleted;
        public UniTask ShowComics(ComicsData comicsData);
        public UniTask PreloadComics(ComicsData comicsData);
        public void EndComics();
        public void TryOpenNextPage();
    }
}