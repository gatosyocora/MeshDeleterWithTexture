using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
//using System.Drawing;
//using System.Runtime.InteropServices;
using System;

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
        private bool[,] drawPos;
        private Texture2D[] textures;
        private int textureIndex = 0;

        private UnityEngine.Color penColor = UnityEngine.Color.black;
        private int penSize = 20;
        private bool isMouseDowning = false;
        private float zoomScale = 1;
        private Vector4 textureOffset = Vector4.zero;

        private static Material editMat;
        
        private enum DRAW_TYPES
        {
            //NONE = -1,
            PEN = 0,
            ERASER = 1,
            //FILL = 2,
            //EDIT = 3
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
            drawPos = null;

            drawType = DRAW_TYPES.PEN;
            triangleCount = 0;
            saveFolder = "Assets/";
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
                                drawPos = new bool[originTexture.width, originTexture.height];
                                if (originTexture != null)
                                {
                                    texture = LoadSettingToTexture(originTexture);
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
                            //Undo.RecordObject(texture, "change texture");
                        }
                        else if (Event.current.type == EventType.MouseUp &&
                            Event.current.button == 0 &&
                            isMouseDowning)
                        {
                            isMouseDowning = false;
                            //Undo.FlushUndoRecordObjects();
                        }

                        if (isMouseDowning && rect.Contains(Event.current.mousePosition) &&
                            Event.current.button == 0)
                        {
                            var pos = ConvertWindowPosToTexturePos(texture, Event.current.mousePosition - rect.position, rect);

                            if (drawType == DRAW_TYPES.PEN)
                                DrawOnTexture(ref texture, pos, penColor, penSize, ref drawPos);
                            else if (drawType == DRAW_TYPES.ERASER)
                                ClearOnTexture(ref texture, pos, penSize, ref drawPos, originTexture);
                            /*
                            else if (drawType == DRAW_TYPES.FILL)
                            {
                                Stopwatch sw = new Stopwatch();
                                sw.Start();

                                FillOnTexture(ref texture, pos, penColor, ref drawPos);

                                sw.Stop();
                                UnityEngine.Debug.Log("Elapsed time (ms) = " + sw.ElapsedMilliseconds);
                            }
                            */
                        }
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
                    

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUI.DisabledGroupScope(texture == null))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Import DeleteMask"))
                        {
                            ImportDeleteMaskTexture(ref texture, ref drawPos);
                        }
                        if (GUILayout.Button("Export DeleteMask"))
                        {
                            ExportDeleteMaskTexture(drawPos);
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
                                    drawPos = new bool[originTexture.width, originTexture.height];
                                    texture = LoadSettingToTexture(originTexture);
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
                        drawType = (DRAW_TYPES)GUILayout.Toolbar((int)drawType, Enum.GetNames(typeof(DRAW_TYPES)));
                        editMat.SetFloat("_EditType", (int)drawType);
                        /*
                        if (GUILayout.Button("Pen"))
                        {
                            drawType = DRAW_TYPES.PEN;
                            editMat.SetFloat("_EditType", 0);
                        }
                        if (GUILayout.Button("Eraser"))
                        {
                            drawType = DRAW_TYPES.ERASER;
                            editMat.SetFloat("_EditType", 0);
                        }
                        if (GUILayout.Button("Fill"))
                        {
                            drawType = DRAW_TYPES.FILL;
                            editMat.SetFloat("_EditType", 0);
                        }
                        */
                    }

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

                    if (texture != null)
                        penSize = EditorGUILayout.IntSlider("Pen/Eraser size", penSize, 1, texture.width / 20);

                    EditorGUILayout.LabelField("Triangle Count", triangleCount + "");

                    GUILayout.Space(10);

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

                    GUILayout.Space(50);

                    using (new EditorGUI.DisabledGroupScope(texture == null))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Reset All"))
                        {
                            drawPos = new bool[texture.width, texture.height];
                            texture.SetPixels(originTexture.GetPixels());
                            texture.Apply();
                        }
                    }
                    
                    /*
                    if (drawType == DRAW_TYPES.EDIT)
                    {
                        editMat.SetFloat("_Threshold", EditorGUILayout.Slider(editMat.GetFloat("_Threshold"), 0, 1));
                        editMat.SetColor("_Color", EditorGUILayout.ColorField(editMat.GetColor("_Color")));
                    }
                    */
                }
                
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledGroupScope(texture == null))
            {
                if (GUILayout.Button("Delete Mesh"))
                {
                    DeleteMesh(renderer, drawPos, texture, textureIndex);

                    var mesh = renderer.sharedMesh;
                    if (mesh != null)
                    {
                        triangleCount = GetMeshTriangleCount(mesh);
                    }
                }
            }
        }

        /// <summary>
        /// メッシュを削除する
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="deleteTexPos"></param>
        /// <param name="texture"></param>
        /// <param name="subMeshIndexInDeletedVertex"></param>
        private void DeleteMesh(SkinnedMeshRenderer renderer, bool[,] deleteTexPos, Texture2D texture, int subMeshIndexInDeletedVertex)
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

            var count = 0;
            foreach (var uv in alluvs)
            {
                x = (int)(Mathf.Abs(uv.x % 1.0f) * texture.width);
                y = (int)(Mathf.Abs(uv.y % 1.0f) * texture.height);

                if (x == texture.width || y == texture.height) continue;

                if (deleteTexPos[x, y])
                {
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

            // 頂点を削除
            var vertices = mesh.vertices.ToList();
            var boneWeights = mesh.boneWeights.ToList();
            var normals = mesh.normals.ToList();
            var tangents = mesh.tangents.ToList();

            count = 0;
            foreach (var deleteVertexIndex in deleteIndexListUniqueDescending)
            {
                vertices.RemoveAt(deleteVertexIndex);
                boneWeights.RemoveAt(deleteVertexIndex);
                normals.RemoveAt(deleteVertexIndex);
                tangents.RemoveAt(deleteVertexIndex);
                if (deleteVertexIndex < uvs.Count())
                    uvs.RemoveAt(deleteVertexIndex);
                if (deleteVertexIndex < uv2s.Count())
                    uv2s.RemoveAt(deleteVertexIndex);
                if (deleteVertexIndex < uv3s.Count())
                    uv3s.RemoveAt(deleteVertexIndex);
                if (deleteVertexIndex < uv4s.Count())
                    uv4s.RemoveAt(deleteVertexIndex);

                EditorUtility.DisplayProgressBar("Delete Mesh",
                    "Deleting Vertices: " + count++ + "/" + deleteIndexListUniqueDescending.Count(),
                    count / (float)deleteIndexListUniqueDescending.Count());
            }
            mesh_custom.SetVertices(vertices);
            mesh_custom.boneWeights = boneWeights.ToArray();
            mesh_custom.normals = normals.ToArray();
            mesh_custom.tangents = tangents.ToArray();
            mesh_custom.SetUVs(0, uvs);
            mesh_custom.SetUVs(1, uv2s);
            mesh_custom.SetUVs(2, uv3s);
            mesh_custom.SetUVs(3, uv4s);


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
            List<Vector3> deltaVerticesList, deltaNormalsList, deltaTangentsList;
            for (int blendshapeIndex = 0; blendshapeIndex < mesh.blendShapeCount; blendshapeIndex++)
            {
                blendShapeName = mesh.GetBlendShapeName(blendshapeIndex);
                frameWeight = mesh.GetBlendShapeFrameWeight(blendshapeIndex, 0);
                
                mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);
                deltaVerticesList = deltaVertices.ToList();
                deltaNormalsList = deltaNormals.ToList();
                deltaTangentsList = deltaTangents.ToList();
                foreach (var deleteVertexIndex in deleteIndexListUniqueDescending)
                {
                    deltaVerticesList.RemoveAt(deleteVertexIndex);
                    deltaNormalsList.RemoveAt(deleteVertexIndex);
                    deltaTangentsList.RemoveAt(deleteVertexIndex);

                    EditorUtility.DisplayProgressBar("Delete Mesh",
                        "Setting BlendShapes : " + count++ + "/" + mesh.blendShapeCount * deleteIndexListUniqueDescending.Count(),
                        count / (float)(mesh.blendShapeCount * deleteIndexListUniqueDescending.Count())
                    );
                }
                mesh_custom.AddBlendShapeFrame(blendShapeName, frameWeight,
                    deltaVerticesList.ToArray(),
                    deltaNormalsList.ToArray(),
                    deltaTangentsList.ToArray());
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
        /// <param name="texture"></param>
        /// <param name="pos"></param>
        /// <param name="c"></param>
        /// <param name="r"></param>
        /// <param name="drawPos"></param>
        private void DrawOnTexture(ref Texture2D texture, Vector2 pos, UnityEngine.Color c, int r, ref bool[,] drawPos)
        {
            int x = (int)pos.x, y = (int)pos.y;
            
            for (int i = -r; i < r; i++)
            {
                for (int j = -r; j < r; j++)
                {
                    if (x + j < 0 || y + i < 0 ||
                        x + j > texture.width - 1 || y + i > texture.height - 1 ||
                        drawPos[x + j, y + i]) continue;

                    texture.SetPixel(x + j, y + i, c);
                    drawPos[x + j, y + i] = true;
                }
            }

            texture.Apply();
            Repaint();
        }

        /// <summary>
        /// 消しゴム
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="pos"></param>
        /// <param name="r"></param>
        /// <param name="drawPos"></param>
        /// <param name="originTexture"></param>
        private void ClearOnTexture(ref Texture2D texture, Vector2 pos, int r, ref bool[,] drawPos, Texture2D originTexture)
        {
            int x = (int)pos.x, y = (int)pos.y;

            for (int i = -r; i < r; i++)
            {
                for (int j = -r; j < r; j++)
                {
                    if (x + j < 0 || y + i < 0 || x + j > texture.width - 1 || y + i > texture.height - 1) continue;
                    var c = originTexture.GetPixel(x + j, y + i);
                    texture.SetPixel(x + j, y + i, c);
                    drawPos[x + j, y + i] = false;
                }
            }

            texture.Apply();
            Repaint();
        }

        /// <summary>
        /// 塗りつぶし
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="pos"></param>
        /// <param name="c"></param>
        /// <param name="drawPos"></param>
        /// 
        private void FillOnTexture(ref Texture2D texture, Vector2 pos, UnityEngine.Color c, ref bool[,] drawPos)
        {
            var stack = new Stack<Vector2>();
            
            int x = (int)pos.x, 
                y = (int)pos.y;

            var cols = texture.GetPixels();
            var width = texture.width;
            var height = texture.height;

            var selectColor = cols[y * width + x];
            float selectH, selectS, selectV;
            UnityEngine.Color.RGBToHSV(selectColor, out selectH, out selectS, out selectV);

            stack.Push(new Vector2(x, y));

            var d = 5;

            UnityEngine.Color col;
            Vector2 p;
            while (true)
            {
                if (stack.Count() <= 0) break;

                p = stack.Pop();

                x = (int)p.x;
                y = (int)p.y;
                
                if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) continue;
                
                col = cols[y * width + x];
                float h, s, v;
                UnityEngine.Color.RGBToHSV(col, out h, out s, out v);

                if (drawPos[x, y] || Mathf.Abs(h - selectH) > 0.1 || Mathf.Abs(s - selectS) > 0.1 || Mathf.Abs(v - selectV) > 0.1) continue;

                for (int i = -d + 1; i < d - 1; i++)
                {
                    for (int j = -d + 1; j < d - 1; j++)
                    {
                        if (x + i < 0 || x + i > texture.width - 1 ||
                            y + j < 0 || y + j > texture.height - 1) continue;
                        
                        cols[(y+j) * width + (x+i)] = c;
                        drawPos[x + i, y + j] = true;
                    }
                }

                stack.Push(new Vector2(x + d, y));
                stack.Push(new Vector2(x - d, y));
                stack.Push(new Vector2(x, y + d));
                stack.Push(new Vector2(x, y - d));
            }

            texture.SetPixels(cols);
            texture.Apply();
            Repaint();
        }

        /* 別パターン
        private void FillOnTexture(ref Texture2D texture, Vector2 pos, UnityEngine.Color c, ref bool[,] drawPos)
        {
            var stack = new Stack<Vector2>();

            int x = (int)pos.x,
                y = (int)pos.y;

            var cols = texture.GetPixels();
            var width = texture.width;
            var height = texture.height;

            var selectColor = cols[y * width + x];
            float selectH, selectS, selectV;
            UnityEngine.Color.RGBToHSV(selectColor, out selectH, out selectS, out selectV);

            stack.Push(new Vector2(x, y));

            var d = 5;

            UnityEngine.Color col;
            Vector2 p;
            int xLeft = 0, xRight = width-1;
            float h, s, v;
            while (true)
            {
                if (stack.Count() <= 0) break;

                p = stack.Pop();

                x = (int)p.x;
                y = (int)p.y;
                
                for (int i = -1; x+i >= 0; i--)
                {
                    col = cols[y * width + (x+i)];
                    UnityEngine.Color.RGBToHSV(col, out h, out s, out v);

                    if (drawPos[x+i, y] || Mathf.Abs(h - selectH) > 0.1 || Mathf.Abs(s - selectS) > 0.1 || Mathf.Abs(v - selectV) > 0.1)
                    {
                        xLeft = x + i + 1;
                        break;
                    }
                }

                for (int j = 1; x + j < width; j++)
                {
                    col = cols[y * width + (x + j)];
                    UnityEngine.Color.RGBToHSV(col, out h, out s, out v);

                    if (drawPos[x+j, y] || Mathf.Abs(h - selectH) > 0.1 || Mathf.Abs(s - selectS) > 0.1 || Mathf.Abs(v - selectV) > 0.1)
                    {
                        xRight = x + j - 1;
                        break;
                    }
                }

                for (int k = xLeft; k <= xRight; k++)
                {
                    if (y > 0)
                    {
                        col = cols[(y - 1) * width + k];
                        UnityEngine.Color.RGBToHSV(col, out h, out s, out v);

                        UnityEngine.Debug.LogFormat("{0}, {1}", k, y - 1);

                        if (drawPos[k, y - 1] || Mathf.Abs(h - selectH) > 0.1 || Mathf.Abs(s - selectS) > 0.1 || Mathf.Abs(v - selectV) > 0.1)
                            stack.Push(new Vector2(k - 1, y - 1));
                    }

                    if (y < height - 1)
                    {
                        col = cols[(y + 1) * width + k];
                        UnityEngine.Color.RGBToHSV(col, out h, out s, out v);

                        if (drawPos[k, y + 1] || Mathf.Abs(h - selectH) > 0.1 || Mathf.Abs(s - selectS) > 0.1 || Mathf.Abs(v - selectV) > 0.1)
                            stack.Push(new Vector2(k - 1, y + 1));
                    }
                }
            }

            texture.SetPixels(cols);
            texture.Apply();
            Repaint();
        }
        */

        #region nouse
        // Bitmapを使った方式
        /*
        private void FillOnTexture(ref Texture2D texture, Vector2 pos, UnityEngine.Color c, ref bool[,] drawPos)
        {
            var binaryData = texture.GetRawTextureData();
            var bitmap = new Bitmap(texture.width, texture.height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            Marshal.Copy(binaryData, 0, bitmapData.Scan0, binaryData.Length);
            //bitmap.UnlockBits(bitmapData);

            /*
            var stack = new Stack<Vector2>();

            int x = (int)pos.x,
                y = (int)pos.y;

            var selectColor = texture.GetPixel(x, y);
            float selectH, selectS, selectV;
            UnityEngine.Color.RGBToHSV(selectColor, out selectH, out selectS, out selectV);

            stack.Push(new Vector2(x, y));

            var d = 5;

            UnityEngine.Color col;
            Vector2 p;
            while (true)
            {
                if (stack.Count() <= 0) break;

                p = stack.Pop();

                x = (int)p.x;
                y = (int)p.y;

                byte r = buf[x + y * 4];
                byte g = buf[x + y * 4 + 1];
                byte b = buf[x + y * 4 + 2];
                byte a = buf[x + y * 4 + 3];
                float h, s, v;
                col = new UnityEngine.Color(r, g, b, a);
                UnityEngine.Color.RGBToHSV(col, out h, out s, out v);

                if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) continue;

                if (drawPos[x, y] || Mathf.Abs(h - selectH) > 0.1 || Mathf.Abs(s - selectS) > 0.1 || Mathf.Abs(v - selectV) > 0.1) continue;

                for (int i = -d + 1; i < d - 1; i++)
                {
                    for (int j = -d + 1; j < d - 1; j++)
                    {
                        if (x + i < 0 || x + i > texture.width - 1 ||
                            y + j < 0 || y + j > texture.height - 1) continue;
                        
                        buf[(x + i) + (y + j) * 4] = (byte)c.r;
                        buf[(x + i) + (y + j) * 4 + 1] = (byte)c.g;
                        buf[(x + i) + (y + j) * 4 + 2] = (byte)c.b;
                        buf[(x + i) + (y + j) * 4 + 3] = (byte)c.r;
                        drawPos[x + i, y + j] = true;
                    }
                }

                stack.Push(new Vector2(x + d, y));
                stack.Push(new Vector2(x - d, y));
                stack.Push(new Vector2(x, y + d));
                stack.Push(new Vector2(x, y - d));
            }

            texture.LoadRawTextureData(buf);
            texture.Apply();
            Repaint();
        }
        */
        #endregion

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

            var texX = (int)(windowPos.x * raito);
            var texY = texture.height - (int)(windowPos.y * raito);

            return ScaleOffset(texture, new Vector2(texX, texY));
        }

        private Vector2 ScaleOffset(Texture2D texture, Vector2 pos)
        {
            var x = (texture.width/2 * (1 - zoomScale) + textureOffset.x * texture.width/2) + pos.x * zoomScale;
            var y = (texture.height/2 * (1 - zoomScale) + textureOffset.y * texture.height/2) + pos.y * zoomScale;
            return new Vector2(x, y);
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
            
            Texture2D editTexture = new Texture2D(originTexture.width, originTexture.height);
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
        private bool ImportDeleteMaskTexture(ref Texture2D texture, ref bool[,] deletePos)
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

            if (maskTexture == null) return false;

            for (int j = 0; j < maskTexture.height; j++)
            {
                for (int i = 0; i < maskTexture.width; i++)
                {
                    var isDelete = (maskTexture.GetPixel(i, j) == UnityEngine.Color.black);
                    if (isDelete)
                        texture.SetPixel(i, j, UnityEngine.Color.black);
                    deletePos[i, j] = isDelete;
                }
            }
            texture.Apply();

            return true;
        }

        /// <summary>
        /// マスク画像を書き出す
        /// </summary>
        /// <param name="deletePos"></param>
        private void ExportDeleteMaskTexture(bool[,] deletePos)
        {
            var height = deletePos.GetLength(0);
            var width = deletePos.Length / height;
            var maskTexture = new Texture2D(width, height);

            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    var c = (deletePos[i, j]) ? UnityEngine.Color.black : UnityEngine.Color.white;
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
    }
#endif
}
