using System;
using _8floor.TimeManagement.Artifacts.Runtime.Data;
#if TMN_Module
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
#endif
using Creobit.Bootstrap.Core.Scripts.Runtime.EventsInterceptors;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene
{
        [Serializable]
        public class BootstrapPrefabReferences
        {
                [field: SerializeField]
                public ApplicationEventsInterceptor ApplicationEventsInterceptor { get; private set; }

                [field: SerializeField] public AssetReference LoadingView { get; private set; }
                [field: SerializeField] public AssetReference FadeView { get; private set; }
                [field: SerializeField] public AssetReference PersistentUICanvas { get; private set; }
                [field: SerializeField] public AssetReference SplashUICanvas { get; private set; }

                /// <summary>
                /// Meta UI panel prefabs to warm in Addressables during splash (before Meta activates).
                /// Empty for projects that do not opt in.
                /// </summary>
                [field: SerializeField] public AssetReference[] MetaUiPreloadAssets { get; private set; }



                #region UGS_DEBUG

                [field: SerializeField] public Canvas Canvas { get; private set; }

                #endregion

                #region AudioService

                [field: SerializeField] public AudioMixer AudioMixer { get; private set; }

                [field: SerializeField] public AudioSource MusicAudioSource { get; private set; }

                [field: SerializeField] public AudioSource SfxAudioSource { get; private set; }

                #endregion

                #region GameSettingsController

                [field: SerializeField] public Texture2D Cursor { get; private set; }
                [field: SerializeField] public GameSettings DefaultSettings { get; private set; }

                #endregion

                #region PlayerProfilesController

                [field: SerializeField] public PlayerProfilesRulesData PlayerProfilesRules { get; private set; }

                #endregion

                #region Artifacts

                [field: SerializeField] public ArtifactsDataSO Artifacts { get; private set; }

                #endregion
        }
}