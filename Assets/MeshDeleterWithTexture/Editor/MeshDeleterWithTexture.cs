using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
//using System.Drawing;
//using System.Runtime.InteropServices;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

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
        private int penKernelId, eraserKernelId, fillKernelId;
        private RenderTexture rwTexture;

        #endregion

        private bool isDrawing = false;

        struct HSV
        {
            public float h;
            public float s;
            public float v;
            public HSV(float _h, float _s, float _v) { h = _h; s = _s; v = _v; }

        }

        private HSV hsvThreshold = new HSV(0.1f, 0.1f, 0.1f);

        private Color targetColor = Color.white;
        private Vector2 dragPos1 = Vector2.zero;
        private Vector2 dragPos2 = Vector2.zero;

        private enum DRAW_TYPES
        {
            CHOOSE_COLOR = 0,
            SELECT_AREA = 1,
            PEN = 2,
            ERASER = 3
        };

        private DRAW_TYPES drawType;

        private int triangleCount = 0;
        private string saveFolder = "Assets/";
        private string meshName;

        struct UVItem {
            public Vector2 uv;
            public int index;
        };

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

            InitComputeShader();
        }

        private void OnDisable()
        {
            if (renderer != null && textures != null)
            {
                ResetMaterialTextures(ref renderer, ref textures);
            }
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
                                    ResetDrawArea(ref texture, ref rwTexture, ref editMat);
                                    SetupComputeShader(ref texture, ref rwTexture);
                                    InitComputeBuffer(texture);

                                    var uvMapTex = GetUVMap(mesh, textureIndex, texture);
                                    editMat.SetTexture("_UVMap", uvMapTex);

                                    renderer.sharedMaterials[textureIndex].mainTexture = texture;
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

                        ResetDrawArea(ref texture, ref rwTexture, ref editMat);
                        SetupComputeShader(ref texture, ref rwTexture);
                        InitComputeBuffer(texture);

                        var uvMapTex = GetUVMap(mesh, textureIndex, texture);
                        editMat.SetTexture("_UVMap", uvMapTex);
                    }
                }
            }
        }

        private void CanvasGUI()
        {
            var width = EditorGUIUtility.currentViewWidth * 0.6f;
            var height = width * texture.height / texture.width;
            EventType mouseEventType = 0;
            Rect rect = new Rect(0, 0, 0, 0);
            var delta = GatoGUILayout.MiniMonitor(texture, width, height, ref rect, ref mouseEventType, true);

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

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                !isMouseDowning && rect.Contains(Event.current.mousePosition))
            {
                isMouseDowning = true;
            }
            else if (Event.current.type == EventType.MouseUp &&
                Event.current.button == 0 &&
                isMouseDowning)
            {
                isMouseDowning = false;
            }

            if (rect.Contains(Event.current.mousePosition))
            {
                var pos = ConvertWindowPosToTexturePos(texture, Event.current.mousePosition, rect);

                if (drawType == DRAW_TYPES.CHOOSE_COLOR)
                {
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        targetColor = originTexture.GetPixel((int)pos.x, (int)pos.y);
                    }
                }
                else if (drawType == DRAW_TYPES.SELECT_AREA)
                {

                    if (Event.current.type == EventType.MouseDown)
                    {
                        isMouseDowning = true;

                        if (Mathf.Abs(pos.x - dragPos1.x) < 10 && Mathf.Max(dragPos1.y, dragPos2.y) >= pos.y && Mathf.Min(dragPos1.y, dragPos2.y) <= pos.y)
                        {
                            changingLine = 1;
                            isAreaSizeChanging = true;
                        }
                        else if (Mathf.Abs(pos.x - dragPos2.x) < 10 && Mathf.Max(dragPos1.y, dragPos2.y) >= pos.y && Mathf.Min(dragPos1.y, dragPos2.y) <= pos.y)
                        {
                            changingLine = 2;
                            isAreaSizeChanging = true;
                        }
                        else if (Mathf.Abs(pos.y - dragPos1.y) < 10 && Mathf.Max(dragPos1.x, dragPos2.x) >= pos.x && Mathf.Min(dragPos1.x, dragPos2.x) <= pos.x)
                        {
                            changingLine = 3;
                            isAreaSizeChanging = true;
                        }
                        else if (Mathf.Abs(pos.y - dragPos2.y) < 10 && Mathf.Max(dragPos1.x, dragPos2.x) >= pos.x && Mathf.Min(dragPos1.x, dragPos2.x) <= pos.x)
                        {
                            changingLine = 4;
                            isAreaSizeChanging = true;
                        }
                        else
                        {
                            dragPos1 = pos;
                            var uvPos = ConvertTexturePosToUVPos(texture, dragPos1);
                            var uvPosVector4 = new Vector4(uvPos.x, uvPos.y, 0, 0);

                            editMat.SetVector("_StartPos", uvPosVector4);
                            editMat.SetVector("_EndPos", uvPosVector4);
                        }

                    }
                    else if (Event.current.type == EventType.MouseUp)
                    {
                        isMouseDowning = false;
                        isAreaSizeChanging = false;
                        changingLine = 0;
                    }


                    if (isMouseDowning)
                    {
                        if (isAreaSizeChanging)
                        {
                            if (changingLine == 1)
                            {
                                dragPos1.x =pos.x;
                                var uvPos = ConvertTexturePosToUVPos(texture, dragPos1);
                                editMat.SetVector("_StartPos", new Vector4(uvPos.x, uvPos.y, 0, 0));
                            }
                            else if (changingLine == 2)
                            {
                                dragPos2.x = pos.x;
                                var uvPos = ConvertTexturePosToUVPos(texture, dragPos2);
                                editMat.SetVector("_EndPos", new Vector4(uvPos.x, uvPos.y, 0, 0));
                            }
                            else if (changingLine == 3)
                            {
                                dragPos1.y = pos.y;
                                var uvPos = ConvertTexturePosToUVPos(texture, dragPos1);
                                editMat.SetVector("_StartPos", new Vector4(uvPos.x, uvPos.y, 0, 0));
                            }
                            else if (changingLine == 4)
                            {
                                dragPos2.y = pos.y;
                                var uvPos = ConvertTexturePosToUVPos(texture, dragPos2);
                                editMat.SetVector("_EndPos", new Vector4(uvPos.x, uvPos.y, 0, 0));
                            }
                        }
                        else
                        {
                            dragPos2 = pos;
                            var uvPos = ConvertTexturePosToUVPos(texture, dragPos2);
                            editMat.SetVector("_EndPos", new Vector4(uvPos.x, uvPos.y, 0, 0));
                        }
                        Repaint();
                    }
                }

                else if (drawType == DRAW_TYPES.PEN || drawType == DRAW_TYPES.ERASER)
                {
                    var uvPos = ConvertTexturePosToUVPos(texture, pos);
                    editMat.SetVector("_CurrentPos", new Vector4(uvPos.x, uvPos.y, 0, 0));
                    Repaint();

                    if (Event.current.type == EventType.MouseDown)
                    {
                        isDrawing = true;
                    }
                    else if (Event.current.type == EventType.MouseUp)
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
                        ImportDeleteMaskTexture(ref texture, ref buffer);
                    }
                    if (GUILayout.Button("Export DeleteMask"))
                    {
                        ExportDeleteMaskTexture(buffer, originTexture);
                        renderer.sharedMaterials[textureIndex].mainTexture = texture;
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
                                ResetDrawArea(ref texture, ref rwTexture, ref editMat);
                                SetupComputeShader(ref texture, ref rwTexture);
                                InitComputeBuffer(texture);

                                var mesh = renderer.sharedMesh;
                                if (mesh != null)
                                {
                                    var uvMapTex = GetUVMap(mesh, textureIndex, texture);
                                    editMat.SetTexture("_UVMap", uvMapTex);
                                }

                                renderer.sharedMaterials[textureIndex].mainTexture = texture;
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
                    if (drawType == DRAW_TYPES.PEN || drawType == DRAW_TYPES.ERASER)
                    {
                        PenEraserGUI();
                    }

                    if (drawType == DRAW_TYPES.CHOOSE_COLOR || drawType == DRAW_TYPES.SELECT_AREA)
                    {
                        FillGUI();
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
                        ResetDrawArea(ref texture, ref rwTexture, ref editMat);
                        SetupComputeShader(ref texture, ref rwTexture);
                        InitComputeBuffer(texture);
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
                }
                if (GUILayout.Button("R"))
                {
                    penColor = UnityEngine.Color.red;
                }
                if (GUILayout.Button("G"))
                {
                    penColor = UnityEngine.Color.green;
                }
                if (GUILayout.Button("B"))
                {
                    penColor = UnityEngine.Color.blue;
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

        private void FillGUI()
        {
            targetColor = EditorGUILayout.ColorField("Target Color", targetColor);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                hsvThreshold.h = EditorGUILayout.Slider("H Threshold", hsvThreshold.h, 0, 1);
                hsvThreshold.s = EditorGUILayout.Slider("S Threshold", hsvThreshold.s, 0, 1);
                hsvThreshold.v = EditorGUILayout.Slider("V Threshold", hsvThreshold.v, 0, 1);

                if (check.changed)
                {
                    FillOnTexture(ref texture, targetColor, dragPos1, dragPos2, hsvThreshold);
                }
            }

            if (GUILayout.Button("Fill"))
            {
                FillOnTexture(ref texture, targetColor, dragPos1, dragPos2, hsvThreshold);
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

            var count = 0;


            Stopwatch sw = new Stopwatch();
            sw.Reset();
            sw.Start();

            /*
            foreach (var uv in alluvs)
            {
                x = (int)(Mathf.Abs(uv.x % 1.0f) * texture.width);
                y = (int)(Mathf.Abs(uv.y % 1.0f) * texture.height);

                if (x == texture.width || y == texture.height) continue;

                int index = y * texture.width + x;

                if (deletePos[index] == 1)
                {
                    // そのテクスチャ位置に対応するUVが複数存在するため, そのUVのインデックスを取得
                    var uvIndexList
                        = uvs
                            .Select((value, i) => new { Index = i, UV = value })
                            .Where(v => v.UV == uv)
                            .Select(v => v.Index)
                            .ToList();

                    deleteIndexList.AddRange(uvIndexList);
                }
                
                EditorUtility.DisplayProgressBar("Delete Mesh",
                    "Searching deleteVertices: " + count++ + "/" + alluvs.Count(),
                    count/(float)alluvs.Count());
            }
        */

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

                /*EditorUtility.DisplayProgressBar("Delete Mesh",
                    "Searching deleteVertices: " + count++ + "/" + alluvs.Count(),
                    count/(float)alluvs.Count());
                    */
            }


            sw.Stop();
            var cpuSpeed = sw.ElapsedMilliseconds;

            sw.Reset();
            sw.Start();

            var uvItems = uvs.Select((v, i) => new UVItem {uv = v, index = i}).ToArray();

            var cs = Instantiate(Resources.Load("getDeleteUVIndexs")) as ComputeShader;
            var kernel = cs.FindKernel("CSMain");

            var uvBuffer = new ComputeBuffer(uvs.Count(), Marshal.SizeOf(typeof(UVItem)));
            uvBuffer.SetData(uvItems);

            var deleteVertexIndexBuffer = new ComputeBuffer(uvs.Count(), Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Append);
            deleteVertexIndexBuffer.SetCounterValue(0);

            var countBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
            var itemCount = new int[] { 0, 1, 0, 0 };
            countBuffer.SetData(itemCount);

            cs.SetBuffer(kernel, "isDelete", computeBuffer);
            cs.SetBuffer(kernel, "UV", uvBuffer);
            cs.SetInt("Width", texture.width);
            cs.SetInt("Height", texture.height);
            cs.SetBuffer(kernel, "DeleteVertexIndex", deleteVertexIndexBuffer);
            cs.Dispatch(kernel, uvs.Count() / 32, 1, 1);

            ComputeBuffer.CopyCount(deleteVertexIndexBuffer, countBuffer, 0);
            countBuffer.GetData(itemCount);
            var size = itemCount[0];
            var deleteVertexIndexs = new int[size];
            deleteVertexIndexBuffer.GetData(deleteVertexIndexs);

            uvBuffer.Release();
            deleteVertexIndexBuffer.Release();
            countBuffer.Release();

            sw.Stop();
            var gpuSpeed = sw.ElapsedMilliseconds;

            deleteIndexList = deleteVertexIndexs.ToList();


            UnityEngine.Debug.LogFormat("cpu:{0}, gpu:{1}", cpuSpeed, gpuSpeed);

            // TODO: 共有されている頂点は存在しない？
            // 他のサブメッシュで共有されている頂点は削除してはいけない
            List<int> nonDeleteSubMeshIndexs = new List<int>();
            for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                if (subMeshIndex != subMeshIndexInDeletedVertex)
                    nonDeleteSubMeshIndexs.AddRange(mesh.GetIndices(subMeshIndex));
            }

            // 削除する頂点のインデックスのリスト（重複なし, 降順）
            var deleteIndexListUniqueDescending
                = deleteIndexList
                    .Distinct()
                    .Where(i => !nonDeleteSubMeshIndexs.Contains(i))
                    .OrderByDescending(value => value)
                    .ToArray();

            // 削除する頂点のインデックスのリスト (重複なし, 昇順)
            var deleteIndexsOrdered
                = deleteIndexList
                    .Distinct()
                    .Where(i => !nonDeleteSubMeshIndexs.Contains(i))
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

            count = 0;

            // サブメッシュごとにポリゴンを処理
            mesh_custom.subMeshCount = mesh.subMeshCount;
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
                
                // 不要なポリゴンを削除する
                var triangleList = subMeshTriangles.ToList();
                for (int i = triangleList.Count() - 1; i >= 0; i--)
                {
                    if (triangleList[i] == -1)
                        triangleList.RemoveAt(i);
                }

                EditorUtility.DisplayProgressBar("Delete Mesh",
                    "Deleting Triangles in subMeshs : " + count++ + "/ " + mesh.subMeshCount,
                    count / (float)mesh.subMeshCount
                );

                mesh_custom.SetTriangles(triangleList.ToArray(), subMeshIndex);
            }

            count = 0;

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

                EditorUtility.DisplayProgressBar("Delete Mesh",
                        "Setting BlendShapes : " + count++ + "/" + mesh.blendShapeCount,
                        count / (float)mesh.blendShapeCount
                );

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
            float[] penColorArray = new float[4 * sizeof(float)];
            penColorArray[0 * sizeof(float)] = penColor.r;
            penColorArray[1 * sizeof(float)] = penColor.g;
            penColorArray[2 * sizeof(float)] = penColor.b;
            penColorArray[3 * sizeof(float)] = penColor.a;

            computeShader.SetInt("PenSize", penSize);
            computeShader.SetFloats("PenColor", penColorArray);
            editMat.SetFloat("_PenSize", penSize / (float)texture.width);
        }

        /// <summary>
        /// 塗りつぶし
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="pos"></param>
        /// <param name="c"></param>
        /// <param name="drawPos"></param>
        /// 
        private void FillOnTexture(ref Texture2D texture, Color targetColor, Vector4 pos1, Vector4 pos2, HSV hsvThreshold)
        {
            var colArray = new float[3 * sizeof(float)];
            colArray[0 * sizeof(float)] = targetColor.r;
            colArray[1 * sizeof(float)] = targetColor.g;
            colArray[2 * sizeof(float)] = targetColor.b;
            computeShader.SetFloats("TargetCol", colArray);

            var hsvThresholdArray = new float[3 * sizeof(float)];
            hsvThresholdArray[0 * sizeof(float)] = hsvThreshold.h;
            hsvThresholdArray[1 * sizeof(float)] = hsvThreshold.s;
            hsvThresholdArray[2 * sizeof(float)] = hsvThreshold.v;
            computeShader.SetFloats("HsvThreshold", hsvThresholdArray);

            var pos1Array = new float[2 * sizeof(float)];
            pos1Array[0 * sizeof(float)] = pos1.x;
            pos1Array[1 * sizeof(float)] = pos1.y;

            var pos2Array = new float[2 * sizeof(float)];
            pos2Array[0 * sizeof(float)] = pos2.x;
            pos2Array[1 * sizeof(float)] = pos2.y;
            computeShader.SetFloats("Pos1", pos1Array);
            computeShader.SetFloats("Pos2", pos2Array);

            computeShader.Dispatch(fillKernelId, texture.width / 32, texture.height / 32, 1);

            Repaint();
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
        private bool ImportDeleteMaskTexture(ref Texture2D texture, ref ComputeBuffer buffer)
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
            buffer.GetData(deletePos);

            for (int j = 0; j < maskTexture.height; j++)
            {
                for (int i = 0; i < maskTexture.width; i++)
                {
                    var isDelete = (maskTexture.GetPixel(i, j) == UnityEngine.Color.black)? 1:0;
                    deletePos[j * maskTexture.width + i] = isDelete;
                }
            }

            buffer.SetData(deletePos);

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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            fillKernelId = computeShader.FindKernel("CSFill");
        }

        private void InitComputeBuffer(Texture2D texture)
        {
            if (buffer != null) buffer.Release();
            buffer = new ComputeBuffer(texture.width * texture.height, sizeof(int));
            computeShader.SetBuffer(penKernelId, "Result", buffer);
            computeShader.SetBuffer(eraserKernelId, "Result", buffer);
            computeShader.SetBuffer(fillKernelId, "Result", buffer);
        }

        private void SetupComputeShader(ref Texture2D texture, ref RenderTexture rwTexture)
        {
            InitComputeBuffer(texture);

            computeShader.SetTexture(penKernelId, "Tex", texture);
            computeShader.SetTexture(eraserKernelId, "Tex", texture);
            computeShader.SetTexture(fillKernelId, "Tex", texture);
            computeShader.SetInt("Width", texture.width);
            computeShader.SetInt("Height", texture.height);

            computeShader.SetTexture(penKernelId, "FillTex", rwTexture);
            computeShader.SetTexture(eraserKernelId, "FillTex", rwTexture);
            computeShader.SetTexture(fillKernelId, "FillTex", rwTexture);
        }
        private void ResetDrawArea(ref Texture2D texture, ref RenderTexture rwTexture, ref Material mat)
        {
            if (rwTexture != null) rwTexture.Release();
            rwTexture = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGBFloat);
            rwTexture.enableRandomWrite = true;
            rwTexture.Create();
            
            mat.SetTexture("_SecondTex", rwTexture);

            mat.SetVector("_StartPos", new Vector4(0, 0, 0, 0));
            mat.SetVector("_EndPos", new Vector4(texture.width - 1, texture.height - 1, 0, 0));
        }

        private void DrawTypeSetting()
        {
            editMat.SetFloat("_EditType", (int)drawType);
            computeShader.SetInt("DrawType", (int)drawType);

            if (drawType == DRAW_TYPES.PEN)
            {
                SetupDrawing(penSize, penColor, texture);
            }
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

            Texture2D uvMapTex = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
            Graphics.CopyTexture(uvMapRT, uvMapTex);
            uvMapRT.Release();

            return uvMapTex;
        }

        #endregion

    }
#endif
}
