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
        private const string MESH_SUFFIX = "_deleteMesh";


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

            // TODO: SubMeshが1つのときに最後のSubMeshを消すとここにたどり着いて、後者がOutOfRangeでエラーを吐く
            // Failedじゃなくて最後のメッシュ削除でここに入る可能性がある
            // sharedMaterialsがなく、ポリゴン0のMeshがついたRendererだがどうするのか
            if (matInfos == null || matInfos.Length <= 0 || matInfos[materialInfoIndex] == null)
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
            var (deletedMesh, hadDeletedSubMeshes) = MeshDeleter.RemoveTriangles(mesh, deletePos, textureSize, materialIndexList);

            if (meshName == "") meshName = mesh.name + MESH_SUFFIX;
            AssetDatabase.CreateAsset(deletedMesh, AssetDatabase.GenerateUniqueAssetPath(Path.Combine(saveFolder, $"{meshName}.asset")));
            AssetDatabase.SaveAssets();

            Undo.RecordObject(renderer, "Change mesh " + deletedMesh.name);
            previousMesh = mesh;
            previousMaterials = renderer.sharedMaterials;
            RendererUtility.SetMesh(renderer, deletedMesh);

            if (hadDeletedSubMeshes.Any(deletedSubMesh => deletedSubMesh == true))
            {
                // サブメッシュ削除によってマテリアルの対応を変更する必要がある
                renderer.sharedMaterials = materials.Where((material, index) => !hadDeletedSubMeshes[index]).ToArray();
                return true;
            }

            return false;
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
            meshName = StringUtility.AddKeywordToEnd(mesh.name, MESH_SUFFIX);
        }

        private void ResetMaterialsToDefault(Renderer renderer) 
            => RendererUtility.SetMaterials(renderer, defaultMaterials);

        private bool SetFbxReadWriteEnabledIfNeeded(Renderer renderer)
        {
            var mesh = RendererUtility.GetMesh(renderer);
            var path = AssetDatabase.GetAssetPath(mesh);

            if (!path.EndsWith(".fbx", true, null)) return false;

            var importer = ModelImporter.GetAtPath(path) as ModelImporter;
            if (importer.isReadable) return false;
            importer.isReadable = true;
            importer.SaveAndReimport();

            return true;
        }

        public void OnChangeRenderer(CanvasView canvasView, Renderer newRenderer)
        {
            if (defaultMaterials != null)
                ResetMaterialsToDefault(renderer);

            renderer = newRenderer;
            previousMesh = null;

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

                Initialize(canvasView);
            } 
            else
            {
                canvasView.InitializeDrawArea();
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

            Initialize(canvasView, false);

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

            Initialize(canvasView, false);
        }

        // TODO: Sceneファイルを保存してから実行する必要がある
        public void DeleteUnUsedMeshes()
        {
            // 本ツールで作成したMeshをSuffixを元に取得する
            var createdMeshPaths = AssetDatabase.FindAssets($"t:Mesh {MESH_SUFFIX}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid));

            // AssetsフォルダにあるAssetが使用しているAssetのパス一覧を取得する（Sceneファイルも含む）
            // 依存関係があるファイルが1つの場合、それは自分自身なので場外
            var dependenciesPaths = AssetDatabase.GetAllAssetPaths()
                .Select(assetPath => AssetDatabase.GetDependencies(assetPath, true))
                .Where(paths => paths.Length > 1)
                .ToArray();

            var foundMeshPaths = createdMeshPaths
                .Where(path => !dependenciesPaths.Any(paths => paths.Contains(path)))
                .ToArray();

            foreach (var path in foundMeshPaths)
            {
                // TODO: このファイルに対して削除処理をおこなう
                Debug.Log(path);
                //UnityEngine.Object.DestroyImmediate(AssetDatabase.LoadAssetAtPath(path));
            }
        }

        public void OnDeleteMeshButtonClicked(CanvasView canvasView)
        {
            ResetMaterialsToDefault(renderer);

            // fbxのRead Write/Enabledが有効でないとメッシュのverticesなどにアクセスできない
            SetFbxReadWriteEnabledIfNeeded(renderer);

            var deletePos = canvasView.GetDeleteData();
            var hadDeletedSubMesh = DeleteMesh(renderer, deletePos, matInfos[materialInfoIndex]);

            Initialize(canvasView, hadDeletedSubMesh);
        }

        public void SelectFolder()
        {
            saveFolder = EditorUtility.OpenFolderPanel("Select saved folder", saveFolder, "");
            var match = Regex.Match(saveFolder, @"Assets/.*");
            saveFolder = match.Value;
            if (saveFolder == "") saveFolder = "Assets/";
        }

        public bool HasTexture() => matInfos != null &&
                                    matInfos.Length > 0 &&
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