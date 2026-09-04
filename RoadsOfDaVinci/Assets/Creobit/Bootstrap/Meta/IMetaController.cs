using _8floor.TimeManagement.Core.Scripts.Runtime.Comics;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.DTO;
using Creobit.Bootstrap.Core.Scripts.Runtime.Operation;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using Creobit.Loading;
using Creobit.UI;
using Creobit.UI.Utility;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public interface IMetaController : ILoadUnit, IDisposable
    {
        public MetaSceneReferences SceneReferences { get; }

        public RuntimeData RuntimeData { get; }

        public ComicsData PendingComics { get; }

        public bool LockState { get; set; }

        public Stack<IMetaState> CurrentStates { get; }

        public event Action OnStateChanged;

        public void ChangeState(IMetaState state);

        public UniTask HandleStateChange(IMetaState currentState, IMetaState newState,
            bool async = true, bool startState = true, bool endState = true);

        public void ReturnLastState();

        public void StateChanged();

        public void ShowPanel(PanelReference panelReference, Transform parent);
        public UniTask<T> ShowPanel<T>(PanelReference panelReference, Transform parent) where T : PanelData;
        public void HidePanel(PanelReference panelReference, Transform parent);
        public DOTweenAnimation[] GetPanelAnimations(PanelReference panelReference, PanelState state);

        public void ShowMap();
        public void HideMap();
        public void RefreshMapAfterUnlockCheat(bool switchToOtherPage = true);

        public OperationResult AddProfile(string profileName);
        public void RemoveProfile(string profileName);

        public void Save(string key, string value);
        public string TryGetSaveValue(string key);

        public void ShowComics(ComicsData comicsData);
        public void ForceShowComics(ComicsData comicsData);
        public void ClearPendingComics();
        public bool IsComicsReady(out ComicsData comicsData);

        public bool TryShowVideoCutscene(params VideoCutsceneTrigger[] triggers);
        public void ShowVideoCutscene(VideoCutsceneSO cutscene);

        public void LoadGameplay();
    }
}
