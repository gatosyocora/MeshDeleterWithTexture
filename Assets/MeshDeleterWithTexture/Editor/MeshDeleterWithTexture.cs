﻿using System.Collections;
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

/*
 * Copyright (c) 2019 gatosyocora
 * Released under the MIT license.
 * see LICENSE.txt
 */

// MeshDeleterWithTexture v0.4b

namespace Gatosyocora.MeshDeleterWithTexture
{
#if UNITY_EDITOR
    public class MeshDeleterWithTexture : EditorWindow
    {

        private SkinnedMeshRenderer renderer;
        private SkinnedMeshRenderer editRenderer;
        private Texture2D originTexture;
        private Texture2D texture;
        //private bool[,] drawPos;
        private Texture2D[] textures;
        private int textureIndex = 0;

        private UnityEngine.Color penColor = UnityEngine.Color.black;
        private int penSize = 20;
        private bool isMouseDowning = false;
        private float zoomScale = 1;
        private Vector4 textureOffset = Vector4.zero;

        private bool isAreaSizeChanging = false;
        private int changingLine = 0;

        private static Material editMat;

        #region compute shader variable

        private ComputeShader computeShader;
        private ComputeBuffer buffer;
        private int penKernelId, eraserKernelId;
        private RenderTexture previewTexture;
        private Texture2D uvMapTex;

        #endregion

        private bool isDrawing = false;

        private enum DRAW_TYPES
        {
            PEN,
            ERASER,
        };

        private DRAW_TYPES drawType;

        private int triangleCount = 0;
        private string saveFolder = "Assets/";
        private string meshName;

        [MenuItem("GatoTool/MeshDeleter with Texture")]
        private static void Open()
        {
            GetWindow<MeshDeleterWithTexture>("MeshDeleter with Texture");
        }

        private void OnEnable()
        {
            editMat = Resources.Load<Material>("TextureEditMat");
            texture = null;
            renderer = null;
            textures = null;
            
            drawType = DRAW_TYPES.PEN;

            triangleCount = 0;
            saveFolder = "Assets/";

            editMat.SetInt("_PointNum", 0);

            InitComputeShader();
        }

        private void OnDisable()
        {
            if (renderer != null && textures != null)
            {
                ResetMaterialTextures(ref renderer, ref textures);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void OnGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                renderer = EditorGUILayout.ObjectField("Renderer", renderer, typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer;

                if (check.changed)
                {
                    if (renderer != null)
                    {
                        if (textures != null)
                            ResetMaterialTextures(ref editRenderer, ref textures);

                        editRenderer = renderer;

                        var mesh = renderer.sharedMesh;
                        if (mesh != null)
                        {
                            triangleCount = GetMeshTriangleCount(mesh);
                            saveFolder = GetMeshPath(mesh);
                            textures = GetTextures(renderer);
                            meshName = mesh.name + "_deleteMesh";

                            if (textures != null)
                            {
                                textureIndex = 0;
                                originTexture = textures[textureIndex];
                                if (originTexture != null)
                                {
                                    texture = LoadSettingToTexture(originTexture);

                                    DrawTypeSetting();
                                    ResetDrawArea(texture, ref editMat, ref previewTexture);
                                    SetupComputeShader(ref texture, ref previewTexture);

                                    uvMapTex = GetUVMap(mesh, textureIndex, texture);
                                    editMat.SetTexture("_UVMap", uvMapTex);

                                    renderer.sharedMaterials[textureIndex].mainTexture = previewTexture;
                                }

                                textureOffset = Vector4.zero;
                                editMat.SetVector("_Offset", textureOffset);
                                zoomScale = 1;
                                ApplyTextureZoomScale(ref editMat, zoomScale);
                            }
                        }

                    }
                    else
                    {
                        texture = null;
                        
                        if (textures != null)
                            ResetMaterialTextures(ref editRenderer, ref textures);

                        editRenderer = null;
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
                            textureOffset = Vector4.zero;
                            editMat.SetVector("_Offset", textureOffset);
                            zoomScale = 1;
                            ApplyTextureZoomScale(ref editMat, zoomScale);
                        }
                    }
                }

                ToolGUI();
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

                    ApplyTextureZoomScale(ref editMat, zoomScale);
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
                    
                    Repaint();
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
                    else
                    {
                        Repaint();
                    }
                }
            }
        }

