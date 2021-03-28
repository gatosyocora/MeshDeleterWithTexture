using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class CanvasModel : IDisposable
    {
        private ComputeShader computeShader;
        public ComputeBuffer buffer;
        private int penKernelId, eraserKernelId;

        private UndoCanvas undoCanvas;
        private UVMapCanvas uvMapCanvas;

        public CanvasModel(UndoCanvas undoCanvas, UVMapCanvas uvMapCanvas)
        {
            InitComputeShader();
            this.undoCanvas = undoCanvas;
            this.uvMapCanvas = uvMapCanvas;
        }

        public void Draw(Vector2 pos, Vector2Int textureSize)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = (int)pos.x;
            posArray[1 * sizeof(int)] = (int)pos.y;
            computeShader.SetInts("Pos", posArray);

            computeShader.Dispatch(penKernelId, textureSize.x / 32, textureSize.y / 32, 1);
        }

        public void Clear(Vector2 pos, Vector2Int textureSize)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = (int)pos.x;
            posArray[1 * sizeof(int)] = (int)pos.y;
            computeShader.SetInts("Pos", posArray);

            computeShader.Dispatch(eraserKernelId, textureSize.x / 32, textureSize.y / 32, 1);
        }

        public void InitComputeShader()
        {
            computeShader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("colorchecker2"));
            penKernelId = computeShader.FindKernel("CSPen");
            eraserKernelId = computeShader.FindKernel("CSEraser");
        }

        private void InitComputeBuffer(Texture2D texture)
        {
            if (buffer != null) buffer.Release();
            buffer = new ComputeBuffer(texture.width * texture.height, sizeof(int));
            computeShader.SetBuffer(penKernelId, "Result", buffer);
            computeShader.SetBuffer(eraserKernelId, "Result", buffer);
        }

        public void SetupComputeShader(ref Texture2D texture, ref RenderTexture previewTexture)
        {
            InitComputeBuffer(texture);

            computeShader.SetTexture(penKernelId, "Tex", texture);
            computeShader.SetTexture(eraserKernelId, "Tex", texture);
            computeShader.SetInt("Width", texture.width);
            computeShader.SetInt("Height", texture.height);

            computeShader.SetTexture(penKernelId, "PreviewTex", previewTexture);
            computeShader.SetTexture(eraserKernelId, "PreviewTex", previewTexture);
        }

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