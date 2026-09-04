using System;
using System.IO;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using Creobit.Audio;
using Creobit.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras.Views
{
    public class ExtrasTrackItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _playButton;
        [SerializeField] private GameObject _playIcon;
        [SerializeField] private GameObject _pauseIcon;
        [SerializeField] private Button _downloadButton;

        public event Action<ExtrasTrackItemView> OnPlayRequested;
        public event Action<ExtrasTrackItemView> OnPauseRequested;
        public event Action<string> OnDownloadFinished;

        public TrackData TrackData { get; private set; }
        public int Index { get; private set; }
        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(OnPlayButtonClicked);
            }

            if (_downloadButton != null)
            {
#if MAC_APPSTORE
                _downloadButton.gameObject.SetActive(false);
#else
                _downloadButton.onClick.AddListener(SaveMusic);
#endif
            }
        }

        private void OnDestroy()
        {
            if (_playButton != null)
            {
                _playButton.onClick.RemoveListener(OnPlayButtonClicked);
            }

            if (_downloadButton != null)
            {
                _downloadButton.onClick.RemoveListener(SaveMusic);
            }
        }

        public void Initialize(TrackData data, int index)
        {
            TrackData = data;
            Index = index;

            if (_titleText != null && TrackData != null)
            {
                if (LocalizationService.Instance != null && !string.IsNullOrEmpty(TrackData.Name))
                {
                    _titleText.text = LocalizationService.Instance.GetText(TrackData.Name);
                }
                else if (TrackData.MusicClip != null)
                {
                    _titleText.text = TrackData.MusicClip.name;
                }
            }

            SetPlaying(false);
        }

        public void SetPlaying(bool isPlaying)
        {
            IsPlaying = isPlaying;

            if (_playIcon != null)
            {
                _playIcon.SetActive(!isPlaying);
            }

            if (_pauseIcon != null)
            {
                _pauseIcon.SetActive(isPlaying);
            }
        }

        private void OnPlayButtonClicked()
        {
            if (IsPlaying)
            {
                OnPauseRequested?.Invoke(this);
            }
            else
            {
                OnPlayRequested?.Invoke(this);
            }
        }

        public void SaveMusic()
        {
            if (TrackData == null || TrackData.MusicClip == null)
            {
                return;
            }

            var fileName = TrackData.MusicClip.name + ".wav";
            string documentsPath;

            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
            {
                documentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Documents");
            }
            else
            {
                documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            if (string.IsNullOrEmpty(documentsPath))
            {
                Debug.LogWarning($"[Extras] Нет доступной папки документов на {Application.platform} — трек {fileName} не сохранён.");
                return;
            }

            try
            {
                documentsPath = Path.Combine(documentsPath, Application.productName);

                if (!Directory.Exists(documentsPath))
                {
                    Directory.CreateDirectory(documentsPath);
                }

                var filePath = Path.Combine(documentsPath, fileName);
                var wavData = EncodeToWAV(TrackData.MusicClip);
                File.WriteAllBytes(filePath, wavData);

                OnDownloadFinished?.Invoke(filePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Extras] Не удалось сохранить трек {fileName} в {documentsPath}: {exception.Message}");
            }
        }

        private byte[] EncodeToWAV(AudioClip clip)
        {
            using (var stream = new MemoryStream())
            {
                WriteWAVHeader(stream, clip);

                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                var bytes = new byte[samples.Length * 2];
                var offset = 0;
                for (var i = 0; i < samples.Length; i++)
                {
                    var intSample = (short)(samples[i] * short.MaxValue);
                    BitConverter.GetBytes(intSample).CopyTo(bytes, offset);
                    offset += 2;
                }

                stream.Write(bytes, 0, bytes.Length);
                return stream.ToArray();
            }
        }

        private void WriteWAVHeader(MemoryStream stream, AudioClip clip)
        {
            var sampleRate = clip.frequency;
            var channels = clip.channels;
            var bitsPerSample = 16;
            var dataSize = clip.samples * channels * 2;

            stream.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }, 0, 4);
            stream.Write(BitConverter.GetBytes(36 + dataSize), 0, 4);
            stream.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' }, 0, 4);

            stream.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' }, 0, 4);
            stream.Write(BitConverter.GetBytes(16), 0, 4);
            stream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
            stream.Write(BitConverter.GetBytes((ushort)channels), 0, 2);
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
            stream.Write(BitConverter.GetBytes(sampleRate * channels * bitsPerSample / 8), 0, 4);
            stream.Write(BitConverter.GetBytes((ushort)(channels * bitsPerSample / 8)), 0, 2);
            stream.Write(BitConverter.GetBytes((ushort)bitsPerSample), 0, 2);

            stream.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }, 0, 4);
            stream.Write(BitConverter.GetBytes(dataSize), 0, 4);
        }
    }
}
