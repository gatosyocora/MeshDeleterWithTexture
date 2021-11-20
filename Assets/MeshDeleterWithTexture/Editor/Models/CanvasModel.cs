using System;
using System.Linq;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class CanvasModel : ScriptableObject, IDisposable
    {
        private const string CS_VARIABLE_RESULT = "Result";
        private const string CS_VARIABLE_TEX = "Tex";
        private const string CS_VARIABLE_WIDTH = "Width";
        private const string CS_VARIABLE_HEIGHT = "Height";
        private const string CS_VARIABLE_PREVIEW_TEX = "PreviewTex";
        private const string CS_VARIABLE_POS = "Pos";
        private const string CS_VARIABLE_PREVIOUS_POS = "PreviousPos";
        private const string CS_VARIABLE_PEN_SIZE = "PenSize";
        private const string CS_VARIABLE_PEN_COLOR = "PenColor";
        private const string CS_VARIABLE_MARK_AREA_BUFFER = "MarkAreaBuffer";

        private ComputeShader computeShader;
        public ComputeBuffer buffer;
        private int penKernelId, eraserKernelId, inverseFillKernelId, markAreaKernelId;

        private Vector2Int textureSize;
        private Vector2Int latestPos;

        public void OnEnable()
        {
            computeShader = Instantiate(AssetRepository.LoadDrawComputeShader());
            penKernelId = computeShader.FindKernel("CSPen");
            eraserKernelId = computeShader.FindKernel("CSEraser");
            inverseFillKernelId = computeShader.FindKernel("CSInverseFill");
            markAreaKernelId = computeShader.FindKernel("CSMarkArea");
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
            computeShader.SetBuffer(penKernelId, CS_VARIABLE_RESULT, buffer);
            computeShader.SetBuffer(eraserKernelId, CS_VARIABLE_RESULT, buffer);
            computeShader.SetBuffer(inverseFillKernelId, CS_VARIABLE_RESULT, buffer);
            computeShader.SetBuffer(markAreaKernelId, CS_VARIABLE_RESULT, buffer);

            computeShader.SetTexture(penKernelId, CS_VARIABLE_TEX, texture);
            computeShader.SetTexture(eraserKernelId, CS_VARIABLE_TEX, texture);
            computeShader.SetTexture(inverseFillKernelId, CS_VARIABLE_TEX, texture);
            computeShader.SetTexture(markAreaKernelId, CS_VARIABLE_TEX, texture);
            computeShader.SetInt(CS_VARIABLE_WIDTH, texture.width);
            computeShader.SetInt(CS_VARIABLE_HEIGHT, texture.height);

            computeShader.SetTexture(penKernelId, CS_VARIABLE_PREVIEW_TEX, previewTexture);
            computeShader.SetTexture(eraserKernelId, CS_VARIABLE_PREVIEW_TEX, previewTexture);
            computeShader.SetTexture(inverseFillKernelId, CS_VARIABLE_PREVIEW_TEX, previewTexture);
            computeShader.SetTexture(markAreaKernelId, CS_VARIABLE_PREVIEW_TEX, previewTexture);

            textureSize = new Vector2Int(texture.width, texture.height);
            ResetLatestPos();
        }

        /// <summary>
        /// 任意の位置を削除箇所としてマーキングする
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="textureSize"></param>
        public void Mark(Vector2Int pos)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = pos.x;
            posArray[1 * sizeof(int)] = pos.y;
            computeShader.SetInts(CS_VARIABLE_POS, posArray);

            var previousPosArray = new int[2 * sizeof(int)];
            previousPosArray[0 * sizeof(int)] = latestPos.x;
            previousPosArray[1 * sizeof(int)] = latestPos.y;
            computeShader.SetInts(CS_VARIABLE_PREVIOUS_POS, previousPosArray);
            latestPos = pos;

            computeShader.Dispatch(penKernelId, textureSize.x / 32, textureSize.y / 32, 1);
        }

        /// <summary>
        /// 任意の位置の削除箇所としてのマーキングを取り消す
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="textureSize"></param>
        public void UnMark(Vector2Int pos)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = pos.x;
            posArray[1 * sizeof(int)] = pos.y;
            computeShader.SetInts(CS_VARIABLE_POS, posArray);

            var previousPosArray = new int[2 * sizeof(int)];
            previousPosArray[0 * sizeof(int)] = latestPos.x;
            previousPosArray[1 * sizeof(int)] = latestPos.y;
            computeShader.SetInts(CS_VARIABLE_PREVIOUS_POS, previousPosArray);
            latestPos = pos;

            computeShader.Dispatch(eraserKernelId, textureSize.x / 32, textureSize.y / 32, 1);
        }

        /// <summary>
        /// ペンの設定を変更する
        /// </summary>
        /// <param name="penSize"></param>
        /// <param name="penColor"></param>
        public void SetPen(int penSize, Color penColor)
        {
            computeShader.SetInt(CS_VARIABLE_PEN_SIZE, penSize);
            computeShader.SetVector(CS_VARIABLE_PEN_COLOR, penColor);
        }

        public void InverseCanvasMarks()
        {
            computeShader.Dispatch(inverseFillKernelId, textureSize.x / 32, textureSize.y / 32, 1);
        }

        public void MarkArea(bool[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(int));
            buffer.SetData(data.Select(x => x ? 1 : 0).ToArray());
            computeShader.SetBuffer(markAreaKernelId, CS_VARIABLE_MARK_AREA_BUFFER, buffer);
            computeShader.Dispatch(markAreaKernelId, textureSize.x / 32, textureSize.y / 32, 1);

            buffer.Dispose();
        }

        public void ResetLatestPos()
        {
            latestPos = new Vector2Int(-1, -1);
        }

        public void Dispose()
        {
            if (buffer != null) buffer.Dispose();
        }
    }
}