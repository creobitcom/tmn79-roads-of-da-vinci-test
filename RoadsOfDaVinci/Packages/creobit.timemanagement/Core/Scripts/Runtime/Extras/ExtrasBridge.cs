using System;
using System.IO;
using System.Linq;
using TMPro;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class ExtrasBridge : MonoBehaviour
    {
        // private IExtrasController _extrasController;
        [SerializeField] private UltEvent<string> OnSaveBackground;
        [SerializeField] private TMP_Text _savedBackgroundText;
        
        // [Inject]
        // public void Construct(IExtrasController extrasController)
        // {
            // _extrasController = extrasController;
            // _extrasController.OnSaveBackground += OnSaveBackgroundEvent;
        // }

        private void OnDestroy()
        {
            // _extrasController.OnSaveBackground -= OnSaveBackgroundEvent;
        }

        //TODO поправить во флоу
        // public void SaveBackground() => _extrasController.SaveBackground();

        // public void SaveMusic(int index) => _extrasController.SaveMusic(index);
        
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
            
            SetBackgroundSaveText(documentsPath);
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
        
        private void SetBackgroundSaveText(string text)
        {
            OnSaveBackground?.Invoke(text);
            if (_savedBackgroundText != null) 
            {
                _savedBackgroundText.text = text;
            }
        }
        
        private void OnSaveBackgroundEvent(string str)
        {
            OnSaveBackground?.Invoke(str);
            if (_savedBackgroundText != null) 
            {
                _savedBackgroundText.text = str;
            }
        }
    }
}