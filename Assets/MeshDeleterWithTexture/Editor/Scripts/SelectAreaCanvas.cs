using Gatosyocora.MeshDeleterWithTexture.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    public class SelectAreaCanvas : MonoBehaviour
    {
        private const string CS_VARIABLE_PEN_SIZE = "PenSize";
        private const string CS_VARIABLE_PREVIOUS_POINT = "PreviousPoint";
        private const string CS_VARIABLE_NEW_POINT = "NewPoint";
        private const string CS_VARIABLE_POINTS = "Points";
        private const string CS_VARIABLE_POINT_COUNT = "PointCount";
        private const string CS_VARIABLE_SELECT_AREA_TEX = "SelectAreaTex";
        private const string CS_VARIABLE_SELECT_RESULT = "Result";
        private const string CS_VARIABLE_WIDTH = "Width";
        private const string CS_VARIABLE_POINT_1 = "Point1";
        private const string CS_VARIABLE_POINT_2 = "Point2";

        private Material editMat;

        private RenderTexture selectAreaRT;
        private ComputeBuffer buffer;

        private List<Vector4> points;
        private Vector4 latestPoint;
        
        private ComputeShader cs;
        private int addPointKernelId;
        private int addLineKernelId;
        private int fillKernelId;
        private int clearKernelId;
        private int inverseFillKernelId;

        private int penSize = 1;

        public SelectAreaCanvas(ref Material editMat)
        {
            this.editMat = editMat;
        }

        public bool SetSelectAreaTexture(Renderer renderer, MaterialInfo materialInfo)
        {
            if (renderer == null || materialInfo == null)
            {
                editMat.SetTexture("_SelectTex", null);
                return false;
            }

            if (selectAreaRT != null)
            {
                selectAreaRT.Release();
            }

            var texture = materialInfo.Texture;

            selectAreaRT = new RenderTexture(texture.width, texture.height, 0)
            {
                enableRandomWrite = true
            };
            selectAreaRT.Create();

            editMat.SetTexture("_SelectTex", selectAreaRT);

            SetupComputeShader(ref selectAreaRT);
            InitalizeProperties();

            return true;
        }

        public void ApplyPenSize(int penSize)
        {
            if (cs == null) return;

            cs.SetInt(CS_VARIABLE_PEN_SIZE, penSize);
            this.penSize = penSize;
        }

        public void AddSelectAreaPoint(Vector2 pos)
        {
            var point = new Vector4(pos.x, pos.y, 0, 0);

            if (Vector4.Distance(latestPoint, point) < penSize) return;

            points.Add(point);
            cs.SetVector(CS_VARIABLE_PREVIOUS_POINT, latestPoint);
            latestPoint = point;
            cs.SetVector(CS_VARIABLE_NEW_POINT, point);
            cs.Dispatch(addPointKernelId, selectAreaRT.width / 32, selectAreaRT.height / 32, 1);
        }

        public void AddLineEnd2Start()
        {
            DrawLine(points.LastOrDefault(), points.FirstOrDefault());
        }

        public void FillSelectArea()
        {
            var buffer = new ComputeBuffer(points.Count, Marshal.SizeOf(typeof(Vector4)));
            buffer.SetData(points);
            cs.SetBuffer(fillKernelId, CS_VARIABLE_POINTS, buffer);
            cs.SetInt(CS_VARIABLE_POINT_COUNT, points.Count);
            cs.Dispatch(fillKernelId, selectAreaRT.width / 32, selectAreaRT.height / 32, 1);

            buffer.Dispose();
        }

        public void ClearSelectArea()
        {
            if (cs == null) return;

            InitalizeProperties();
            cs.Dispatch(clearKernelId, selectAreaRT.width / 32, selectAreaRT.height / 32, 1);
        }

        public bool[] GetFillArea()
        {
            var data = new int[selectAreaRT.width * selectAreaRT.height];
            buffer.GetData(data);

            ClearSelectArea();

            return data.Select(x => x == 1).ToArray();
        }

        public void InverseSelectArea()
        {
            cs.Dispatch(inverseFillKernelId, selectAreaRT.width / 32, selectAreaRT.height / 32, 1);
        }

        private void InitalizeProperties()
        {
            points = new List<Vector4>();
            latestPoint = Vector3.one * -1;
        }

        private void SetupComputeShader(ref RenderTexture renderTexture)
        {
            cs = Object.Instantiate(AssetRepository.LoadCalculateSelectAreaComputeShader());
            addPointKernelId = cs.FindKernel("CSAddPoint");
            addLineKernelId = cs.FindKernel("CSAddLine");
            fillKernelId = cs.FindKernel("CSFill");
            clearKernelId = cs.FindKernel("CSClear");
            inverseFillKernelId = cs.FindKernel("CSInverseFill");

            cs.SetTexture(addPointKernelId, CS_VARIABLE_SELECT_AREA_TEX, renderTexture);
            cs.SetTexture(addLineKernelId, CS_VARIABLE_SELECT_AREA_TEX, renderTexture);
            cs.SetTexture(fillKernelId, CS_VARIABLE_SELECT_AREA_TEX, renderTexture);
            cs.SetTexture(clearKernelId, CS_VARIABLE_SELECT_AREA_TEX, renderTexture);
            cs.SetTexture(inverseFillKernelId, CS_VARIABLE_SELECT_AREA_TEX, renderTexture);

            buffer = new ComputeBuffer(renderTexture.width * renderTexture.height, sizeof(int));
            cs.SetBuffer(addPointKernelId, CS_VARIABLE_SELECT_RESULT, buffer);
            cs.SetBuffer(addLineKernelId, CS_VARIABLE_SELECT_RESULT, buffer);
            cs.SetBuffer(fillKernelId, CS_VARIABLE_SELECT_RESULT, buffer);
            cs.SetBuffer(clearKernelId, CS_VARIABLE_SELECT_RESULT, buffer);
            cs.SetBuffer(inverseFillKernelId, CS_VARIABLE_SELECT_RESULT, buffer);

            cs.SetInt(CS_VARIABLE_WIDTH, renderTexture.width);
        }

        private void DrawLine(Vector4 a, Vector4 b)
        {
            if (a == null || b == null) return;

            cs.SetVector(CS_VARIABLE_POINT_1, a);
            cs.SetVector(CS_VARIABLE_POINT_2, b);

            cs.Dispatch(addLineKernelId, selectAreaRT.width / 32, selectAreaRT.height / 32, 1);
        }
    }
}
