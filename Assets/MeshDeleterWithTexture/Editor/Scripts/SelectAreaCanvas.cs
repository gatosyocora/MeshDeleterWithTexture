using Gatosyocora.MeshDeleterWithTexture.Models;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    public class SelectAreaCanvas : MonoBehaviour
    {
        private Material editMat;

        private RenderTexture selectAreaRT;

        private List<Vector4> points = new List<Vector4>();
        private Vector4 latestPoint = Vector4.one * -1;
        
        private ComputeShader cs;
        private int addPointKernelId;
        private int fillKernelId;

        public SelectAreaCanvas(ref Material editMat)
        {
            this.editMat = editMat;
        }

        public bool SetSelectAreaTexture(Renderer renderer, MaterialInfo materialInfo)
        {
            if (renderer == null || materialInfo == null)
            {
                editMat.SetTexture("_SelectTex", null);
                return true;
            }

            var texture = materialInfo.Texture;

            selectAreaRT = new RenderTexture(texture.width, texture.height, 0)
            {
                enableRandomWrite = true
            };
            selectAreaRT.Create();

            editMat.SetTexture("_SelectTex", selectAreaRT);

            SetupComputeShader(ref selectAreaRT);
            return true;
        }

        public void AddSelectAreaPoint(Vector2 pos)
        {
            cs.SetVector("PreviousPoint", latestPoint);

            var point = new Vector4(pos.x, pos.y, 0, 0);
            points.Add(point);
            latestPoint = point;
            cs.SetVector("NewPoint", point);
            cs.Dispatch(addPointKernelId, selectAreaRT.width, selectAreaRT.height, 1);
        }

        public void FillSelectArea()
        {
            var buffer = new ComputeBuffer(points.Count, Marshal.SizeOf(typeof(Vector4)));
            buffer.SetData(points);
            cs.SetBuffer(fillKernelId, "Points", buffer);
            cs.SetInt("PointCount", points.Count);
            cs.Dispatch(fillKernelId, selectAreaRT.width, selectAreaRT.height, 1);

            buffer.Dispose();
        }

        private void SetupComputeShader(ref RenderTexture renderTexture)
        {
            cs = Object.Instantiate(AssetRepository.LoadCalculateSelectAreaComputeShader());
            addPointKernelId = cs.FindKernel("CSAddPoint");
            fillKernelId = cs.FindKernel("CSFill");

            cs.SetTexture(addPointKernelId, "SelectAreaTex", renderTexture);
            cs.SetTexture(fillKernelId, "SelectAreaTex", renderTexture);
            cs.SetInt("PenSize", 5);
        }
    }
}