        private void ToolGUI()
        {

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUI.DisabledGroupScope(texture == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import DeleteMask"))
                    {
                        ImportDeleteMaskTexture(ref texture, ref buffer, ref previewTexture);
                    }
                    if (GUILayout.Button("Export DeleteMask"))
                    {
                        ExportDeleteMaskTexture(buffer, originTexture);
                        renderer.sharedMaterials[textureIndex].mainTexture = previewTexture;
                    }
                }

                GUILayout.Space(10);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    if (textures != null)
                        textureIndex = EditorGUILayout.Popup("Texture", textureIndex, textures.Select(x => x.name).ToArray());

                    if (check.changed)
                    {
                        if (textures != null)
                        {
                            ResetMaterialTextures(ref renderer, ref textures);

                            originTexture = textures[textureIndex];
                            if (originTexture != null)
                            {
                                texture = LoadSettingToTexture(originTexture);

                                DrawTypeSetting();
                                ResetDrawArea(texture, ref editMat, ref previewTexture);
                                SetupComputeShader(ref texture, ref previewTexture);

                                var mesh = renderer.sharedMesh;
                                if (mesh != null)
                                {
                                    uvMapTex = GetUVMap(mesh, textureIndex, texture);
                                    editMat.SetTexture("_UVMap", uvMapTex);
                                }

                                renderer.sharedMaterials[textureIndex].mainTexture = previewTexture;
                            }
                        }
                    }
                }

                GUILayout.Space(20);

                EditorGUILayout.LabelField("DrawType");
                using (new EditorGUI.DisabledGroupScope(texture == null))
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

                // DrawTypeによるGUIの表示
                if (texture != null)
                {
                    GUILayout.Space(20);

                    if (drawType == DRAW_TYPES.PEN || drawType == DRAW_TYPES.ERASER)
                    {
                        PenEraserGUI();
                    }
                }

                EditorGUILayout.LabelField("Triangle Count", triangleCount + "");

                GUILayout.Space(10);

                OutputMeshGUI();

                GUILayout.Space(50);

