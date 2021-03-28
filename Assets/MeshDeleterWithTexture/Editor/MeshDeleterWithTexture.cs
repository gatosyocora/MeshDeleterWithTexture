using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using System.Runtime.InteropServices;
using Gatosyocora.MeshDeleterWithTexture.Utilities;
using Gatosyocora.MeshDeleterWithTexture.Views;

/*
 * Copyright (c) 2019 gatosyocora
 * Released under the MIT license.
 * see LICENSE.txt
 */

// MeshDeleterWithTexture v0.6.1

namespace Gatosyocora.MeshDeleterWithTexture
{
#if UNITY_EDITOR
    public class MeshDeleterWithTexture : EditorWindow
    {
        private MaterialInfo[] matInfos;
        private int materialInfoIndex = 0;

        private Renderer renderer;
        private Texture2D originTexture;
        private Texture2D texture;
        private Texture2D[] textures;
        private string[] textureNames;

        private readonly string[] deleteMaskTextureExtensions = {".png", ".jpg", ".jpeg" };

        private int triangleCount = 0;
        private string saveFolder = "Assets/";
        private string meshName;

        private Mesh previousMesh = null;
        private Material[] previousMaterials = null;

        private CanvasView canvasView;

        [MenuItem("GatoTool/MeshDeleter with Texture")]
        private static void Open()
        {
            GetWindow<MeshDeleterWithTexture>(nameof(MeshDeleterWithTexture));
        }

        private void OnEnable()
        {
            canvasView = new CanvasView();
            texture = null;
            renderer = null;
            textures = null;

            triangleCount = 0;
            saveFolder = "Assets/";
        }

        private void OnDisable()
        {
            if (renderer != null && textures != null)
            {
                RendererUtility.ResetMaterialTextures(renderer, textures);
            }

            canvasView.Dispose();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void Update()
        {
            Repaint();
        }

        private void OnGUI()
        {
            // TODO: ComputeShaderがAndroidBuildだと使えないから警告文を出す
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                DrawNotSupportBuildTarget();
                return;
            }

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                renderer = EditorGUILayout.ObjectField("Renderer", renderer, typeof(Renderer), true) as Renderer;

                if (check.changed)
                {
                    if (textures != null)
                        RendererUtility.ResetMaterialTextures(renderer, textures);

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

                        if (textures != null)
                        {
                            materialInfoIndex = 0;
                            canvasView.Initialize(matInfos[materialInfoIndex]);
                            canvasView.InitializeDrawingArea(matInfos[materialInfoIndex], renderer);
                            texture = matInfos[materialInfoIndex].Texture;
                        }
                    }
                    else
                    {
                        texture = null;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    if (texture != null)
                    {
                        canvasView.Render();
                    }
                    else
                    {
                        var size = EditorGUIUtility.currentViewWidth * 0.6f;
                        var rect = GUILayoutUtility.GetRect(size, size);
                        GUI.Box(rect, "");
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var zoomScale = EditorGUILayout.Slider("Scale", canvasView.ZoomScale, 0.1f, 1.0f);

                        if (check.changed)
                        {
                            canvasView.ZoomScale = zoomScale;
                        }

                        if (GUILayout.Button("Reset"))
                        {
                            canvasView.ResetDrawAreaOffsetAndZoom();
                        }
                    }
                }

                ToolGUI();
            }

            if (Event.current.type == EventType.KeyDown && 
                Event.current.keyCode == KeyCode.Z)
            {
                // TODO: Undo機能を一時的に閉じる
                // canvasView.undo.UndoPreviewTexture(ref previewTexture, ref buffer);
            }
        }

