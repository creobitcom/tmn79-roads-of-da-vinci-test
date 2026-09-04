using _8floor.TimeManagement.Core.Scripts.Runtime.Comics;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public class MapState : IMetaState
    {
        public void StartState(MetaController metaController)
        {
            var sceneRefs = metaController.SceneReferences;

            sceneRefs.CameraTrackPoint.transform.position = sceneRefs.DefaultTrackPoint.position;
            sceneRefs.PositionComposer.Composition.ScreenPosition = sceneRefs.DefaultTrackOffset;

#if UNITY_STANDALONE
            sceneRefs.ExitButton.gameObject.SetActive(false);
#endif
            if (sceneRefs.ExtrasButton != null)
            {
                sceneRefs.ExtrasButton.gameObject.SetActive(false);
            }

            metaController.ShowMap();

            metaController.ShowPanel(sceneRefs.MapBottomPanel, sceneRefs.MetaCanvas.transform);

            sceneRefs.MapObject.gameObject.SetActive(true);
            metaController.TryShowMapChangeButton();

            if (!metaController.TryShowVideoCutscene(VideoCutsceneTrigger.AfterLevel))
            {
                if (metaController.IsComicsReady(out var comicsData) && comicsData.ComicsCondition == ComicsConditions.AfterLevel)
                {
                    metaController.ChangeState(MetaStates.Comics);
                    metaController.LockState = true;
                    return;
                }
            }

            if (metaController.RuntimeData != null && metaController.RuntimeData.AutoStartNextLevel)
            {
                metaController.RuntimeData.AutoStartNextLevel = false;
                metaController.ChangeState(MetaStates.Gameplay);
            }
        }

        public bool ChangeState(MetaController metaController, IMetaState newState)
        {
            if (newState.GetType() == typeof(MenuState)
                || newState.GetType() == typeof(ComicsState)
                || newState.GetType() == typeof(VideoCutsceneState)
                || newState.GetType() == typeof(CollectionRoomState))
            {
                metaController.HandleStateChange(this, newState).Forget();
                return true;
            }

            if (newState.GetType() == typeof(GameplayState))
            {
                if (!metaController.TryShowVideoCutscene(VideoCutsceneTrigger.BeforeLevel))
                {
                    TryShowComics(metaController);
                }

                metaController.HandleStateChange(this, newState, endState: false).Forget();
                return true;
            }

            return false;
        }

        private void TryShowComics(MetaController metaController)
        {
            if(metaController.IsComicsReady(out ComicsData comicsData))
            {
                if(comicsData.ComicsCondition != ComicsConditions.BeforeLevel) return;
                metaController.ChangeState(MetaStates.Comics);
                metaController.LockState = true;
            }
        }

        public async UniTask EndState(MetaController metaController, bool async)
        {
            var sceneRefs = metaController.SceneReferences;

            metaController.HidePanel(sceneRefs.MapBottomPanel, sceneRefs.MetaCanvas.transform);

            var mapPanelAnimations = metaController.GetPanelAnimations
                (sceneRefs.MapBottomPanel, PanelState.Hide);

            foreach(var tweenAnimation in mapPanelAnimations)
            {
                foreach(var tween in tweenAnimation.GetTweens())
                {
                    await tween.AsyncWaitForCompletion();
                }
            }

            metaController.HideMap();

            sceneRefs.MapObject.gameObject.SetActive(false);
            sceneRefs.MapChangeButton.gameObject.SetActive(false);
        }
    }
}
