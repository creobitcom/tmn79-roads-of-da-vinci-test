using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras.Views;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class ExtrasController : IExtrasController
    {
        private IUIController _uiController;
        private ExtrasControllerJson _extrasControllerJson;
        private readonly List<string> _trackNames = new();

        public event Action<string> OnSaveBackground;
        
        [Inject]
        public void Construct(IUIController uiController)
        {
            _uiController = uiController;
        }
        
        public UniTask Load(ExtrasControllerJson extrasControllerJson)
        {
            _extrasControllerJson = extrasControllerJson;
            return UniTask.CompletedTask;
        }
        
        public void SaveBackground()
        {
            var documentsPath = GetDocumentsPath();
            
            var path = Path.Combine(Application.dataPath, Path.Combine(Application.streamingAssetsPath, "Background"));
            
            var wallpaperFiles = Directory.GetFiles(path, "*.*")
                .Where(file => file.ToLower().EndsWith("jpg") || file.ToLower().EndsWith("png"))
                .ToList();

            foreach (var background in wallpaperFiles)
            {
                File.Copy(background, 
                    Path.Combine(documentsPath, Path.GetFileName(background)), true);
            }
            
            SetBackgroundSaveText(documentsPath).Forget();
        }

        public void SaveMusic(int index)
        {
            var documentsPath = GetDocumentsPath();

            var path = GetAudioPath(_trackNames[index]);
            
            File.Copy(path, Path.Combine(documentsPath, _trackNames[index]), true);
            
            SetMusicSaveText(documentsPath).Forget();
        }

        private async UniTaskVoid SetBackgroundSaveText(string text)
        {
            var extrasView = (await _uiController.GetPanel(_extrasControllerJson.ExtrasPanel))
                .GetComponent<ExtrasViewRefs>();
            
            extrasView.savePathText.text = "Saved at: " + text;
            OnSaveBackground?.Invoke(extrasView.savePathText.text);
        }
        
        private async UniTaskVoid SetMusicSaveText(string text)
        {
            var musicView = (await _uiController.GetPanel(_extrasControllerJson.ExtrasMusicPanel))
                .GetComponent<MusicPanelView>();
            
            foreach (var contentItem in musicView.content)
            {
                contentItem.SetActive(false);
            }
            
            musicView.saveWindow.SetActive(true);
            musicView.savePathText.text = "Saved at: " + text;
        }
        
        private string GetAudioPath(string name)
        {
            return Path.Combine(Application.dataPath,
                Path.Combine(Application.streamingAssetsPath, Path.Combine("Audio", name)));
        }
        
        private string GetDocumentsPath()
        {
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
            
            if (!Directory.Exists(documentsPath))
            {
                Directory.CreateDirectory(documentsPath);
            }
            
            return documentsPath;
        }
    }
}