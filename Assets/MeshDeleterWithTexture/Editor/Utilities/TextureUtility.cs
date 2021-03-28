using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Utilities
{
    public static class TextureUtility
    {
        public static RenderTexture CopyTexture2DToRenderTexture(Texture2D texture, Vector2Int textureSize, bool isLinearColorSpace = false)
        {
            RenderTexture renderTexture;

            if (isLinearColorSpace)
                renderTexture = new RenderTexture(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            else
                renderTexture = new RenderTexture(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32);

            renderTexture.enableRandomWrite = true;
            renderTexture.anisoLevel = texture.anisoLevel;
            renderTexture.mipMapBias = texture.mipMapBias;
            renderTexture.filterMode = texture.filterMode;
            renderTexture.wrapMode = texture.wrapMode;
            renderTexture.wrapModeU = texture.wrapModeU;
            renderTexture.wrapModeV = texture.wrapModeV;
            renderTexture.wrapModeW = texture.wrapModeW;
            renderTexture.Create();

            Graphics.Blit(texture, renderTexture);
            return renderTexture;
        }

        /// <summary>
        /// 読み込んだ後の設定をおこなう
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Texture2D GenerateTextureToEditting(Texture2D originTexture)
        {

            // 書き込むために設定の変更が必要
            // isReadable = true
            // type = Default
            // format = RGBA32
            var assetPath = AssetDatabase.GetAssetPath(originTexture);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            importer.isReadable = true;
            importer.textureType = TextureImporterType.Default;
            var setting = importer.GetDefaultPlatformTextureSettings();
            setting.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(setting);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();

            Texture2D editTexture = new Texture2D(originTexture.width, originTexture.height, TextureFormat.ARGB32, false);
            editTexture.SetPixels(originTexture.GetPixels());
            editTexture.name = originTexture.name;

            editTexture.Apply();

            return editTexture;
        }
    }
}