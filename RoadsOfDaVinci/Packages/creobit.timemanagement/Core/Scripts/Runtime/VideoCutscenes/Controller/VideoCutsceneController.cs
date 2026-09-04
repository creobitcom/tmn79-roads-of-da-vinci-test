using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.UI;
using Creobit.Audio;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Controller
{
    /// <summary>
    /// Решает, какую катсцену показать, спавнит экран и ведёт проигрывание.
    /// Флаги просмотра пишутся в общий список PassedComics с префиксом "video:",
    /// чтобы не менять формат сейва.
    /// </summary>
    public class VideoCutsceneController : IVideoCutsceneController, IDisposable
    {
        private IMapController _mapController;
        private ISaveController _saveController;
        private IAudioService _audioService;

        private CutsceneLibrarySO _library;
        private Transform _uiRoot;

        private VideoCutsceneView _activeView;

        public bool IsPlaying { get; private set; }
        public VideoCutsceneSO LastPlayed { get; private set; }

        public IReadOnlyList<VideoCutsceneSO> Cutscenes =>
            _library == null ? Array.Empty<VideoCutsceneSO>() : _library.Cutscenes;

        public event Action OnCutsceneStarted;
        public event Action OnCutsceneCompleted;

        [Inject]
        public void Construct(IMapController mapController,
            ISaveController saveController,
            IAudioService audioService)
        {
            _mapController = mapController;
            _saveController = saveController;
            _audioService = audioService;
        }

        public void Initialize(CutsceneLibrarySO library, Transform uiRoot)
        {
            _library = library;
            _uiRoot = uiRoot;
        }

        public bool TryGetReady(out VideoCutsceneSO cutscene, params VideoCutsceneTrigger[] triggers)
        {
            cutscene = null;

            if (_library == null || triggers == null || triggers.Length == 0)
            {
                return false;
            }

            foreach (var candidate in _library.Cutscenes)
            {
                if (candidate == null)
                {
                    continue;
                }

                if (Array.IndexOf(triggers, candidate.Trigger) < 0)
                {
                    continue;
                }

                if (!IsReady(candidate))
                {
                    continue;
                }

                cutscene = candidate;
                return true;
            }

            return false;
        }

        public bool IsReady(VideoCutsceneSO cutscene)
        {
            if (cutscene == null || string.IsNullOrEmpty(cutscene.VideoFile))
            {
                return false;
            }

            if (cutscene.PlayOnce && _saveController.Service.IsComicsPassed(cutscene.SaveKey))
            {
                return false;
            }

            switch (cutscene.Trigger)
            {
                case VideoCutsceneTrigger.OnNewProfile:
                    return true;

                case VideoCutsceneTrigger.BeforeLevel:
                    return _mapController != null && _mapController.CurrentLevel == cutscene.Level;

                case VideoCutsceneTrigger.AfterLevel:
                    return _saveController.CurrentSaveData != null
                           && _saveController.CurrentSaveData.LastPassedLevel == cutscene.Level;

                case VideoCutsceneTrigger.Manual:
                    return true;

                default:
                    return false;
            }
        }

        public VideoCutsceneSO Find(string id) => _library == null ? null : _library.Find(id);

        public async UniTask Play(VideoCutsceneSO cutscene)
        {
            if (cutscene == null || IsPlaying)
            {
                return;
            }

            IsPlaying = true;
            LastPlayed = cutscene;

            var musicPaused = false;
            var attempted = false;

            try
            {
                if (_library == null || _library.ViewPrefab == null)
                {
                    Log.Meta.Error("VideoCutscene: не задан префаб экрана в CutsceneLibrary.");
                    return;
                }

                var parent = _uiRoot != null ? _uiRoot : null;
                var instance = Object.Instantiate(_library.ViewPrefab, parent, false);

                _activeView = instance.GetComponent<VideoCutsceneView>();

                if (_activeView == null)
                {
                    Log.Meta.Error("VideoCutscene: на префабе экрана нет компонента VideoCutsceneView.");
                    Object.Destroy(instance);
                    return;
                }

                if (cutscene.PauseMusic && _audioService != null)
                {
                    _audioService.PauseMusic();
                    musicPaused = true;
                }

                Log.Meta.Info($"VideoCutscene: играем {cutscene.Id} ({cutscene.VideoFile})");

                _activeView.ApplyStyle(_library.SubtitleStyle);
                _activeView.ApplyAudioGroup(_library.AudioMixerGroup);
                _activeView.Started += CutsceneStartedHandler;

                attempted = true;

                await _activeView.PlayAsync(cutscene);
            }
            catch (Exception exception)
            {
                Log.Meta.Error(exception);
            }
            finally
            {
                if (_activeView != null)
                {
                    _activeView.Started -= CutsceneStartedHandler;
                    Object.Destroy(_activeView.gameObject);
                    _activeView = null;
                }

                if (musicPaused && _audioService != null)
                {
                    _audioService.ResumeMusic();
                }

                // Сломанная настройка (нет префаба экрана) не должна тихо "съедать" катсцену:
                // отметку ставим только если экран действительно создавался.
                if (attempted)
                {
                    MarkPassed(cutscene);
                }

                IsPlaying = false;

                OnCutsceneCompleted?.Invoke();
            }
        }

        private void CutsceneStartedHandler() => OnCutsceneStarted?.Invoke();

        private void MarkPassed(VideoCutsceneSO cutscene)
        {
            if (!cutscene.PlayOnce || _saveController == null)
            {
                return;
            }

            _saveController.Service.SaveComics(cutscene.SaveKey);
            _saveController.Save();
        }

        public void Dispose()
        {
            if (_activeView != null)
            {
                Object.Destroy(_activeView.gameObject);
                _activeView = null;
            }

            OnCutsceneStarted = null;
            OnCutsceneCompleted = null;
        }
    }
}