                using (new EditorGUI.DisabledGroupScope(texture == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Reset All"))
                    {
                        DrawTypeSetting();
                        ResetDrawArea(texture, ref editMat,ref previewTexture);
                        SetupComputeShader(ref texture, ref previewTexture);

                        renderer.sharedMaterials[textureIndex].mainTexture = previewTexture;
                    }
                }

                if  (GUILayout.Button("Export UVMap"))
                {
                    ExportUVMapTexture(uvMapTex);
                }


                EditorGUILayout.Space();

                using (new EditorGUI.DisabledGroupScope(texture == null))
                {
                    if (GUILayout.Button("Delete Mesh"))
                    {
                        DeleteMesh(renderer, buffer, texture, textureIndex);

                        var mesh = renderer.sharedMesh;
                        if (mesh != null)
                        {
                            triangleCount = GetMeshTriangleCount(mesh);

                            ResetDrawArea(texture, ref editMat, ref previewTexture);
                            SetupComputeShader(ref texture, ref previewTexture);

                            uvMapTex = GetUVMap(mesh, textureIndex, texture);
                            editMat.SetTexture("_UVMap", uvMapTex);
                        }
                    }
                }
            }
        }

        private void PenEraserGUI()
        {
            EditorGUILayout.LabelField("PenColor");
            using (new EditorGUI.DisabledGroupScope(texture == null))
            using (new EditorGUILayout.HorizontalScope())
            {
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

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                penSize = EditorGUILayout.IntSlider("Pen/Eraser size", penSize, 1, texture.width / 20);

                if (check.changed)
                    SetupDrawing(penSize, penColor, texture);
            }
        }

        private void OutputMeshGUI()
        {
            EditorGUILayout.LabelField("Output Mesh");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("SaveFolder", saveFolder);

                if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
                {
                    saveFolder = EditorUtility.OpenFolderPanel("Select saved folder", saveFolder, "");
                    var match = Regex.Match(saveFolder, @"Assets/.*");
                    saveFolder = match.Value + "/";
                    if (saveFolder == "/") saveFolder = "Assets/";
                }
            }

            meshName = EditorGUILayout.TextField("Name", meshName);
        }

        /// <summary>
        /// メッシュを削除する
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="deleteTexPos"></param>
        /// <param name="texture"></param>
        /// <param name="subMeshIndexInDeletedVertex"></param>
        private void DeleteMesh(SkinnedMeshRenderer renderer, ComputeBuffer computeBuffer, Texture2D texture, int subMeshIndexInDeletedVertex)
        {

            var mesh = renderer.sharedMesh;
            var mesh_custom = Instantiate(mesh);

            mesh_custom.Clear();
            mesh_custom.MarkDynamic();

            // 削除する頂点のリストを取得
            var uvs = mesh.uv.ToList();
            var uv2s = mesh.uv2.ToList();
            var uv3s = mesh.uv3.ToList();
            var uv4s = mesh.uv4.ToList();
            int x, y;
            List<int> deleteIndexList = new List<int>();

            var alluvs = new List<Vector2>();
            alluvs.AddRange(uvs);
            alluvs.AddRange(uv2s);
            alluvs.AddRange(uv3s);
            alluvs.AddRange(uv4s);
            alluvs = alluvs.Distinct().ToList();

            var deletePos = new int[texture.width * texture.height];
            computeBuffer.GetData(deletePos);

            for (int i = 0; i < uvs.Count(); i++)
            {
                x = (int)(Mathf.Abs(uvs[i].x % 1.0f) * texture.width);
                y = (int)(Mathf.Abs(uvs[i].y % 1.0f) * texture.height);

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
                if (subMeshIndex != subMeshIndexInDeletedVertex)
                    nonDeleteVertexIndexs.AddRange(mesh.GetIndices(subMeshIndex));
            }
            nonDeleteVertexIndexs = nonDeleteVertexIndexs.Distinct().OrderBy(v => v).ToList();

            // 削除する頂点のインデックスのリスト(重複なし)
            var deleteIndexListUnique
                = deleteIndexList
                    .Distinct()
                    .Where(i => nonDeleteVertexIndexs.BinarySearch(i) < 0);

            // 削除する頂点のインデックスのリスト (重複なし, 昇順)
            var deleteIndexsOrdered
                = deleteIndexListUnique
                    .OrderBy(value => value)
                    .ToList();

            // 頂点を削除
            var vertices = mesh.vertices.ToList();
            var boneWeights = mesh.boneWeights.ToList();
            var normals = mesh.normals.ToList();
            var tangents = mesh.tangents.ToList();

            var nonDeleteVertices = vertices.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteWeights = boneWeights.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteNormals = normals.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteTangents = tangents.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToArray();
            var nonDeleteUVs = uvs.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteUV2s = uv2s.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteUV3s = uv3s.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();
            var nonDeleteUV4s = uv4s.Where((v, index) => deleteIndexsOrdered.BinarySearch(index) < 0).ToList();

            mesh_custom.SetVertices(nonDeleteVertices);
            mesh_custom.boneWeights = nonDeleteWeights;
            mesh_custom.normals = nonDeleteNormals;
            mesh_custom.tangents = nonDeleteTangents;
            mesh_custom.SetUVs(0, nonDeleteUVs);
            mesh_custom.SetUVs(1, nonDeleteUV2s);
            mesh_custom.SetUVs(2, nonDeleteUV3s);
            mesh_custom.SetUVs(3, nonDeleteUV4s);

            // サブメッシュごとにポリゴンを処理
            int count = 0;

            // 削除する頂点のインデックスのリスト（重複なし, 降順）
            var deleteIndexListUniqueDescending
                = deleteIndexListUnique
                    .OrderByDescending(value => value)
                    .ToArray();

            mesh_custom.subMeshCount = mesh.subMeshCount;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                count = 0;

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

                    EditorUtility.DisplayProgressBar("search deleted triangles", 
                                                        "submesh "+ (subMeshIndex+1) + "/" +mesh.subMeshCount+ "  " + count + " / " + deleteIndexListUniqueDescending.Count(), 
                                                        (count++) / (float)deleteIndexListUniqueDescending.Count());
                }

                // 不要なポリゴンを削除する
                var triangleList = subMeshTriangles.Where(v => v != -1).ToArray();
                mesh_custom.SetTriangles(triangleList, subMeshIndex);
            }

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

                mesh_custom.AddBlendShapeFrame(blendShapeName, frameWeight,
                    deltaNonDeleteVerteicesList,
                    deltaNonDeleteNormalsList,
                    deltaNonDeleteTangentsList);
            }

            if (meshName == "") meshName = mesh.name+"_deleteMesh";
            AssetDatabase.CreateAsset(mesh_custom, AssetDatabase.GenerateUniqueAssetPath(saveFolder + meshName + ".asset"));
            AssetDatabase.SaveAssets();

            Undo.RecordObject(renderer, "Change mesh " + mesh_custom.name);
            renderer.sharedMesh = mesh_custom;

            renderer.sharedMaterials[subMeshIndexInDeletedVertex].mainTexture = texture;

            EditorUtility.ClearProgressBar();
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

            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
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
                    var isDelete = (col == UnityEngine.Color.black)? 1:0;
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
        }

        /// <summary>
        /// Meshのポリゴン数を取得する
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        private int GetMeshTriangleCount(Mesh mesh)
        {
            return mesh.triangles.Length / 3;
        }

        /// <summary>
        /// mesh保存先のパスを取得する
        /// </summary>
        /// <param name="Mesh"></param>
        /// <returns></returns>
        private string GetMeshPath(Mesh mesh)
        {
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(mesh)) + "/";
        }

        /// <summary>
        /// テクスチャを取得する
        /// </summary>
        /// <param name="renderer"></param>
        /// <returns></returns>
        private Texture2D[] GetTextures(SkinnedMeshRenderer renderer)
        {
            var materials = renderer.sharedMaterials;
            var textures = new Texture2D[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                textures[i] = materials[i].mainTexture as Texture2D;
            }
            return textures;
        }

        private void ResetMaterialTextures(ref SkinnedMeshRenderer renderer, ref Texture2D[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
                renderer.sharedMaterials[i].mainTexture = textures[i];
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
        private Texture2D GetUVMap(Mesh mesh, int subMeshIndex, Texture2D texture)
        {
            var triangles = mesh.GetTriangles(subMeshIndex);
            var uvs = mesh.uv;

            if (uvs.Count() <= 0) return null;

            ComputeShader cs = Instantiate(Resources.Load<ComputeShader>("getUVMap")) as ComputeShader;
            int kernel = cs.FindKernel("CSMain");

            RenderTexture uvMapRT = new RenderTexture(texture.width, texture.height, 0);
            uvMapRT.enableRandomWrite = true;
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

            cs.Dispatch(kernel, triangles.Length / 3, 1, 1);

            triangleBuffer.Release();
            uvBuffer.Release();

            Texture2D uvMapTex = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            uvMapTex.name = texture.name;

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
            if (previewTexture != null) previewTexture.Release();
            previewTexture = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            previewTexture.enableRandomWrite = true;
            previewTexture.Create();
            Graphics.Blit(texture, previewTexture);

            mat.SetVector("_StartPos", new Vector4(0, 0, 0, 0));
            mat.SetVector("_EndPos", new Vector4(texture.width - 1, texture.height - 1, 0, 0));

            mat.SetTexture("_SelectTex", null);
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

    }
#endif
}
