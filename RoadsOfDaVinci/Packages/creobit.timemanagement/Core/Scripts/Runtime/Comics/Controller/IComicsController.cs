using System;
using System.Collections.Generic;
using Creobit.Loading;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller
{
    public interface IComicsController : ILoadUnit<ComicsJson>
    {
        public IComicsService ComicsService { get; }
        public List<ComicsData> ComicsData { get; }
        public event Action OnComicsShowed;
        public event Action OnComicsCompleted;

        public void Initialize(List<ComicsData> comicsData);
        public UniTask ShowComics(ComicsData comicsData);
        public UniTask PreloadComics(ComicsData comicsData);
        public bool IsComicsReady(ComicsData comicsData);
        public bool IsComicsReady(out ComicsData comicsData);
        public void TryOpenNextPage();
        public void EndComics();
    }
    
    [Serializable]
    public class ComicsJson
    {
        public Transform ComicsRoot;
        public PanelReference ComicsUI;
        public Canvas MetaCanvas;
            
        public ComicsJson(Transform comicsRoot, PanelReference comicsUI, Canvas metaCanvas)
        {
                ComicsRoot = comicsRoot;
                ComicsUI = comicsUI;
                MetaCanvas = metaCanvas;
        }
    }
}