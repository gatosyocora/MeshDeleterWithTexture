using Gatosyocora.MeshDeleterWithTexture.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    public static class MeshDeleter
    {
        private const int DELETE = -1;

        public static (Mesh, bool[]) RemoveTriangles(Mesh mesh, bool[] deletePos, Vector2Int textureSize, List<int> materialIndexList, bool showProgressBar = true)
        {
            // 削除する頂点のリストを取得
            var deleteIndexList = GetDeleteVertexIndices(mesh.uv.ToList(), deletePos, textureSize);

            if (!deleteIndexList.Any())
            {
                throw new NotFoundVerticesException();
            }

            // TODO: 共有されている頂点は存在しない？
            // これがないと他のサブメッシュのポリゴンも削除された
            // 他のサブメッシュで共有されている頂点は削除してはいけない
            List<int> nonDeleteVertexIndexs = new List<int>();
            for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                if (materialIndexList.BinarySearch(subMeshIndex) < 0)
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
            if (!deleteIndexsOrdered.Any())
            {
                throw new NotFoundVerticesException();
            }

            // 頂点を削除
            var deletedMesh = RemoveVertices(mesh, deleteIndexsOrdered);

            // サブメッシュごとにポリゴンを処理

            // 削除する頂点のインデックスのリスト（重複なし, 降順）
            var deleteIndexListUniqueDescending
                = deleteIndexListUnique
                    .OrderByDescending(value => value)
                    .ToArray();

            (var deletedMesh2, var hadDeletedSubMeshes) = RemoveTrianglesInSubMeshes(mesh, deletedMesh, deleteIndexListUniqueDescending, showProgressBar);

            // BlendShapeを設定する
            deletedMesh2 = SetupBlendShape(mesh, deletedMesh2, deleteIndexsOrdered);

            return (deletedMesh2, hadDeletedSubMeshes);
        }

        /// <summary>
        /// 削除する頂点のIndexのListを取得する
        /// </summary>
        /// <param name="uvs">各頂点のUV座標</param>
        /// <param name="deletePos">削除するかどうか(テクスチャの幅×高さのサイズ)</param>
        /// <param name="textureSize">テクスチャのサイズ</param>
        /// <returns>削除する頂点のindexのList</returns>
        private static List<int> GetDeleteVertexIndices(List<Vector2> uvs, bool[] isDeletePositions, Vector2Int textureSize)
        {
            if (isDeletePositions.Length != textureSize.x * textureSize.y)
            {
                throw new ArgumentException("isDeletePositions and (textureSize.x * textureSize.y) are not same size");
            }

            var deleteIndexList = new List<int>();

            for (int i = 0; i < uvs.Count(); i++)
            {
                var u = uvs[i].x < 0 ? 1f - Mathf.Abs(uvs[i].x % 1.0f) : uvs[i].x % 1.0f;
                var v = uvs[i].y < 0 ? 1f - Mathf.Abs(uvs[i].y % 1.0f) : uvs[i].y % 1.0f;

                var x = (int)(u * textureSize.x);
                var y = (int)(v * textureSize.y);

                if (x == textureSize.x || y == textureSize.y) continue;

                int index = y * textureSize.x + x;

                if (isDeletePositions[index])
                {
                    deleteIndexList.Add(i);
                }
            }

            return deleteIndexList;
        }

        /// <summary>
        /// indicesOrderedに含まれていないindexの要素の配列を作る
        /// </summary>
        /// <typeparam name="T">要素</typeparam>
        /// <param name="array">要素の配列</param>
        /// <param name="indicesOrdered">抽出されない要素のindexの配列</param>
        /// <returns>indicesOrderedに含まれないindexの要素の配列</returns>
        private static IEnumerable<T> ExtractMeshInfosWithIndices<T>(T[] array, List<int> indicesOrdered)
            => array.Where((v, index) => indicesOrdered.BinarySearch(index) < 0);

        private static Mesh RemoveVertices(Mesh mesh, List<int> deleteIndexsOrdered)
        {
            var deletedMesh = UnityEngine.Object.Instantiate(mesh);
            deletedMesh.Clear();
            deletedMesh.MarkDynamic();

            var nonDeleteVertices = ExtractMeshInfosWithIndices(mesh.vertices, deleteIndexsOrdered);
            var nonDeleteWeights = ExtractMeshInfosWithIndices(mesh.boneWeights, deleteIndexsOrdered);
            var nonDeleteNormals = ExtractMeshInfosWithIndices(mesh.normals, deleteIndexsOrdered);
            var nonDeleteTangents = ExtractMeshInfosWithIndices(mesh.tangents, deleteIndexsOrdered);
            var nonDeleteColors = ExtractMeshInfosWithIndices(mesh.colors, deleteIndexsOrdered);
            var nonDeleteColor32s = ExtractMeshInfosWithIndices(mesh.colors32, deleteIndexsOrdered);
            var nonDeleteUVs = ExtractMeshInfosWithIndices(mesh.uv, deleteIndexsOrdered);
            var nonDeleteUV2s = ExtractMeshInfosWithIndices(mesh.uv2, deleteIndexsOrdered);
            var nonDeleteUV3s = ExtractMeshInfosWithIndices(mesh.uv3, deleteIndexsOrdered);
            var nonDeleteUV4s = ExtractMeshInfosWithIndices(mesh.uv4, deleteIndexsOrdered);

            deletedMesh.SetVertices(nonDeleteVertices.ToList());
            deletedMesh.boneWeights = nonDeleteWeights.ToArray();
            deletedMesh.SetNormals(nonDeleteNormals.ToList());
            deletedMesh.SetTangents(nonDeleteTangents.ToList());
            deletedMesh.SetColors(nonDeleteColors.ToList());
            deletedMesh.SetColors(nonDeleteColor32s.ToList());
            deletedMesh.SetUVs(0, nonDeleteUVs.ToList());
            deletedMesh.SetUVs(1, nonDeleteUV2s.ToList());
            deletedMesh.SetUVs(2, nonDeleteUV3s.ToList());
            deletedMesh.SetUVs(3, nonDeleteUV4s.ToList());

            return deletedMesh;
        }

        private static (Mesh, bool[] hadDeletedSubMeshes) RemoveTrianglesInSubMeshes(Mesh mesh, Mesh deletedMesh, int[] deleteIndexListUniqueDescending, bool showProgressBar)
        {
            var hadDeletedSubMeshes = new bool[mesh.subMeshCount];

            // Mesh.GetTrianglesでアクセスするために一旦最大値を入れる
            deletedMesh.subMeshCount = mesh.subMeshCount;

            float progressMaxCount = mesh.subMeshCount;
            float count = 0;
            int addSubMeshIndex = 0;

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
                            subMeshTriangles[i] = DELETE;
                            subMeshTriangles[i + 1] = DELETE;
                            subMeshTriangles[i + 2] = DELETE;
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

                if (showProgressBar && EditorUtility.DisplayCancelableProgressBar("Delete triangles",
                        Mathf.Floor(count / progressMaxCount * 100) + "%", count++ / progressMaxCount))
                {
                    EditorUtility.ClearProgressBar();
                    return (null, Array.Empty<bool>());
                }

                // 不要なポリゴンを削除する
                var triangleList = subMeshTriangles.Where(v => v != DELETE).ToArray();

                // ポリゴン数0のサブメッシュは追加しない
                if (!triangleList.Any())
                {
                    hadDeletedSubMeshes[subMeshIndex] = true;
                    continue;
                }

                deletedMesh.SetTriangles(triangleList, addSubMeshIndex++);
            }

            EditorUtility.ClearProgressBar();

            if (hadDeletedSubMeshes.Any(deletedSubMesh => deletedSubMesh == true))
            {
                // ポリゴン削除の結果, ポリゴン数0になったSubMeshは含めない
                deletedMesh.subMeshCount = addSubMeshIndex;
            }

            //BindPoseをコピー
            deletedMesh.bindposes = mesh.bindposes;

            return (deletedMesh, hadDeletedSubMeshes);
        }

        private static Mesh SetupBlendShape(Mesh mesh, Mesh deletedMesh, List<int> deleteIndexsOrdered)
        {
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

                var deltaNonDeleteVerteicesList = ExtractMeshInfosWithIndices(deltaVertices, deleteIndexsOrdered);
                var deltaNonDeleteNormalsList = ExtractMeshInfosWithIndices(deltaNormals, deleteIndexsOrdered);
                var deltaNonDeleteTangentsList = ExtractMeshInfosWithIndices(deltaTangents, deleteIndexsOrdered);

                deletedMesh.AddBlendShapeFrame(blendShapeName, frameWeight,
                    deltaNonDeleteVerteicesList.ToArray(),
                    deltaNonDeleteNormalsList.ToArray(),
                    deltaNonDeleteTangentsList.ToArray());
            }

            return deletedMesh;
        }

    }
}