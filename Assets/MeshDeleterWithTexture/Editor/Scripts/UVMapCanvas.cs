using Gatosyocora.MeshDeleterWithTexture.Models;
using Gatosyocora.MeshDeleterWithTexture.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    public class UVMapCanvas
    {
        private const string CS_VARIABLE_UVMAP = "UVMap";
        private const string CS_VARIABLE_WIDTH = "Width";
        private const string CS_VARIABLE_HEIGHT = "Height";
        private const string CS_VARIABLE_TRIANGLES = "Triangles";
        private const string CS_VARIABLE_UVS = "UVs";

        private Material editMat;

        private Texture2D uvMapTexture;
        public Color uvMapLineColor { get; private set; } = Color.black;

        public UVMapCanvas(ref Material editMat)
        {
            this.editMat = editMat;
        }

        public bool SetUVMapTexture(Renderer renderer, MaterialInfo materialInfo)
        {
            if (renderer == null || materialInfo == null)
            {
                editMat.SetTexture("_UVMap", null);
                return true;
            }

            var mesh = RendererUtility.GetMesh(renderer);

            if (mesh == null) return false;

            uvMapTexture = GenerateUVMap(mesh, materialInfo);
            editMat.SetTexture("_UVMap", uvMapTexture);
            return true;
        }

        public void SetUVMapLineColor(Color uvMapLineColor)
        {
            this.uvMapLineColor = uvMapLineColor;
            editMat.SetColor("_UVMapLineColor", uvMapLineColor);
        }

        /// <summary>
        /// UVマップを取得する
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="subMeshIndex"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        public Texture2D GenerateUVMap(Mesh mesh, MaterialInfo matInfo)
        {
            var texture = matInfo.Texture;
            var triangles = new List<int>();
            foreach (var slotIndex in matInfo.MaterialSlotIndices)
                triangles.AddRange(mesh.GetTriangles(slotIndex));
            var uvs = mesh.uv;

            if (uvs.Count() <= 0 || triangles.Count() <= 0) return null;

            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2(Mathf.Repeat(uvs[i].x, 1.0f), Mathf.Repeat(uvs[i].y, 1.0f));
            }

            ComputeShader cs = Object.Instantiate(AssetRepository.LoadCreateUVMapComputeShader());
            int kernel = cs.FindKernel("CSMain");

            var uvMapRT = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                anisoLevel = texture.anisoLevel,
                mipMapBias = texture.mipMapBias,
                filterMode = texture.filterMode,
                wrapMode = texture.wrapMode,
                wrapModeU = texture.wrapModeU,
                wrapModeV = texture.wrapModeV,
                wrapModeW = texture.wrapModeW
            };
            uvMapRT.Create();

            var triangleBuffer = new ComputeBuffer(triangles.Count(), sizeof(int));
            var uvBuffer = new ComputeBuffer(uvs.Count(), Marshal.SizeOf(typeof(Vector2)));
            triangleBuffer.SetData(triangles);
            uvBuffer.SetData(uvs);

            cs.SetTexture(kernel, CS_VARIABLE_UVMAP, uvMapRT);
            cs.SetInt(CS_VARIABLE_WIDTH, texture.width);
            cs.SetInt(CS_VARIABLE_HEIGHT, texture.height);
            cs.SetBuffer(kernel, CS_VARIABLE_TRIANGLES, triangleBuffer);
            cs.SetBuffer(kernel, CS_VARIABLE_UVS, uvBuffer);

            cs.Dispatch(kernel, triangles.Count() / 3, 1, 1);

            triangleBuffer.Release();
            uvBuffer.Release();

            Texture2D uvMapTex = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false)
            {
                name = texture.name
            };

            var original = RenderTexture.active;
            RenderTexture.active = uvMapRT;
            uvMapTex.ReadPixels(new Rect(0, 0, uvMapRT.width, uvMapRT.height), 0, 0);
            uvMapTex.Apply();
            RenderTexture.active = original;

            uvMapRT.Release();

            uvMapTexture = uvMapTex;

            return uvMapTex;
        }

        /// <summary>
        ///　UVマップを書き出す
        /// </summary>
        /// <param name="deletePos"></param>
        public void ExportUVMapTexture()
        {
            if (uvMapTexture == null) return;

            RenderTexture uvMapRT = new RenderTexture(uvMapTexture.width, uvMapTexture.height, 0, RenderTextureFormat.ARGB32);
            Material negaposiMat = new Material(Shader.Find("Gato/NegaPosi"));
            negaposiMat.SetTexture("_MaskTex", uvMapTexture);
            negaposiMat.SetFloat("_Inverse", 1);
            Graphics.Blit(null, uvMapRT, negaposiMat);

            var original = RenderTexture.active;
            RenderTexture.active = uvMapRT;
            uvMapTexture.ReadPixels(new Rect(0, 0, uvMapRT.width, uvMapRT.height), 0, 0);
            RenderTexture.active = original;

            var png = uvMapTexture.EncodeToPNG();

            var path = EditorUtility.SaveFilePanel(
                        "Save delete mask texture as PNG",
                        "Assets",
                        uvMapTexture.name + "_uv.png",
                        "png");

            if (path.Length > 0)
                File.WriteAllBytes(path, png);
        }
    }
}