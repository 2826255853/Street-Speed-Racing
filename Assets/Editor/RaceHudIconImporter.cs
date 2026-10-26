using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class RaceHudIconImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/UI/Icons/")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
        importer.spriteBorder = Path.GetFileNameWithoutExtension(assetPath) == "panel"
            ? new Vector4(12, 12, 12, 12) : Vector4.zero;
    }

    public static void Prepare()
    {
        foreach (string path in Directory.GetFiles("Assets/Resources/UI/Icons", "*.png"))
            AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
    }
}
