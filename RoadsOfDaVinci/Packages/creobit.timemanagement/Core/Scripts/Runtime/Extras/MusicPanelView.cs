using System;
using System.Collections.Generic;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras.Views
{
    public class MusicPanelView : PanelData
    {
        [SerializeField]
        private TrackData[] _musicTracks;
        
        [SerializeField]
        private AudioSource _musicSource;
        
        [SerializeField]
        private TrackView _trackPrefab;
        
        [SerializeField]
        private Transform _tracksParent;
        
        [SerializeField] private UltEvent _OnSaveTrack;
        
        public List<GameObject> content;
        public GameObject saveWindow;
        public TMP_Text savePathText;
        
        private IObjectResolver _resolver;
        
        private List<TrackView> _trackViewsCache = new List<TrackView>();

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public override UniTask Load()
        {
            for (var index = 0; index < _musicTracks.Length; index++)
            {
                var track = _musicTracks[index];
                var currentTrack = Instantiate(_trackPrefab, _tracksParent);
                currentTrack.Initialize(_musicSource, track, index);
                _trackViewsCache.Add(currentTrack);
                _resolver.Inject(currentTrack);
            }

            foreach (var trackView in _trackViewsCache)
            {
                trackView.OnDownloadTrack += SetText;
                trackView.OnPlay += OnPlayTrack;
            }
            
            return base.Load();
        }

        private void SetText(string text)
        {
            savePathText.text = text;
            _OnSaveTrack?.Invoke();
        }

        private void OnPlayTrack(AudioClip clip)
        {
            foreach (var trackView in _trackViewsCache)
            {
                if (trackView.TrackData.MusicClip!=clip)
                {
                    trackView.PlayButton.SetActive(true);
                }
            }
        }

        private void OnDisable()
        {
            foreach (var trackView in _trackViewsCache)
            {
                trackView.Pause();
            }
        }

        private void OnDestroy()
        {
            foreach (var trackView in _trackViewsCache)
            {
                trackView.OnDownloadTrack -= SetText;
                trackView.OnPlay -= OnPlayTrack;
            }
        }
    }
}