using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    public class DeleteMaskCanvas
    {
        private ComputeBuffer buffer;
        private Texture2D texture;
        private RenderTexture previewTexture;

        public DeleteMaskCanvas(ref ComputeBuffer buffer, Texture2D texture, ref RenderTexture previewTexture)
        {
            this.buffer = buffer;
            this.texture = texture;
            this.previewTexture = previewTexture;
        }

        /// <summary>
        /// マスク画像を読み込む
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="deletePos"></param>
        /// <returns></returns>
        public bool ImportDeleteMaskTexture()
        {
            // 画像ファイルを取得(png, jpg)
            var path = EditorUtility.OpenFilePanelWithFilters("Select delete mask texture", "Assets", new string[] { "Image files", "png,jpg,jpeg" });

            if (string.IsNullOrEmpty(path)) return false;

            return ApplyDeleteMaskTextureToBuffer(path);
        }

        /// <summary>
        /// マスク画像を書き出す
        /// </summary>
        /// <param name="deletePos"></param>
        public void ExportDeleteMaskTexture()
        {
            var height = texture.height;
            var width = texture.width;
            var maskTexture = new Texture2D(width, height);

            var deletePos = new int[width * height];
            buffer.GetData(deletePos);

            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    var c = (deletePos[j * width + i] == 1) ? UnityEngine.Color.black : UnityEngine.Color.white;
                    maskTexture.SetPixel(i, j, c);
                }
            }

            var png = maskTexture.EncodeToPNG();

            var path = EditorUtility.SaveFilePanel(
                        "Save delete mask texture as PNG",
                        "Assets",
                        texture.name + ".png",
                        "png");

            if (path.Length > 0)
                File.WriteAllBytes(path, png);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// マスク画像をCanvasに適用する
        /// </summary>
        /// <param name="maskTexturePath">マスク画像のパス</param>
        /// <returns></returns>
        public bool ApplyDeleteMaskTextureToBuffer(string maskTexturePath)
        {
            var fileStream = new FileStream(maskTexturePath, FileMode.Open, FileAccess.Read);
            var bin = new BinaryReader(fileStream);
            var binaryData = bin.ReadBytes((int)bin.BaseStream.Length);
            bin.Close();

            var maskTexture = new Texture2D(0, 0);
            maskTexture.LoadImage(binaryData);

            if (maskTexture == null || texture.width != maskTexture.width || texture.height != maskTexture.height) return false;

            var deletePos = new int[maskTexture.width * maskTexture.height];
            buffer.GetData(deletePos);

            for (int j = 0; j < maskTexture.height; j++)
            {
                for (int i = 0; i < maskTexture.width; i++)
                {
                    var col = maskTexture.GetPixel(i, j);
                    var isDelete = (col == UnityEngine.Color.black) ? 1 : 0;
                    deletePos[j * maskTexture.width + i] = isDelete;
                }
            }

            buffer.SetData(deletePos);

            Material negaposiMat = new Material(Shader.Find("Gato/NegaPosi"));
            negaposiMat.SetTexture("_MaskTex", maskTexture);
            negaposiMat.SetFloat("_Inverse", 0);
            Graphics.Blit(texture, previewTexture, negaposiMat);

            return true;
        }
    }
}