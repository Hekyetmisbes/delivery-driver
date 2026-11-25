#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.UIKit.Editor
{
    public sealed class DDUITextureImporter : AssetPostprocessor
    {
        private const string SpriteRoot = "Assets/Resources/UI/DeliveryDriver/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot, System.StringComparison.Ordinal)) return;
            Configure((TextureImporter)assetImporter, assetPath);
        }

        [MenuItem("Tools/Delivery Driver UI/Configure Sprites")]
        public static void ConfigureAll()
        {
            string[] roots = {
                SpriteRoot + "Components",
                SpriteRoot + "Icons",
                SpriteRoot + "Backgrounds"
            };
            int count = 0;
            foreach (string root in roots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                {
                    Debug.LogWarning("DDUI: Folder not found: " + root);
                    continue;
                }
                string[] assets = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
                foreach (string guid in assets)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;
                    Configure(importer, path);
                    importer.SaveAndReimport();
                    count++;
                }
            }
            AssetDatabase.Refresh();
            Debug.Log("DDUI: configured " + count + " imported sprite(s).");
        }

        private static void Configure(TextureImporter importer, string path)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            TextureImporterSettings spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(spriteSettings);
            importer.textureCompression = TextureImporterCompression.Compressed;

            bool component = path.StartsWith(SpriteRoot + "Components/", System.StringComparison.Ordinal);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
            bool sliced = component && (baseName.StartsWith("button_") || baseName.StartsWith("panel_") ||
                       baseName.StartsWith("card_") || baseName.StartsWith("input_") ||
                       baseName.StartsWith("tab_") || baseName.StartsWith("toast_") ||
                       baseName == "modal_window" || baseName == "tooltip");
            importer.spriteBorder = sliced ? new Vector4(20f, 20f, 20f, 20f) : Vector4.zero;
        }
    }
}
#endif
