using System;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class CanvasModel : ScriptableObject, IDisposable
    {
        private ComputeShader computeShader;
        public ComputeBuffer buffer;
        private int penKernelId, eraserKernelId;

        private Vector2Int textureSize;

        public void OnEnable()
        {
            computeShader = Instantiate(Resources.Load<ComputeShader>("colorchecker2"));
        }

        public CanvasModel()
        {
            penKernelId = computeShader.FindKernel("CSPen");
            eraserKernelId = computeShader.FindKernel("CSEraser");
        }

        /// <summary>
        /// 初期化する
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="previewTexture"></param>
        public void Initialize(ref Texture2D texture, ref RenderTexture previewTexture)
        {
            if (buffer != null) buffer.Release();
            buffer = new ComputeBuffer(texture.width * texture.height, sizeof(int));
            computeShader.SetBuffer(penKernelId, "Result", buffer);
            computeShader.SetBuffer(eraserKernelId, "Result", buffer);

            computeShader.SetTexture(penKernelId, "Tex", texture);
            computeShader.SetTexture(eraserKernelId, "Tex", texture);
            computeShader.SetInt("Width", texture.width);
            computeShader.SetInt("Height", texture.height);

            computeShader.SetTexture(penKernelId, "PreviewTex", previewTexture);
            computeShader.SetTexture(eraserKernelId, "PreviewTex", previewTexture);

            textureSize = new Vector2Int(texture.width, texture.height);
        }

        /// <summary>
        /// 任意の位置を削除箇所としてマーキングする
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="textureSize"></param>
        public void Mark(Vector2 pos)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = (int)pos.x;
            posArray[1 * sizeof(int)] = (int)pos.y;
            computeShader.SetInts("Pos", posArray);

            computeShader.Dispatch(penKernelId, textureSize.x / 32, textureSize.y / 32, 1);
        }

        /// <summary>
        /// 任意の位置の削除箇所としてのマーキングを取り消す
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="textureSize"></param>
        public void UnMark(Vector2 pos)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = (int)pos.x;
            posArray[1 * sizeof(int)] = (int)pos.y;
            computeShader.SetInts("Pos", posArray);

            computeShader.Dispatch(eraserKernelId, textureSize.x / 32, textureSize.y / 32, 1);
        }

        /// <summary>
        /// ペンの設定を変更する
        /// </summary>
        /// <param name="penSize"></param>
        /// <param name="penColor"></param>
        public void SetPen(int penSize, Color penColor)
        {
            computeShader.SetInt("PenSize", penSize);
            computeShader.SetVector("PenColor", penColor);
        }

        public void Dispose()
        {
            if (buffer != null) buffer.Dispose();
        }
    }
}