using Gatosyocora.MeshDeleterWithTexture.Utilities;
using Gatosyocora.MeshDeleterWithTexture.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class MeshDeleterWithTextureModel : IDisposable
    {
        private MaterialInfo[] matInfos;
        public int materialInfoIndex = 0;

        public Renderer renderer;
        private Material[] defaultMaterials;
        public string[] textureNames;
        public Texture2D Texture => matInfos[materialInfoIndex].Texture;

        public int triangleCount = 0;
        public string saveFolder = "Assets/";
        public string meshName;

        private Mesh previousMesh = null;
        private Material[] previousMaterials = null;

        public MaterialInfo currentMaterialInfo => matInfos[materialInfoIndex];

        public MeshDeleterWithTextureModel()
        {
            renderer = null;
            defaultMaterials = null;

            triangleCount = 0;
            saveFolder = "Assets/";
        }

        private void Initialize(CanvasView canvasView, bool initializeMaterialInfoIndex = true)
        {
            LoadRendererData(renderer);

            if (initializeMaterialInfoIndex)
            {
                materialInfoIndex = 0;
            }

            if (matInfos == null || matInfos[materialInfoIndex] == null)
            {
                throw new NullReferenceException("Failed to load Material");
            }

            canvasView.Initialize(matInfos[materialInfoIndex], renderer);
        }

        /// <summary>
        /// メッシュを削除する
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="deleteTexPos"></param>
        /// <param name="texture"></param>
        /// <param name="subMeshIndexInDeletedVertex"></param>
        private bool DeleteMesh(Renderer renderer, bool[] deletePos, MaterialInfo matInfo)
        {
            var texture = matInfo.Texture;
            var materialIndexList = matInfo.MaterialSlotIndices;

            var mesh = RendererUtility.GetMesh(renderer);
            var materials = renderer.sharedMaterials.ToArray();
            var textureSize = new Vector2Int(texture.width, texture.height);
            var (deletedMesh, deletedSubMeshes) = MeshDeleter.RemoveTriangles(mesh, deletePos, textureSize, materialIndexList);

            if (meshName == "") meshName = mesh.name + "_deleteMesh";
            AssetDatabase.CreateAsset(deletedMesh, AssetDatabase.GenerateUniqueAssetPath(Path.Combine(saveFolder, $"{meshName}.asset")));
            AssetDatabase.SaveAssets();

            Undo.RecordObject(renderer, "Change mesh " + deletedMesh.name);
            previousMesh = mesh;
            previousMaterials = renderer.sharedMaterials;
            RendererUtility.SetMesh(renderer, deletedMesh);

            if (deletedSubMeshes.Any())
            {
                // サブメッシュ削除によってマテリアルの対応を変更する必要がある
                renderer.sharedMaterials = materials.Where((material, index) => !deletedSubMeshes[index]).ToArray();
            }

            return deletedSubMeshes.Any();
        }

        /// <summary>
        /// Rendererから必要な情報を取得
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="mesh"></param>
        private void LoadRendererData(Renderer renderer)
        {
            var mesh = RendererUtility.GetMesh(renderer);
            if (mesh == null) return;
            triangleCount = RendererUtility.GetMeshTriangleCount(mesh);
            saveFolder = RendererUtility.GetMeshPath(mesh);
            defaultMaterials = RendererUtility.GetMaterials(renderer);
            matInfos = RendererUtility.GetMaterialInfos(renderer);
            textureNames = matInfos.Select(x => x.Name).ToArray();
            meshName = StringUtility.AddKeywordToEnd(mesh.name, "_deleteMesh");
        }

        private void ResetMaterialsToDefault(Renderer renderer) 
            => RendererUtility.SetMaterials(renderer, defaultMaterials);

        public void OnChangeRenderer(CanvasView canvasView, Renderer newRenderer)
        {
            if (defaultMaterials != null)
                ResetMaterialsToDefault(renderer);

            renderer = newRenderer;

            if (newRenderer != null)
            {
                if (!(newRenderer is SkinnedMeshRenderer ||
                    newRenderer is MeshRenderer))
                {
                    EditorUtility.DisplayDialog(
                        string.Empty,
                        $"Support {nameof(SkinnedMeshRenderer)}, {nameof(MeshRenderer)}.",
                        "OK");
                    renderer = null;
                    return;
                }

                previousMesh = null;

                Initialize(canvasView);
            }
        }

        public void OnChangeMaterial(CanvasView canvasView)
        {
            if (defaultMaterials != null)
            {
                ResetMaterialsToDefault(renderer);
                canvasView.InitializeDrawArea(matInfos[materialInfoIndex], renderer);
            }
        }

        public void SetPreviewTextureToMaterial(ref RenderTexture previewTexture)
        {
            renderer.sharedMaterials[matInfos[materialInfoIndex].MaterialSlotIndices[0]].mainTexture = previewTexture;
        }

        public void RevertMeshToPrefab(CanvasView canvasView)
        {
            ResetMaterialsToDefault(renderer);
            RendererUtility.RevertMeshToPrefab(renderer);

            canvasView.uvMap.SetUVMapTexture(renderer, matInfos[materialInfoIndex]);

            Initialize(canvasView);

            previousMesh = null;
            previousMaterials = null;
        }

        public void RevertMeshToPreviously(CanvasView canvasView)
        {
            ResetMaterialsToDefault(renderer);

            RendererUtility.SetMesh(renderer, previousMesh);
            previousMesh = null;
            renderer.sharedMaterials = previousMaterials;
            previousMaterials = null;

            Initialize(canvasView);
        }

        public void DeleteMesh(CanvasView canvasView)
        {
            ResetMaterialsToDefault(renderer);

            var deletePos = canvasView.GetDeleteData();
            var deletedSubMesh = DeleteMesh(renderer, deletePos, matInfos[materialInfoIndex]);

            Initialize(canvasView, deletedSubMesh);
        }

        public void SelectFolder()
        {
            saveFolder = EditorUtility.OpenFolderPanel("Select saved folder", saveFolder, "");
            var match = Regex.Match(saveFolder, @"Assets/.*");
            saveFolder = match.Value;
            if (saveFolder == "") saveFolder = "Assets/";
        }

        public bool HasTexture() => matInfos != null &&
                                    materialInfoIndex >= 0 && 
                                    matInfos[materialInfoIndex] != null && 
                                    matInfos[materialInfoIndex].Texture != null;
        public bool HasPreviousMesh() => previousMesh != null;
        public bool HasMaterials() => defaultMaterials != null;

        public void Dispose()
        {
            if (renderer != null && defaultMaterials != null)
            {
                ResetMaterialsToDefault(renderer);
            }
        }
    }
}