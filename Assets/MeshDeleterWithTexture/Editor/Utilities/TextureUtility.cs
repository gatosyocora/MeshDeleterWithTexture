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
            Texture2D editTexture = new Texture2D(originTexture.width, originTexture.height, originTexture.format, false);
            Graphics.CopyTexture(originTexture, 0, 0, editTexture, 0, 0);
            editTexture.name = originTexture.name;

            editTexture.Apply();

            return editTexture;
        }
    }
}