        private void ToolGUI()
        {
            using (new EditorGUI.DisabledGroupScope(texture == null))
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import DeleteMask"))
                    {
                        canvasView.ImportDeleteMaskTexture();
                    }
                    if (GUILayout.Button("Export DeleteMask"))
                    {
                        canvasView.ExportDeleteMaskTexture();
                        renderer.sharedMaterials[matInfos[materialInfoIndex].MaterialSlotIndices[0]].mainTexture = canvasView.previewTexture;

                        var mesh = RendererUtility.GetMesh(renderer);
                        var uvMapTex = canvasView.uvMap.GenerateUVMap(mesh, matInfos[materialInfoIndex], texture);
                        canvasView.uvMap.SetUVMapTexture(uvMapTex);
                    }
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var path = GatoGUILayout.DragAndDropableArea("Drag & Drop DeleteMaskTexture", deleteMaskTextureExtensions);

                    if (check.changed)
                    {
                        canvasView.ApplyDeleteMaskTexturetToBuffer(path);
                    }
                }

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var uvMapLineColor = EditorGUILayout.ColorField("UVMap LineColor", canvasView.uvMap.uvMapLineColor);
                        if (check.changed)
                        {
                            canvasView.uvMap.SetUVMapLineColor(uvMapLineColor);
                        }
                    }

                    if (GUILayout.Button("Export UVMap"))
                    {
                        canvasView.uvMap.ExportUVMapTexture();
                    }
                }

                GUILayout.Space(10);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    if (textures != null)
                        materialInfoIndex = EditorGUILayout.Popup("Texture (Material)", materialInfoIndex, textureNames);

                    if (check.changed)
                    {
                        if (textures != null)
                        {
                            RendererUtility.ResetMaterialTextures(renderer, textures);
                            canvasView.InitializeDrawingArea(matInfos[materialInfoIndex], renderer);
                        }
                    }
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("DrawType");
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var drawType = (DrawType)GUILayout.Toolbar((int)canvasView.drawType, Enum.GetNames(typeof(DrawType)));

                        if (check.changed)
                        {
                            canvasView.DrawTypeSetting(drawType);
                        }
                    }
                }

                EditorGUILayout.Space();

                PenEraserGUI();

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Inverse FillArea"))
                    {
                        // TODO: Undo機能を一時的に閉じる
                        // canvasView.undo.RegisterUndoTexture(previewTexture, buffer);
                        canvasView.InverseSiroKuro();
                    }

                    if (GUILayout.Button("Clear All Drawing"))
                    {
                        // TODO: Undo機能を一時的に閉じる
                        // canvasView.undo.RegisterUndoTexture(previewTexture, buffer);

                        canvasView.DrawTypeSetting(canvasView.drawType);
                        canvasView.ResetDrawArea();

                        renderer.sharedMaterials[matInfos[materialInfoIndex].MaterialSlotIndices[0]].mainTexture = canvasView.previewTexture;
                    }

                    using (new EditorGUI.DisabledGroupScope(!canvasView.undo.canUndo()))
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Undo Drawing"))
                        {
                            // TODO: Undo機能を一時的に閉じる
                            // canvasView.undo.UndoPreviewTexture(ref previewTexture);
                        }
                    }

                }

                GUILayout.Space(20);

                EditorGUILayout.LabelField("Model Information", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("Triangle Count", triangleCount + "");
                }

                GUILayout.Space(20);

                OutputMeshGUI();

                GUILayout.Space(50);

                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledGroupScope(renderer == null || !PrefabUtility.IsPartOfAnyPrefab(renderer)))
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Revert Mesh to Prefab"))
                    {
                        RendererUtility.ResetMaterialTextures(renderer, textures);
                        RendererUtility.RevertMeshToPrefab(renderer);
                        var mesh = RendererUtility.GetMesh(renderer);
                        var uvMapTex = canvasView.uvMap.GenerateUVMap(mesh, matInfos[materialInfoIndex], texture);
                        canvasView.uvMap.SetUVMapTexture(uvMapTex);

                        LoadRendererData(renderer);
                        materialInfoIndex = 0;
                        canvasView.InitializeDrawingArea(matInfos[materialInfoIndex], renderer);

                        previousMesh = null;
                        previousMaterials = null;
                    }
                }

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledGroupScope(previousMesh == null))
                    {
                        if (GUILayout.Button("Revert Mesh to previously"))
                        {
                            RendererUtility.ResetMaterialTextures(renderer, textures);

                            RendererUtility.SetMesh(renderer, previousMesh);
                            previousMesh = null;
                            renderer.sharedMaterials = previousMaterials;
                            previousMaterials = null;

                            LoadRendererData(renderer);
                            materialInfoIndex = 0;
                            canvasView.InitializeDrawingArea(matInfos[materialInfoIndex], renderer);
                        }
                    }
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("Delete Mesh"))
                {
                    var deletePos = canvasView.GetDeleteData(texture.width, texture.height);
                    var deletedSubMesh = DeleteMesh(renderer, deletePos, texture, matInfos[materialInfoIndex]);

                    LoadRendererData(renderer);

                    if (deletedSubMesh)
                    {
                        materialInfoIndex = 0;
                    }
                    canvasView.InitializeDrawingArea(matInfos[materialInfoIndex], renderer);

                    GUIUtility.ExitGUI();
                }
            }
        }

        private void PenEraserGUI()
        {
            using (new EditorGUI.DisabledGroupScope(texture == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("PenColor");

                if (GUILayout.Button("Black"))
                {
                    canvasView.PenColor = Color.black;
                }
                if (GUILayout.Button("R"))
                {
                    canvasView.PenColor = Color.red;
                }
                if (GUILayout.Button("G"))
                {
                    canvasView.PenColor = Color.green;
                }
                if (GUILayout.Button("B"))
                {
                    canvasView.PenColor = Color.blue;
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var penColor = EditorGUILayout.ColorField(canvasView.PenColor);
                    if (check.changed)
                    {
                        canvasView.PenColor = penColor;
                    }
                }
            }

            EditorGUILayout.Space();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var penSize = EditorGUILayout.IntSlider(
                                "Pen/Eraser size",
                                canvasView.PenSize,
                                1,
                                (texture == null) ? 100 : texture.width / 20);

                if (check.changed)
                {
                    canvasView.PenSize = penSize;
                }
            }
        }

        private void OutputMeshGUI()
        {
            EditorGUILayout.LabelField("Output Mesh", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("SaveFolder", saveFolder);

                    if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
                    {
                        saveFolder = EditorUtility.OpenFolderPanel("Select saved folder", saveFolder, "");
                        var match = Regex.Match(saveFolder, @"Assets/.*");
                        saveFolder = match.Value;
                        if (saveFolder == "") saveFolder = "Assets/";
                    }
                }

                meshName = EditorGUILayout.TextField("Name", meshName);
            }
        }

        private void DrawNotSupportBuildTarget()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Can't use with BuildTarget 'Android'.\nPlease switch BuildTarget to PC");
                GUILayout.FlexibleSpace();
            }
            GUILayout.FlexibleSpace();
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
            var deletedMesh = Instantiate(mesh);
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
                    for (int i = 0; i < subMeshTriangles.Count(); i+=3)
                    {
                        // ポリゴンの3つの頂点1つでも削除されるならそのポリゴンを削除する
                        // mesh.trianglesの要素数は3の倍数である必要がある
                        if (subMeshTriangles[i] == deleteVerticesIndex ||
                            subMeshTriangles[i+1] == deleteVerticesIndex ||
                            subMeshTriangles[i+2] == deleteVerticesIndex)
                        {
                            subMeshTriangles[i] = -1;
                            subMeshTriangles[i+1] = -1;
                            subMeshTriangles[i+2] = -1;
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

            if (meshName == "") meshName = mesh.name+"_deleteMesh";
            AssetDatabase.CreateAsset(deletedMesh, AssetDatabase.GenerateUniqueAssetPath(Path.Combine(saveFolder, $"{meshName}.asset")));
            AssetDatabase.SaveAssets();

            Undo.RecordObject(renderer, "Change mesh " + deletedMesh.name);
            previousMesh = mesh;
            previousMaterials = renderer.sharedMaterials;
            RendererUtility.SetMesh(renderer, deletedMesh);

            // 削除したサブメッシュに対応したマテリアルにテクスチャを戻すためにここでおこなう
            RendererUtility.ResetMaterialTextures(renderer, textures);

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
            textures = RendererUtility.GetMainTextures(renderer);
            matInfos = GetMaterialInfos(renderer);
            textureNames = matInfos.Select(x => x.Name).ToArray();
            meshName = StringUtility.AddKeywordToEnd(mesh.name, "_deleteMesh");
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
    }
#endif
}
