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
        // 同じマテリアルが設定されたサブメッシュがどれかを管理するためのクラス
        public class MaterialInfo
        {
            public Texture2D Texture { get; private set; }
            public List<int> MaterialSlotIndices { get; private set; }
            public string Name { get; private set; }

            public MaterialInfo(Material mat, int slotIndex)
            {
                MaterialSlotIndices = new List<int>();
                AddSlotIndex(slotIndex);
                Name = mat.name;
                Texture = RendererUtility.GetMainTexture(mat);
            }

            public void AddSlotIndex(int index)
            {
                MaterialSlotIndices.Add(index);
            }
        }

        private MaterialInfo[] matInfos;
        private int materialInfoIndex = 0;

        private Renderer renderer;
        private Texture2D originTexture;
        private Texture2D texture;
        private Texture2D[] textures;
        private string[] textureNames;

        private Color penColor = Color.black;
        private int penSize = 20;
        private float zoomScale = 1;
        private Vector4 textureOffset = Vector4.zero;
        private Color uvMapLineColor = Color.black;

        private static Material editMat;

        #region compute shader variable

        private ComputeShader computeShader;
        private ComputeBuffer buffer;
        private int penKernelId, eraserKernelId;
        private RenderTexture previewTexture;
        private Texture2D uvMapTex;

        #endregion

        private readonly string[] deleteMaskTextureExtensions = {".png", ".jpg", ".jpeg" };

        private bool isDrawing = false;

        private bool isLinearColorSpace = false;

        private enum DRAW_TYPES
        {
            PEN,
            ERASER,
        };

        private DRAW_TYPES drawType;

        private RenderTexture[] undoTextures;
        private int[][] undoBuffers;
        private int undoIndex = 0;
        private const int MAX_UNDO_COUNT = 10;

        private int triangleCount = 0;
        private string saveFolder = "Assets/";
        private string meshName;

        private Mesh previousMesh = null;
        private Material[] previousMaterials = null;

        [MenuItem("GatoTool/MeshDeleter with Texture")]
        private static void Open()
        {
            GetWindow<MeshDeleterWithTexture>(nameof(MeshDeleterWithTexture));
        }

        private void OnEnable()
        {
            editMat = Resources.Load<Material>("TextureEditMat");
            texture = null;
            renderer = null;
            textures = null;

            undoTextures = new RenderTexture[MAX_UNDO_COUNT];
            undoBuffers = new int[MAX_UNDO_COUNT][];
            undoIndex = -1;
            
            drawType = DRAW_TYPES.PEN;

            triangleCount = 0;
            saveFolder = "Assets/";

            editMat.SetInt("_PointNum", 0);

            isLinearColorSpace = (PlayerSettings.colorSpace == ColorSpace.Linear);
            
            if (isLinearColorSpace)
                editMat.SetFloat("_ApplyGammaCorrection", 1);
            else
                editMat.SetFloat("_ApplyGammaCorrection", 0);

            InitComputeShader();
        }

        private void OnDisable()
        {
            if (renderer != null && textures != null)
            {
                RendererUtility.ResetMaterialTextures(renderer, textures);
            }

            if (buffer != null) buffer.Dispose();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
                            InitializeDrawingArea(materialInfoIndex);
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
                        CanvasGUI();
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
                        zoomScale = EditorGUILayout.Slider("Scale", zoomScale, 0.1f, 1.0f);

                        if (check.changed)
                        {
                            ApplyTextureZoomScale(ref editMat, zoomScale);
                        }

                        if (GUILayout.Button("Reset"))
                        {
                            ResetDrawAreaOffsetAndZoom();
                        }
                    }
                }

                ToolGUI();
            }

            if (Event.current.type == EventType.KeyDown && 
                Event.current.keyCode == KeyCode.Z)
            {
                UndoPreviewTexture(ref previewTexture);
            }
        }

        private void CanvasGUI()
        {
            var width = EditorGUIUtility.currentViewWidth * 0.6f;
            var height = width * texture.height / texture.width;
            EventType mouseEventType = 0;
            Rect rect = new Rect(0, 0, 0, 0);
            var delta = GatoGUILayout.MiniMonitor(previewTexture, width, height, ref rect, ref mouseEventType, true);
            
            if (rect.Contains(Event.current.mousePosition))
            {
                // テクスチャの拡大縮小機能
                if (mouseEventType == EventType.ScrollWheel)
                {
                    zoomScale += Mathf.Sign(delta.y) * 0.1f;

                    if (zoomScale > 1) zoomScale = 1;
                    else if (zoomScale < 0.1f) zoomScale = 0.1f;

                    // 縮小ではOffsetも中心に戻していく
                    if (Mathf.Sign(delta.y) > 0)
                    {
                        if (zoomScale < 1)
                            textureOffset *= zoomScale;
                        else
                            textureOffset = Vector4.zero;

                        editMat.SetVector("_Offset", textureOffset);
                    }
                    
                    editMat.SetFloat("_TextureScale", zoomScale);
                }
                // テクスチャの表示箇所を移動する機能
                else if (Event.current.button == 1 &&
                    mouseEventType == EventType.MouseDrag)
                {
                    if (delta.x != 0)
                    {
                        textureOffset.x -= delta.x / rect.width;

                        if (textureOffset.x > 1 - zoomScale)
                            textureOffset.x = 1 - zoomScale;
                        else if (textureOffset.x < -(1 - zoomScale))
                            textureOffset.x = -(1 - zoomScale);
                    }

                    if (delta.y != 0)
                    {
                        textureOffset.y += delta.y / rect.height;

                        if (textureOffset.y > 1 - zoomScale)
                            textureOffset.y = 1 - zoomScale;
                        else if (textureOffset.y < -(1 - zoomScale))
                            textureOffset.y = -(1 - zoomScale);
                    }

                    editMat.SetVector("_Offset", textureOffset);
                }


                var pos = ConvertWindowPosToTexturePos(texture, Event.current.mousePosition, rect);
                
                if (drawType == DRAW_TYPES.PEN || drawType == DRAW_TYPES.ERASER)
                {
                    var uvPos = ConvertTexturePosToUVPos(texture, pos);
                    editMat.SetVector("_CurrentPos", new Vector4(uvPos.x, uvPos.y, 0, 0));

                    if (Event.current.type == EventType.MouseDown &&
                        Event.current.button == 0 &&
                        !isDrawing)
                    {
                        RegisterUndoTexture(previewTexture);
                        isDrawing = true;
                    }
                    else if (Event.current.type == EventType.MouseUp &&
                        Event.current.button == 0 &&
                        isDrawing)
                    {
                        isDrawing = false;
                    }

                    if (isDrawing)
                    {
                        if (drawType == DRAW_TYPES.PEN)
                            DrawOnTexture(pos);
                        else
                            ClearOnTexture(pos);
                    }
                }

                Repaint();
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
                        ImportDeleteMaskTexture(ref texture, ref buffer, ref previewTexture);
                    }
                    if (GUILayout.Button("Export DeleteMask"))
                    {
                        ExportDeleteMaskTexture(buffer, originTexture);
                        renderer.sharedMaterials[matInfos[materialInfoIndex].MaterialSlotIndices[0]].mainTexture = previewTexture;

                        var mesh = RendererUtility.GetMesh(renderer);
                        uvMapTex = GetUVMap(mesh, matInfos[materialInfoIndex], texture);
                        editMat.SetTexture("_UVMap", uvMapTex);
                        editMat.SetColor("_UVMapLineColor", uvMapLineColor);
                    }
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var path = GatoGUILayout.DragAndDropableArea("Drag & Drop DeleteMaskTexture", deleteMaskTextureExtensions);

                    if (check.changed)
                    {
                        ApplyDeleteMaskTexturetToBuffer(ref texture, ref buffer, ref previewTexture, path);
                    }
                }

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        uvMapLineColor = EditorGUILayout.ColorField("UVMap LineColor", uvMapLineColor);
                        if (check.changed)
                        {
                            editMat.SetColor("_UVMapLineColor", uvMapLineColor);
                        }
                    }

                    if (GUILayout.Button("Export UVMap"))
                    {
                        ExportUVMapTexture(uvMapTex);
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
                            InitializeDrawingArea(materialInfoIndex);
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
                        drawType = (DRAW_TYPES)GUILayout.Toolbar((int)drawType, Enum.GetNames(typeof(DRAW_TYPES)));

                        if (check.changed)
                        {
                            DrawTypeSetting();
                        }
                    }
                }

                EditorGUILayout.Space();

                if (drawType == DRAW_TYPES.PEN || drawType == DRAW_TYPES.ERASER)
                {
                    PenEraserGUI();
                }

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Inverse FillArea"))
                    {
                        RegisterUndoTexture(previewTexture);
                        InverseSiroKuro(ref buffer, texture, ref previewTexture);
                    }

                    if (GUILayout.Button("Clear All Drawing"))
                    {
                        RegisterUndoTexture(previewTexture);

                        DrawTypeSetting();
                        ResetDrawArea(texture, ref editMat, ref previewTexture);
                        SetupComputeShader(ref texture, ref previewTexture);

                        renderer.sharedMaterials[matInfos[materialInfoIndex].MaterialSlotIndices[0]].mainTexture = previewTexture;
                    }

                    using (new EditorGUI.DisabledGroupScope(undoIndex == -1))
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Undo Drawing"))
                        {
                            UndoPreviewTexture(ref previewTexture);
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
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Revert Mesh to Prefab"))
                    {
                        RendererUtility.RevertMeshToPrefab(renderer);
                        var mesh = RendererUtility.GetMesh(renderer);
                        uvMapTex = GetUVMap(mesh, matInfos[materialInfoIndex], texture);
                        editMat.SetTexture("_UVMap", uvMapTex);
                        editMat.SetColor("_UVMapLineColor", uvMapLineColor);
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
                            RendererUtility.SetMesh(renderer, previousMesh);
                            previousMesh = null;
                            renderer.sharedMaterials = previousMaterials;
                            previousMaterials = null;

                            var mesh = RendererUtility.GetMesh(renderer);
                            uvMapTex = GetUVMap(mesh, matInfos[materialInfoIndex], texture);
                            editMat.SetTexture("_UVMap", uvMapTex);
                            editMat.SetColor("_UVMapLineColor", uvMapLineColor);
                        }
                    }
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("Delete Mesh"))
                {
                    DeleteMesh(renderer, buffer, texture, matInfos[materialInfoIndex]);

                    LoadRendererData(renderer);

                    materialInfoIndex = 0;
                    InitializeDrawingArea(materialInfoIndex);

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
                    penColor = UnityEngine.Color.black;
                    SetupDrawing(penSize, penColor, texture);
                }
                if (GUILayout.Button("R"))
                {
                    penColor = UnityEngine.Color.red;
                    SetupDrawing(penSize, penColor, texture);
                }
                if (GUILayout.Button("G"))
                {
                    penColor = UnityEngine.Color.green;
                    SetupDrawing(penSize, penColor, texture);
                }
                if (GUILayout.Button("B"))
                {
                    penColor = UnityEngine.Color.blue;
                    SetupDrawing(penSize, penColor, texture);
                }
                penColor = EditorGUILayout.ColorField(penColor);
            }

            EditorGUILayout.Space();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var maxValue = (texture == null) ? 100 : texture.width / 20;
                penSize = EditorGUILayout.IntSlider("Pen/Eraser size", penSize, 1, maxValue);

                if (check.changed)
                    SetupDrawing(penSize, penColor, texture);
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
        private void DeleteMesh(Renderer renderer, ComputeBuffer computeBuffer, Texture2D texture, MaterialInfo matInfo)
        {

            var mesh = RendererUtility.GetMesh(renderer);
            var deletedMesh = Instantiate(mesh);
            var materials = renderer.sharedMaterials.ToArray();

            deletedMesh.Clear();
            deletedMesh.MarkDynamic();

            // 削除する頂点のリストを取得
            var uvs = mesh.uv.ToList();
            List<int> deleteIndexList = new List<int>();

            var deletePos = new int[texture.width * texture.height];
            computeBuffer.GetData(deletePos);

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
            if (deleteIndexsOrdered.Count == 0) return;

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
                    return;
                }

                // 不要なポリゴンを削除する
                var triangleList = subMeshTriangles.Where(v => v != -1).ToArray();

                // ポリゴン数0のサブメッシュは追加しない
                if (!triangleList.Any())
                {
                    materials[subMeshIndex] = null;
                    continue;
                }

                deletedMesh.SetTriangles(triangleList, addSubMeshIndex++);
            }

            // ポリゴン削除の結果, ポリゴン数0になったSubMeshは含めない
            deletedMesh.subMeshCount = addSubMeshIndex;

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
            renderer.sharedMaterials = materials.Where(m => m != null).ToArray();

            EditorUtility.ClearProgressBar();
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
            meshName = mesh.name + "_deleteMesh";
        }

        /// <summary>
        /// DrawAreaを初期化
        /// </summary>
        /// <param name="index"></param>
        /// <param name="mesh"></param>
        private void InitializeDrawingArea(int index)
        {
            originTexture = matInfos[index].Texture;
            if (originTexture != null)
            {
                texture = LoadSettingToTexture(originTexture);

                DrawTypeSetting();
                ResetDrawArea(texture, ref editMat, ref previewTexture);
                SetupComputeShader(ref texture, ref previewTexture);

                var mesh = RendererUtility.GetMesh(renderer);
                if (mesh != null)
                {
                    uvMapTex = GetUVMap(mesh, matInfos[materialInfoIndex], texture);
                    editMat.SetTexture("_UVMap", uvMapTex);
                    editMat.SetColor("_UVMapLineColor", uvMapLineColor);
                }

                // TODO: _MainTexが存在しないマテリアルは違うやつに入れないといけない
                renderer.sharedMaterials[matInfos[materialInfoIndex].MaterialSlotIndices[0]].mainTexture = previewTexture;
            }
            ResetDrawAreaOffsetAndZoom();
        }

        /// <summary>
        /// ペン
        /// </summary>
        /// <param name="pos"></param>
        private void DrawOnTexture(Vector2 pos)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = (int)pos.x;
            posArray[1 * sizeof(int)] = (int)pos.y;
            computeShader.SetInts("Pos", posArray);

            computeShader.Dispatch(penKernelId, texture.width / 32, texture.height / 32, 1);

            Repaint();
        }

        /// <summary>
        /// 消しゴム
        /// </summary>
        /// <param name="pos"></param>
        private void ClearOnTexture(Vector2 pos)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = (int)pos.x;
            posArray[1 * sizeof(int)] = (int)pos.y;
            computeShader.SetInts("Pos", posArray);

            computeShader.Dispatch(eraserKernelId, texture.width / 32, texture.height / 32, 1);

            Repaint();
        }

        private void SetupDrawing(int penSize, Color penColor, Texture2D texture)
        {
            computeShader.SetInt("PenSize", penSize);
            computeShader.SetVector("PenColor", penColor);
            editMat.SetFloat("_PenSize", penSize / (float)texture.width);
        }
        
        /// <summary>
        /// ウィンドウの座標をテクスチャの座標に変換
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="windowPos"></param>
        /// <param name="texX"></param>
        /// <param name="texY"></param>
        private Vector2 ConvertWindowPosToTexturePos(Texture2D texture, Vector2 windowPos, Rect rect)
        {
            float raito = texture.width / rect.width;

            var texX = (int)((windowPos.x - rect.position.x) * raito);
            var texY = texture.height - (int)((windowPos.y - rect.position.y) * raito);

            return ScaleOffset(texture, new Vector2(texX, texY));
        }

        private Vector2 ScaleOffset(Texture2D texture, Vector2 pos)
        {
            var x = (texture.width/2 * (1 - zoomScale) + textureOffset.x * texture.width/2) + pos.x * zoomScale;
            var y = (texture.height/2 * (1 - zoomScale) + textureOffset.y * texture.height/2) + pos.y * zoomScale;
            return new Vector2(x, y);
        }

        private Vector2 ConvertTexturePosToUVPos(Texture2D texture, Vector2 texturePos)
        {
            return new Vector2(texturePos.x / (float)texture.width, texturePos.y / (float)texture.height);
        }

        /// <summary>
        /// 読み込んだ後の設定をおこなう
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        private Texture2D LoadSettingToTexture(Texture2D originTexture)
        {

            // 書き込むために設定の変更が必要
            // isReadable = true
            // type = Default
            // format = RGBA32
            var assetPath = AssetDatabase.GetAssetPath(originTexture);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            importer.isReadable = true;
            importer.textureType = TextureImporterType.Default;
            var setting = importer.GetDefaultPlatformTextureSettings();
            setting.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(setting);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            
            Texture2D editTexture = new Texture2D(originTexture.width, originTexture.height, TextureFormat.ARGB32, false);
            editTexture.SetPixels(originTexture.GetPixels());
            editTexture.name = originTexture.name;

            editTexture.Apply();

            return editTexture;
        }

        /// <summary>
        /// マスク画像を読み込む
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="deletePos"></param>
        /// <returns></returns>
        private bool ImportDeleteMaskTexture(ref Texture2D texture, ref ComputeBuffer computeBuffer, ref RenderTexture renderTexture)
        {
            // 画像ファイルを取得(png, jpg)
            var path = EditorUtility.OpenFilePanelWithFilters("Select delete mask texture", "Assets", new string[]{"Image files", "png,jpg,jpeg"});

            if (string.IsNullOrEmpty(path)) return false;

            return ApplyDeleteMaskTexturetToBuffer(ref texture, ref computeBuffer, ref renderTexture, path);
        }

        /// <summary>
        /// マスク画像を書き出す
        /// </summary>
        /// <param name="deletePos"></param>
        private void ExportDeleteMaskTexture(ComputeBuffer computeBuffer, Texture2D texture)
        {
            var height = texture.height;
            var width = texture.width;
            var maskTexture = new Texture2D(width, height);

            var deletePos = new int[texture.width * texture.height];
            computeBuffer.GetData(deletePos);

            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    var c = (deletePos[j * width + i] == 1) ? UnityEngine.Color.black : UnityEngine.Color.white;
                    maskTexture.SetPixel(i, j, c);
                }
            }

            var png = maskTexture.EncodeToPNG();

            var path = EditorUtility.SaveFilePanel(
                        "Save delete mask texture as PNG",
                        "Assets",
                        texture.name + ".png",
                        "png");

            if (path.Length > 0)
                File.WriteAllBytes(path, png);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private bool ApplyDeleteMaskTexturetToBuffer(ref Texture2D texture, ref ComputeBuffer computeBuffer, ref RenderTexture renderTexture, string maskTexturePath)
        {
            var fileStream = new FileStream(maskTexturePath, FileMode.Open, FileAccess.Read);
            var bin = new BinaryReader(fileStream);
            var binaryData = bin.ReadBytes((int)bin.BaseStream.Length);
            bin.Close();

            var maskTexture = new Texture2D(0, 0);
            maskTexture.LoadImage(binaryData);

            if (maskTexture == null || texture.width != maskTexture.width || texture.height != maskTexture.height) return false;

            var deletePos = new int[maskTexture.width * maskTexture.height];
            computeBuffer.GetData(deletePos);

            for (int j = 0; j < maskTexture.height; j++)
            {
                for (int i = 0; i < maskTexture.width; i++)
                {
                    var col = maskTexture.GetPixel(i, j);
                    var isDelete = (col == UnityEngine.Color.black) ? 1 : 0;
                    deletePos[j * maskTexture.width + i] = isDelete;
                }
            }

            computeBuffer.SetData(deletePos);

            Material negaposiMat = new Material(Shader.Find("Gato/NegaPosi"));
            negaposiMat.SetTexture("_MaskTex", maskTexture);
            negaposiMat.SetFloat("_Inverse", 0);
            Graphics.Blit(texture, renderTexture, negaposiMat);

            Repaint();

            return true;
        }

        private void ResetDrawAreaOffsetAndZoom()
        {
            textureOffset = Vector4.zero;
            editMat.SetVector("_Offset", textureOffset);
            zoomScale = 1;
            ApplyTextureZoomScale(ref editMat, zoomScale);
        }

        private void ApplyTextureZoomScale(ref Material mat, float scale)
        {
            mat.SetFloat("_TextureScale", scale);
            
            Repaint();
        }


        #region compute shader
        private void InitComputeShader()
        {
            computeShader = Instantiate(Resources.Load<ComputeShader>("colorchecker2")) as ComputeShader;
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

        private void SetupComputeShader(ref Texture2D texture, ref RenderTexture previewTexture)
        {
            InitComputeBuffer(texture);

            computeShader.SetTexture(penKernelId, "Tex", texture);
            computeShader.SetTexture(eraserKernelId, "Tex", texture);
            computeShader.SetInt("Width", texture.width);
            computeShader.SetInt("Height", texture.height);

            computeShader.SetTexture(penKernelId, "PreviewTex", previewTexture);
            computeShader.SetTexture(eraserKernelId, "PreviewTex", previewTexture);
        }

        /// <summary>
        /// UVマップを取得する
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="subMeshIndex"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        private Texture2D GetUVMap(Mesh mesh, MaterialInfo matInfo, Texture2D texture)
        {
            var triangles = new List<int>();
            foreach (var slotIndex in matInfo.MaterialSlotIndices)
                triangles.AddRange(mesh.GetTriangles(slotIndex));
            var uvs = mesh.uv;

            if (uvs.Count() <= 0 || triangles.Count() <= 0) return null;

            ComputeShader cs = Instantiate(Resources.Load<ComputeShader>("getUVMap")) as ComputeShader;
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

            cs.SetTexture(kernel, "UVMap", uvMapRT);
            cs.SetInt("Width", texture.width);
            cs.SetInt("Height", texture.height);
            cs.SetBuffer(kernel, "Triangles", triangleBuffer);
            cs.SetBuffer(kernel, "UVs", uvBuffer);

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

            return uvMapTex;
        }
        #endregion


        private void ResetDrawArea(Texture2D texture, ref Material mat, ref RenderTexture previewTexture)
        {
            CopyTexture2DToRenderTexture(texture, ref previewTexture);

            mat.SetVector("_StartPos", new Vector4(0, 0, 0, 0));
            mat.SetVector("_EndPos", new Vector4(texture.width - 1, texture.height - 1, 0, 0));

            mat.SetTexture("_SelectTex", null);
        }

        private void CopyTexture2DToRenderTexture(Texture2D texture, ref RenderTexture renderTexture)
        {
            if (renderTexture != null) renderTexture.Release();

            if (isLinearColorSpace)
                renderTexture = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            else
                renderTexture = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);

            renderTexture.enableRandomWrite = true;
            renderTexture.anisoLevel = texture.anisoLevel;
            renderTexture.mipMapBias = texture.mipMapBias;
            renderTexture.filterMode = texture.filterMode;
            renderTexture.wrapMode = texture.wrapMode;
            renderTexture.wrapModeU = texture.wrapModeU;
            renderTexture.wrapModeV = texture.wrapModeV;
            renderTexture.wrapModeW = texture.wrapModeW;
            renderTexture.Create();

            Graphics.Blit(texture, renderTexture);
        }

        private void DrawTypeSetting()
        {
            if (drawType == DRAW_TYPES.PEN)
            {
                SetupDrawing(penSize, penColor, texture);
            }
        }

        /// <summary>
        ///　UVマップを書き出す
        /// </summary>
        /// <param name="deletePos"></param>
        private void ExportUVMapTexture(Texture2D uvMapTexture)
        {
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

        private void InverseSiroKuro(ref ComputeBuffer buffer, Texture2D texture, ref RenderTexture renderTexture)
        {
            var height = texture.height;
            var width = texture.width;
            var maskTexture = new Texture2D(width, height);

            var deletePos = new int[texture.width * texture.height];
            buffer.GetData(deletePos);
            deletePos = deletePos.Select(x => Mathf.Abs(x - 1)).ToArray();
            buffer.SetData(deletePos);

            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    var c = (deletePos[j * width + i] == 1) ? UnityEngine.Color.black : UnityEngine.Color.white;
                    maskTexture.SetPixel(i, j, c);
                }
            }
            maskTexture.Apply();

            Material negaposiMat = new Material(Shader.Find("Gato/NegaPosi"));
            negaposiMat.SetTexture("_MaskTex", maskTexture);
            negaposiMat.SetFloat("_Inverse", 0);
            Graphics.Blit(texture, renderTexture, negaposiMat);

            Repaint();
        }

        /// <summary>
        /// 履歴に追加する
        /// </summary>
        /// <param name="texture"></param>
        private void RegisterUndoTexture(RenderTexture texture)
        {
            undoIndex++;
            if (undoIndex >= MAX_UNDO_COUNT) undoIndex = 0;
            var undoTexture = new RenderTexture(texture);
            Graphics.CopyTexture(texture, undoTexture);
            undoTextures[undoIndex] = undoTexture;
            var undoBuffer = new int[texture.width * texture.height];
            buffer.GetData(undoBuffer);
            undoBuffers[undoIndex] = undoBuffer;
        }

        /// <summary>
        /// 履歴を使って変更を1つ戻す
        /// </summary>
        /// <param name="previewTexture"></param>
        private void UndoPreviewTexture(ref RenderTexture previewTexture)
        {
            if (undoIndex == -1) return;

            var undoTexture = undoTextures[undoIndex];
            var undoBuffer = undoBuffers[undoIndex];
            undoIndex--;
            Graphics.CopyTexture(undoTexture, previewTexture);
            buffer.SetData(undoBuffer);
            Repaint();
        }
    }
#endif
}
