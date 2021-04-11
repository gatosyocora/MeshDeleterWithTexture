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
        private Material[] materials;
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
            materials = null;

            triangleCount = 0;
            saveFolder = "Assets/";
        }

        /// <summary>
        /// メッシュを削除する
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="deleteTexPos"></param>
        /// <param name="texture"></param>
        /// <param name="subMeshIndexInDeletedVertex"></param>
        private bool DeleteMesh(Renderer renderer, int[] deletePos, Texture2D texture, MaterialInfo matInfo)
        {

            var mesh = RendererUtility.GetMesh(renderer);
            var deletedMesh = UnityEngine.Object.Instantiate(mesh);
            var materials = renderer.sharedMaterials.ToArray();

            deletedMesh.Clear();
            deletedMesh.MarkDynamic();

            // 削除する頂点のリストを取得
            var uvs = mesh.uv.ToList();
            List<int> deleteIndexList = new List<int>();

            for (int i = 0; i < uvs.Count(); i++)
            {
                var x = (int)(Mathf.Abs(uvs[i].x % 1.0f) * texture.width);
                var y = (int)(Mathf.Abs(uvs[i].y % 1.0f) * texture.height);

                if (x == texture.width || y == texture.height) continue;

                int index = y * texture.width + x;

                if (deletePos[index] == 1)
                {
                    deleteIndexList.Add(i);
                }
            }

            // TODO: 共有されている頂点は存在しない？
            // これがないと他のサブメッシュのポリゴンも削除された
            // 他のサブメッシュで共有されている頂点は削除してはいけない
            List<int> nonDeleteVertexIndexs = new List<int>();
            for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                if (matInfo.MaterialSlotIndices.BinarySearch(subMeshIndex) < 0)
                    nonDeleteVertexIndexs.AddRange(mesh.GetIndices(subMeshIndex));
            }
            nonDeleteVertexIndexs = nonDeleteVertexIndexs.Distinct().ToList();
            nonDeleteVertexIndexs.Sort();

            // 削除する頂点のインデックスのリスト(重複なし)
            var deleteIndexListUnique
                = deleteIndexList
                    .Distinct()
                    .Where(i => nonDeleteVertexIndexs.BinarySearch(i) < 0);

            // 削除する頂点のインデックスのリスト (重複なし, 昇順)
            var deleteIndexsOrdered
                = deleteIndexListUnique
                    .ToList();
            deleteIndexsOrdered.Sort();

            // 削除する頂点がないので終了する
            if (deleteIndexsOrdered.Count == 0) return false;

            // 頂点を削除
            var nonDeleteVertices = mesh.vertices.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteWeights = mesh.boneWeights.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteNormals = mesh.normals.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteTangents = mesh.tangents.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteColors = mesh.colors.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteColor32s = mesh.colors32.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteUVs = uvs.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteUV2s = mesh.uv2.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteUV3s = mesh.uv3.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteUV4s = mesh.uv4.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();

            deletedMesh.SetVertices(nonDeleteVertices);
            deletedMesh.boneWeights = nonDeleteWeights;
            deletedMesh.normals = nonDeleteNormals;
            deletedMesh.tangents = nonDeleteTangents;
            deletedMesh.colors = nonDeleteColors;
            deletedMesh.colors32 = nonDeleteColor32s;
            deletedMesh.SetUVs(0, nonDeleteUVs);
            deletedMesh.SetUVs(1, nonDeleteUV2s);
            deletedMesh.SetUVs(2, nonDeleteUV3s);
            deletedMesh.SetUVs(3, nonDeleteUV4s);

            // サブメッシュごとにポリゴンを処理

            // 削除する頂点のインデックスのリスト（重複なし, 降順）
            var deleteIndexListUniqueDescending
                = deleteIndexListUnique
                    .OrderByDescending(value => value)
                    .ToArray();

            // Mesh.GetTrianglesでアクセスするために一旦最大値を入れる
            deletedMesh.subMeshCount = mesh.subMeshCount;

            float progressMaxCount = mesh.subMeshCount;
            float count = 0;
            int addSubMeshIndex = 0;
            bool deletedSubMesh = false;

            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                var subMeshTriangles = mesh.GetTriangles(subMeshIndex);
                // インデックスがずれるので各頂点への対応付けが必要
                // インデックスが大きいものから順に処理していく
                foreach (var deleteVerticesIndex in deleteIndexListUniqueDescending)
                {
                    for (int i = 0; i < subMeshTriangles.Count(); i += 3)
                    {
                        // ポリゴンの3つの頂点1つでも削除されるならそのポリゴンを削除する
                        // mesh.trianglesの要素数は3の倍数である必要がある
                        if (subMeshTriangles[i] == deleteVerticesIndex ||
                            subMeshTriangles[i + 1] == deleteVerticesIndex ||
                            subMeshTriangles[i + 2] == deleteVerticesIndex)
                        {
                            subMeshTriangles[i] = -1;
                            subMeshTriangles[i + 1] = -1;
                            subMeshTriangles[i + 2] = -1;
                        }
                        else
                        {
                            if (subMeshTriangles[i] > deleteVerticesIndex)
                                subMeshTriangles[i]--;
                            if (subMeshTriangles[i + 1] > deleteVerticesIndex)
                                subMeshTriangles[i + 1]--;
                            if (subMeshTriangles[i + 2] > deleteVerticesIndex)
                                subMeshTriangles[i + 2]--;
                        }
                    }
                }

                if (EditorUtility.DisplayCancelableProgressBar("Delete triangles",
                        Mathf.Floor(count / progressMaxCount * 100) + "%", count++ / progressMaxCount))
                {
                    EditorUtility.ClearProgressBar();
                    return false;
                }

                // 不要なポリゴンを削除する
                var triangleList = subMeshTriangles.Where(v => v != -1).ToArray();

                // ポリゴン数0のサブメッシュは追加しない
                if (!triangleList.Any())
                {
                    materials[subMeshIndex] = null;
                    deletedSubMesh = true;
                    continue;
                }

                deletedMesh.SetTriangles(triangleList, addSubMeshIndex++);
            }

            if (deletedSubMesh)
            {
                // ポリゴン削除の結果, ポリゴン数0になったSubMeshは含めない
                deletedMesh.subMeshCount = addSubMeshIndex;
            }

            //BindPoseをコピー
            deletedMesh.bindposes = mesh.bindposes;

            // BlendShapeを設定する
            string blendShapeName;
            float frameWeight;
            var deltaVertices = new Vector3[mesh.vertexCount];
            var deltaNormals = new Vector3[mesh.vertexCount];
            var deltaTangents = new Vector3[mesh.vertexCount];
            for (int blendshapeIndex = 0; blendshapeIndex < mesh.blendShapeCount; blendshapeIndex++)
            {
                blendShapeName = mesh.GetBlendShapeName(blendshapeIndex);
                frameWeight = mesh.GetBlendShapeFrameWeight(blendshapeIndex, 0);

                mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);

                var deltaNonDeleteVerteicesList = deltaVertices.Where((value, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
                var deltaNonDeleteNormalsList = deltaNormals.Where((value, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
                var deltaNonDeleteTangentsList = deltaTangents.Where((value, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();

                deletedMesh.AddBlendShapeFrame(blendShapeName, frameWeight,
                    deltaNonDeleteVerteicesList,
                    deltaNonDeleteNormalsList,
                    deltaNonDeleteTangentsList);
            }

            if (meshName == "") meshName = mesh.name + "_deleteMesh";
            AssetDatabase.CreateAsset(deletedMesh, AssetDatabase.GenerateUniqueAssetPath(Path.Combine(saveFolder, $"{meshName}.asset")));
            AssetDatabase.SaveAssets();

            Undo.RecordObject(renderer, "Change mesh " + deletedMesh.name);
            previousMesh = mesh;
            previousMaterials = renderer.sharedMaterials;
            RendererUtility.SetMesh(renderer, deletedMesh);

            // 削除したサブメッシュに対応したマテリアルにテクスチャを戻すためにここでおこなう
            ResetMaterialsToDefault(renderer, materials);

            if (deletedSubMesh)
            {
                // サブメッシュ削除によってマテリアルの対応を変更する必要がある
                renderer.sharedMaterials = materials.Where(m => m != null).ToArray();
            }

            EditorUtility.ClearProgressBar();

            return deletedSubMesh;
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
            materials = GetMaterials(renderer);
            matInfos = GetMaterialInfos(renderer);
            textureNames = matInfos.Select(x => x.Name).ToArray();
            meshName = StringUtility.AddKeywordToEnd(mesh.name, "_deleteMesh");
        }

        private Material[] GetMaterials(Renderer renderer) => renderer.sharedMaterials.ToArray();

        private void ResetMaterialsToDefault(Renderer renderer, Material[] materials)
        {
            if (renderer.sharedMaterials.Length != materials.Length)
            {
                throw new Exception("renderer.sharedMaterials.Length is not equal to materials.Length");
            }

            renderer.sharedMaterials = materials;
        }

        private MaterialInfo[] GetMaterialInfos(Renderer renderer)
        {
            var mats = renderer.sharedMaterials;
            var matInfos = new List<MaterialInfo>();
            var processedList = new List<string>();

            for (int matIndex = 0; matIndex < mats.Length; matIndex++)
            {
                if (!processedList.Contains(mats[matIndex].name))
                {
                    matInfos.Add(new MaterialInfo(mats[matIndex], matIndex));
                    processedList.Add(mats[matIndex].name);
                }
                else
                {
                    var infoIndex = processedList.IndexOf(mats[matIndex].name);
                    matInfos[infoIndex].AddSlotIndex(matIndex);
                }
            }

            return matInfos.ToArray();
        }

        public void OnChangeRenderer(CanvasView canvasView)
        {
            if (materials != null)
                ResetMaterialsToDefault(renderer, materials);

            if (renderer != null)
            {
                if (!(renderer is SkinnedMeshRenderer ||
                    renderer is MeshRenderer))
                {
                    EditorUtility.DisplayDialog(
                        string.Empty,
                        $"Support {nameof(SkinnedMeshRenderer)}, {nameof(MeshRenderer)}.",
                        "OK");
                    renderer = null;
                    return;
                }

                previousMesh = null;

                LoadRendererData(renderer);

                if (materials != null)
                {
                    materialInfoIndex = 0;
                    canvasView.Initialize(matInfos[materialInfoIndex], renderer);
                }
            }
        }

        public void OnChangeMaterial(CanvasView canvasView)
        {
            if (materials != null)
            {
                ResetMaterialsToDefault(renderer, materials);
                canvasView.InitializeDrawArea(matInfos[materialInfoIndex], renderer);
            }
        }

        public void SetPreviewTextureToMaterial(ref RenderTexture previewTexture)
        {
            renderer.sharedMaterials[matInfos[materialInfoIndex].MaterialSlotIndices[0]].mainTexture = previewTexture;
        }

        public void RevertMeshToPrefab(CanvasView canvasView)
        {
            ResetMaterialsToDefault(renderer, materials);
            RendererUtility.RevertMeshToPrefab(renderer);
            var mesh = RendererUtility.GetMesh(renderer);
            var uvMapTex = canvasView.uvMap.GenerateUVMap(mesh, matInfos[materialInfoIndex], matInfos[materialInfoIndex].Texture);
            canvasView.uvMap.SetUVMapTexture(uvMapTex);

            LoadRendererData(renderer);
            materialInfoIndex = 0;
            canvasView.InitializeDrawArea(matInfos[materialInfoIndex], renderer);

            previousMesh = null;
            previousMaterials = null;
        }

        public void RevertMeshToPreviously(CanvasView canvasView)
        {
            ResetMaterialsToDefault(renderer, materials);

            RendererUtility.SetMesh(renderer, previousMesh);
            previousMesh = null;
            renderer.sharedMaterials = previousMaterials;
            previousMaterials = null;

            LoadRendererData(renderer);
            materialInfoIndex = 0;
            canvasView.InitializeDrawArea(matInfos[materialInfoIndex], renderer);
        }

        public void DeleteMesh(CanvasView canvasView)
        {
            var deletePos = canvasView.GetDeleteData();
            var deletedSubMesh = DeleteMesh(renderer, deletePos, matInfos[materialInfoIndex].Texture, matInfos[materialInfoIndex]);

            LoadRendererData(renderer);

            if (deletedSubMesh)
            {
                materialInfoIndex = 0;
            }
            canvasView.InitializeDrawArea(matInfos[materialInfoIndex], renderer);
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
        public bool HasMaterials() => materials != null;

        public void Dispose()
        {
            if (renderer != null && materials != null)
            {
                ResetMaterialsToDefault(renderer, materials);
            }
        }
    }
}