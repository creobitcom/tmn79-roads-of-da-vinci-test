using System;
using System.IO;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Editor.Windows
{
    public class SpriteCutterToolWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Sprite Cutter")]
        private static void OpenWindow()
        {
            var window = GetWindow<SpriteCutterToolWindow>();
            
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 240);
            
        }

        public SpriteCutter SpriteCutter;
    }
    
    [HideLabel]
    [Serializable]
    public class SpriteCutter
    {
        [field: SerializeField]
        [field: Required]
        [field: AssetsOnly]
        public Sprite Sprite { get; private set; }
        
        [field: SerializeField]
        [field: Required]
        [field: AssetsOnly]
        public Material Material { get; private set; }

        [field: SerializeField]
        [field: ValidateInput(nameof(ValidatePixels))]
        [field: ReadOnly] 
        public float PixelsPerUnit { get; private set; }

        [field: SerializeField]
        [field: Min(1)]
        public uint Rows { get; private set; }
        
        [field: SerializeField]
        [field: Min(1)]
        public uint Columns { get; private set; }

        [field: SerializeField]
        [field: FolderPath]
        public string PrefabSavePath { get; private set; }
        
        [field: SerializeField]
        public string PrefabName { get; private set; }

        [ShowIf(nameof(ValidateCut))]
        [Button]
        private void CutSprite()
        {
            var texture = Sprite.texture;
            
            var pieceWidth = texture.width / (float) Columns;
            
            var pieceHeight = texture.height / (float) Rows;
    
            var parentObject = new GameObject(PrefabName);
            
            var prefabDirectory = Path.Combine(PrefabSavePath, $"{Sprite.name}_Cut");
            
            var spriteDirectory = Path.Combine(prefabDirectory, "Sprites");

            if (!Directory.Exists(PrefabSavePath))
            {
                Directory.CreateDirectory(PrefabSavePath);
            }

            if (!Directory.Exists(spriteDirectory))
            {
                Directory.CreateDirectory(spriteDirectory);
            }
            
            var spriteCount = 1;
            
            for (var row = 0; row < Rows; row++)
            {
                for (var col = 0; col < Columns; col++)
                {
                    var pieceRect = new Rect(col * pieceWidth, row * pieceHeight, pieceWidth, pieceHeight);
                    
                    var pieceSprite = Sprite.Create(texture, pieceRect, new Vector2(0, 0), PixelsPerUnit);
                    
                    SaveSpriteAsPNG(pieceSprite, $"{spriteDirectory}/{spriteCount}.png");
                    
                    var pieceObject = new GameObject($"{spriteCount}");
                    
                    var renderer = pieceObject.AddComponent<SpriteRenderer>();

                    var path = spriteDirectory + $"/{spriteCount}.png";

                    var asset = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    
                    renderer.sprite = asset;
                    
                    renderer.material = Material;
                    
                    var xPos = col * (pieceWidth / PixelsPerUnit);
                    
                    var yPos = -row * (pieceHeight / PixelsPerUnit);
                    
                    pieceObject.transform.position = new Vector2(xPos, -yPos);
    
                    pieceObject.transform.SetParent(parentObject.transform);
                    
                    spriteCount++;
                }
            }
            
            var prefabPath = $"{prefabDirectory}/{PrefabName}.prefab";
            
            PrefabUtility.SaveAsPrefabAsset(parentObject, prefabPath);
            
            AssetDatabase.SaveAssets();
            
            AssetDatabase.Refresh();
            
            parentObject.AddComponent<ObjectDestroyer>().DestroyObject();
        }
        
        private void SaveSpriteAsPNG(Sprite sprite, string path)
        {
            var extractedTexture = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
            
            var pixels = sprite.texture.GetPixels(
                (int)sprite.rect.x,
                (int)sprite.rect.y,
                (int)sprite.rect.width,
                (int)sprite.rect.height
            );
            
            extractedTexture.SetPixels(pixels);
            extractedTexture.Apply();

            var pngData = extractedTexture.EncodeToPNG();
            
            if (pngData != null)
            {
                File.WriteAllBytes(path, pngData);
            }
            
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            
            AssetDatabase.Refresh();
            
            var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (textureImporter != null && textureImporter.textureType == TextureImporterType.Sprite)
            {
                textureImporter.spritePixelsPerUnit = PixelsPerUnit;
                
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                
                AssetDatabase.Refresh();
            }
        }

        private bool ValidateCut()
        {
            return Sprite != null && !string.IsNullOrEmpty(PrefabSavePath);
        }

        private bool ValidatePixels()
        {
            if (Sprite == null)
            {
                return false;
            }

            PixelsPerUnit = Sprite.pixelsPerUnit;
                
            return true;

        }
    }
}