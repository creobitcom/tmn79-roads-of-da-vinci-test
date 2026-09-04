using System;
using System.IO;
using Creobit.Audio;
using Creobit.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras.Views
{
    public class TrackView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _trackName;
        
        [SerializeField] private Button _downloadButton;
        [SerializeField] private GameObject _playButton;
        public GameObject PlayButton => _playButton;
        [SerializeField] private GameObject _pauseButton;
        
        public event Action<string> OnDownloadTrack;
        public event Action<AudioClip> OnPlay;
        
        private IExtrasController _extrasController;
        private IAudioService _audioService;

        private AudioSource _musicSource;
        public TrackData TrackData { get; private set; }
        
        [Inject]
        public void Construct(IExtrasController extrasController,
            IAudioService audioController)
        {
            _extrasController = extrasController;
            _audioService = audioController;
        }
        
        public void Initialize(AudioSource source, TrackData data, int index)
        {
            _musicSource = source;
            TrackData = data;

            _trackName.text = LocalizationService.Instance.GetText(TrackData.Name);
            //_downloadButton.onClick.AddListener(() => _extrasController.SaveMusic(index));
            _downloadButton.onClick.AddListener(SaveMusic);
        }

        private void OnDestroy()
        {
            _downloadButton.onClick.RemoveListener(SaveMusic);
        }

        private void SaveMusic()
        {
            var fileName = $"{TrackData.MusicClip.name}.wav";
            
            
            string documentsPath;
            
            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
            {
                documentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Documents");
            }
            else
            {
                documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            documentsPath = Path.Combine(documentsPath, Application.productName);
            var filePath = Path.Combine(documentsPath, fileName);

            // Конвертация в WAV и сохранение
            var wavData = EncodeToWAV(TrackData.MusicClip);
            //TODO Android download assist
            File.WriteAllBytes(filePath, wavData);

            OnDownloadTrack?.Invoke($"Saved to: {filePath}");
        }
        
        private byte[] EncodeToWAV(AudioClip clip)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                // Заголовок WAV
                WriteWAVHeader(stream, clip);

                // Аудиоданные (конвертируем float в 16-bit int)
                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                var bytes = new byte[samples.Length * 2];
                var offset = 0;
                foreach (float sample in samples)
                {
                    var intSample = (short)(sample * short.MaxValue);
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
            var dataSize = clip.samples * channels * 2; // 2 байта на сэмпл

            // RIFF-заголовок
            stream.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }, 0, 4);
            stream.Write(BitConverter.GetBytes(36 + dataSize), 0, 4);
            stream.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' }, 0, 4);

            // fmt-чанк
            stream.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' }, 0, 4);
            stream.Write(BitConverter.GetBytes(16), 0, 4); // Размер чанка
            stream.Write(BitConverter.GetBytes((ushort)1), 0, 2); // PCM формат
            stream.Write(BitConverter.GetBytes((ushort)channels), 0, 2);
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
            stream.Write(BitConverter.GetBytes(sampleRate * channels * bitsPerSample / 8), 0, 4); // Байтрейт
            stream.Write(BitConverter.GetBytes((ushort)(channels * bitsPerSample / 8)), 0, 2); // Блок выравнивания
            stream.Write(BitConverter.GetBytes((ushort)bitsPerSample), 0, 2);

            // data-чанк
            stream.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }, 0, 4);
            stream.Write(BitConverter.GetBytes(dataSize), 0, 4);
        }
        
        //В TrackView не получится положить AudioBridge, поэтому
        //временное решение - вынос проигрывания кликов по кнопке сюда
        public void PlaySFX(AudioClip clip)
        {
            _audioService.PlaySfx(clip);
        }

        public void Play()
        {
            _musicSource.clip = TrackData.MusicClip;
            _musicSource.Play();
            
            OnPlay?.Invoke(TrackData.MusicClip);

            _playButton.SetActive(_musicSource.clip != TrackData.MusicClip);
            _audioService.PauseMusic();
        }

        public void Pause()
        {
            _musicSource.Pause();
            _audioService.ResumeMusic();
            _playButton.SetActive(true);
        }
    }
}