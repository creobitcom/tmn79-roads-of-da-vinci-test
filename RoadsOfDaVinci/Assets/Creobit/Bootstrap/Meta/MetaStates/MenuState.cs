using _8floor.TimeManagement.Core.Scripts.Runtime.Comics;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Panels;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Views;
using Creobit.Logger;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using RuntimeConstants = Creobit.Bootstrap.Core.Scripts.Runtime.Utility.RuntimeConstants;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public class MenuState : IMetaState
    {
        public void StartState(MetaController metaController)
        {
            Log.Meta.Info("MenuState: start");

            var sceneRefs = metaController.SceneReferences;

            metaController.ShowPanel(sceneRefs.MenuBottomPanel, sceneRefs.MetaCanvas.transform);

            sceneRefs.CameraTrackPoint.transform.position = sceneRefs.MenuTrackPoint.position;
            sceneRefs.PositionComposer.Composition.ScreenPosition = sceneRefs.DefaultTrackOffset;

            sceneRefs.Background.gameObject.SetActive(true);
#if UNITY_STANDALONE
            sceneRefs.ExitButton.gameObject.SetActive(true);
#endif

#if UNITY_STANDALONE
            if (sceneRefs.ExtrasButton != null)
            {
                sceneRefs.ExtrasButton.gameObject.SetActive(true);
            }
#endif

            if(metaController.TryGetSaveValue(RuntimeConstants.PlayerProfilesPrefs.FirstProfile) == "TRUE")
            {
                Log.Meta.Info("MenuState: It is first profile");
#if UNITY_ANDROID || UNITY_IOS
                CreateMobileDefaultProfile(metaController);
#else
                HandlePlayerProfilePanelView(metaController).Forget();
#endif
            }
        }

#if UNITY_ANDROID || UNITY_IOS
        private void CreateMobileDefaultProfile(MetaController metaController)
        {
            var operationResult = metaController.AddProfile(RuntimeConstants.PlayerProfilesPrefs.MobileDefaultProfileName);

            if (!operationResult.IsSuccess)
            {
                Log.Meta.Error($"MenuState: mobile default profile creation failed: {operationResult.ErrorMessage}");
                return;
            }

            metaController.RemoveProfile(RuntimeConstants.PlayerProfilesPrefs.FirstDefaultProfileName);
            metaController.Save(RuntimeConstants.PlayerProfilesPrefs.FirstProfile, "FALSE");
        }
#endif

        private async UniTaskVoid HandlePlayerProfilePanelView(MetaController metaController)
        {
            var createProfilePanel = metaController.SceneReferences.CreateProfilePanel;

            (await metaController.ShowPanel<CreatePlayerProfilePanelView>(createProfilePanel, null)).SetInputData(
                ok: (profileName) => 
                {
                    var operationResult = metaController.AddProfile(profileName);

                    if(!operationResult.IsSuccess)
                    {
                        ShowErrorAlert(metaController, operationResult.ErrorMessage).Forget();
                        return;
                    }

                    metaController.RemoveProfile(RuntimeConstants.PlayerProfilesPrefs.FirstDefaultProfileName);

                    metaController.Save(RuntimeConstants.PlayerProfilesPrefs.FirstProfile, "FALSE");

                    metaController.HidePanel(createProfilePanel, null);
                },
                cancel: () => {
                    metaController.HidePanel(createProfilePanel, null);
                }
            );
        }

        private async UniTaskVoid ShowErrorAlert(MetaController metaController, string message)
        {
            var alertPanel = metaController.SceneReferences.AlertPanel;
            
            (await metaController.ShowPanel<AlertPanelView>(alertPanel, null)).SetInputData("Error", message,
                ok: () => {
                    metaController.HidePanel(alertPanel, null);
                });
        }

        public bool ChangeState(MetaController metaController, IMetaState newState)
        {
            if (newState.GetType() == typeof(MapState))
            {
                // Сначала видео-катсцена; нет подходящей — работает старый путь с комиксом.
                if (!metaController.TryShowVideoCutscene(VideoCutsceneTrigger.OnNewProfile,
                        VideoCutsceneTrigger.AfterLevel))
                {
                    TryShowComics(metaController);
                }

                metaController.SceneReferences.CurrentLevel.WasVisited = false;
                metaController.HandleStateChange(this, newState).Forget();
                return true;
            }

            if (newState.GetType() == typeof(GuidesState))
            {
                metaController.HandleStateChange(this, newState, async: false).Forget();
                return true;
            }

            if (newState.GetType() == typeof(ComicsState)
                || newState.GetType() == typeof(VideoCutsceneState))
            {
                metaController.HandleStateChange(this, newState).Forget();
                return true;
            }

            return false;
        }

        private bool TryShowComics(MetaController metaController)
        {
            if (!metaController.IsComicsReady(out var comicsData)) return false;
            if (comicsData.ComicsCondition == ComicsConditions.BeforeLevel) return false;
                
            metaController.ChangeState(MetaStates.Comics);
            metaController.LockState = true;
            return true;
        }

        public async UniTask EndState(MetaController metaController, bool async)
        {
            var sceneRefs = metaController.SceneReferences;

            metaController.HidePanel(sceneRefs.MenuBottomPanel, sceneRefs.MetaCanvas.transform);

            var menuPanelAnimations = metaController.GetPanelAnimations
                (sceneRefs.MenuBottomPanel, PanelState.Hide);

            foreach(var tweenAnimation in menuPanelAnimations)
            {
                foreach(var tween in tweenAnimation.GetTweens())
                {
                    if(async) await tween.AsyncWaitForCompletion();
                } 
            }

            sceneRefs.Background.gameObject.SetActive(false);
#if UNITY_STANDALONE
            sceneRefs.ExitButton.gameObject.SetActive(false);
#endif
            if (sceneRefs.ExtrasButton != null)
            {
                sceneRefs.ExtrasButton.gameObject.SetActive(false);
            }
        }
    }
}