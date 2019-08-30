using System.Collections;
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
        private int penKernelId, eraserKernelId, fillKernelId, fill2KernelId;
        private RenderTexture previewTexture;
        private Texture2D uvMapTex;

        private ComputeShader selectAreaComputeShader;
        private int selectAreaDrawsKernelId;
        private RenderTexture selectAreaRT;
        private ComputeBuffer isDrawBuffer;

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
            ERASER = 3,
        };

        private DRAW_TYPES drawType;

        private enum SELECT_AREA_TYPES
        {
            RECT,
            FLEXIBLE,
            DRAW
        };

        private SELECT_AREA_TYPES selectAreaType;

        private int triangleCount = 0;
        private string saveFolder = "Assets/";
        private string meshName;

        private const int POINT_MAX_NUM = 1000;
        private Vector4[] flexibleSelectAreaPoints = new Vector4[POINT_MAX_NUM];
        private int pointCount = 0;
        private int movingPointIndex = -1;
        private enum SelectAreaMode
        {
            SELECT_AREA,
            IDLE,
            CUT_LINE,
            DELETE_POINT
        };
        private SelectAreaMode mode;

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
            selectAreaType = SELECT_AREA_TYPES.RECT;
            mode = SelectAreaMode.IDLE;

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
                else if (drawType == DRAW_TYPES.SELECT_AREA && selectAreaType == SELECT_AREA_TYPES.RECT)
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
                                dragPos1.x = pos.x;
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
                    else
                    {
                        
                        Repaint();
                    }
                }
                else if (drawType == DRAW_TYPES.SELECT_AREA && selectAreaType == SELECT_AREA_TYPES.FLEXIBLE)
                {
                    var uvPos = ConvertTexturePosToUVPos(texture, pos);

                    if (Event.current.type == EventType.MouseDown)
                    {
                        if (mode != SelectAreaMode.SELECT_AREA && pointCount == 0)
                        {
                            mode = SelectAreaMode.SELECT_AREA;
                            flexibleSelectAreaPoints = new Vector4[100];
                            pointCount = 0;
                            editMat.SetFloat("_IsSelectingArea", 1);
                            editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);
                            editMat.SetInt("_PointNum", pointCount);
                        }


                        if (mode == SelectAreaMode.SELECT_AREA)
                        {
                            // 最初の点と十分に近かったら閉路にして終わる
                            if (pointCount > 0 && Vector4.Distance(flexibleSelectAreaPoints[0], uvPos) < 0.01)
                            {
                                mode = SelectAreaMode.IDLE;
                                editMat.SetFloat("_IsSelectingArea", 0);

                                SortCounterclockwise(ref flexibleSelectAreaPoints, pointCount);
                                editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);

                                editMat.SetVector("_CurrentPos", new Vector4(0, 0, 0, 0));
                            }
                            else
                            {
                                flexibleSelectAreaPoints[pointCount++] = new Vector4(uvPos.x, uvPos.y, 0, 0);

                                editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);
                                editMat.SetInt("_PointNum", pointCount);
                            }

                            
                            Repaint();
                        }
                        // 範囲を選択し終えた後
                        else if (pointCount > 0)
                        {
                            if (mode == SelectAreaMode.CUT_LINE)
                            {
                                for (int i = 0; i < pointCount; i++)
                                {
                                    Vector4 p1, p2;
                                    if (i != pointCount - 1)
                                    {
                                        p1 = flexibleSelectAreaPoints[i];
                                        p2 = flexibleSelectAreaPoints[i + 1];
                                    }
                                    else
                                    {
                                        p1 = flexibleSelectAreaPoints[pointCount - 1];
                                        p2 = flexibleSelectAreaPoints[0];

                                    }
                                    var uvPosVec4 = new Vector4(uvPos.x, uvPos.y, 0, 0);
                                    float inner = Vector4.Dot(Vector4.Normalize(p2 - p1), Vector4.Normalize(uvPosVec4 - p1));

                                    if (inner > 0.999 && inner <= 1)
                                    {
                                        var flexibleSelectAreaPointList = flexibleSelectAreaPoints.ToList();
                                        flexibleSelectAreaPointList.Insert(i + 1, uvPosVec4);
                                        pointCount++;
                                        flexibleSelectAreaPoints = flexibleSelectAreaPointList.ToArray();
                                        Array.Resize(ref flexibleSelectAreaPoints, POINT_MAX_NUM);

                                        editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);
                                        editMat.SetInt("_PointNum", pointCount);

                                        break;
                                    }
                                }

                                mode = SelectAreaMode.IDLE;
                            }
                            else if (mode == SelectAreaMode.DELETE_POINT)
                            {
                                for (int i = 0; i < pointCount; i++)
                                {
                                    if (Vector4.Distance(flexibleSelectAreaPoints[i], uvPos) < 0.005)
                                    {
                                        var flexibleSelectAreaPointList = flexibleSelectAreaPoints.ToList();
                                        flexibleSelectAreaPointList.RemoveAt(i);
                                        pointCount--;
                                        flexibleSelectAreaPoints = flexibleSelectAreaPointList.ToArray();
                                        Array.Resize(ref flexibleSelectAreaPoints, POINT_MAX_NUM);

                                        editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);
                                        editMat.SetInt("_PointNum", pointCount);

                                        break;
                                    }
                                }

                                mode = SelectAreaMode.IDLE;
                            }
                            else
                            {
                                isMouseDowning = true;

                                for (int i = 0; i < pointCount; i++)
                                {
                                    if (Vector4.Distance(flexibleSelectAreaPoints[i], uvPos) < 0.005)
                                    {
                                        movingPointIndex = i;
                                        break;
                                    }
                                }

                                isAreaSizeChanging = (movingPointIndex != -1);
                            }
                        }
                    }
                    else if (Event.current.type == EventType.MouseUp)
                    {
                        isAreaSizeChanging = false;
                        isMouseDowning = false;
                        movingPointIndex = -1;
                    }

                    if (isMouseDowning && isAreaSizeChanging)
                    {
                        flexibleSelectAreaPoints[movingPointIndex] = new Vector4(uvPos.x, uvPos.y, 0, 0);
                        editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);

                        
                        Repaint();
                    }
                }
                else if (drawType == DRAW_TYPES.SELECT_AREA && selectAreaType == SELECT_AREA_TYPES.DRAW)
                {
                    var uvPos = ConvertTexturePosToUVPos(texture, pos);
                    editMat.SetVector("_CurrentPos", new Vector4(uvPos.x, uvPos.y, 0, 0));

                    if (Event.current.type == EventType.MouseDown)
                    {
                        isDrawing = true;
                    }
                    else if (Event.current.type == EventType.MouseUp)
                    {
                        isDrawing = false;

                        var points = EstimateAreaWithDrawResult0(isDrawBuffer);
                        pointCount = points.Length;

                        UnityEngine.Debug.Log(pointCount);

                        flexibleSelectAreaPoints = new Vector4[POINT_MAX_NUM];
                        Array.Copy(points, flexibleSelectAreaPoints, pointCount);
                        editMat.SetTexture("_SelectTex", null);
                        editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);
                        editMat.SetInt("_PointNum", pointCount);

                        isDrawBuffer.Release();

                        selectAreaType = SELECT_AREA_TYPES.FLEXIBLE;
                        mode = SelectAreaMode.IDLE;
                        editMat.SetFloat("_IsSelectingArea", 0);

                        editMat.SetVector("_CurrentPos", new Vector4(0, 0, 0, 0));
                    }

                    if (isDrawing)
                        SelectAreaWithDrawing(pos);
                }
                /*
                else if (drawType == DRAW_TYPES.SELECT_B)
                {
                    if (Event.current.type == EventType.MouseDown)
                    {
                        ComputeShader cs = Instantiate(Resources.Load<ComputeShader>("selectAreaNshape")) as ComputeShader;
                        int kernel = cs.FindKernel("CSMain");

                        RenderTexture selectAreaTex = new RenderTexture(texture.width, texture.height, 0);
                        selectAreaTex.enableRandomWrite = true;
                        selectAreaTex.Create();

                        cs.SetInt("Width", texture.width);
                        cs.SetInt("Height", texture.height);
                        cs.SetTexture(kernel, "Tex", texture);

                        int[] posArray = new int[2 * sizeof(int)];
                        posArray[0 * sizeof(int)] = (int)pos.x;
                        posArray[1 * sizeof(int)] = (int)pos.y;
                        cs.SetInts("Pos", posArray);

                        int N = 8;
                        var cb = new ComputeBuffer(N, sizeof(int) * 2);
                        cs.SetBuffer(kernel, "Points", cb);
                        cs.SetTexture(kernel, "Result", selectAreaTex);
                        cs.SetInt("N", N);

                        cs.Dispatch(kernel, N, 1, 1);

                        Material negaposiMat = new Material(Shader.Find("Gato/NegaPosi"));
                        negaposiMat.SetTexture("_MaskTex", selectAreaTex);
                        negaposiMat.SetFloat("_Inverse", 1);
                        Graphics.Blit(texture, previewTexture, negaposiMat);

                        selectAreaTex.Release();
                        cb.Release();

                        
                        Repaint();
                    }
                }
                */
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

                    if (drawType == DRAW_TYPES.CHOOSE_COLOR || drawType == DRAW_TYPES.SELECT_AREA)
                    {
                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            selectAreaType = (SELECT_AREA_TYPES)GUILayout.Toolbar((int)selectAreaType, Enum.GetNames(typeof(SELECT_AREA_TYPES)));

                            if (check.changed)
                            {
                                SelectAreaSetting(selectAreaType);
                            }
                        }

                        if (selectAreaType == SELECT_AREA_TYPES.FLEXIBLE)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button("Cut line"))
                                {
                                    mode = SelectAreaMode.CUT_LINE;
                                }
                                if (GUILayout.Button("Delete point"))
                                {
                                    mode = SelectAreaMode.DELETE_POINT;
                                }
                                if (GUILayout.Button("Reset Selecting"))
                                {
                                    flexibleSelectAreaPoints = new Vector4[POINT_MAX_NUM];
                                    pointCount = 0;

                                    editMat.SetVectorArray("_Points", flexibleSelectAreaPoints);
                                    editMat.SetInt("_PointNum", pointCount);
                                }
                            }
                        }

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
                    if (selectAreaType == SELECT_AREA_TYPES.RECT)
                        FillOnTexture(ref texture, targetColor, dragPos1, dragPos2, hsvThreshold);
                    else if (selectAreaType == SELECT_AREA_TYPES.FLEXIBLE)
                        FillOnTexture2(ref texture, targetColor, flexibleSelectAreaPoints, pointCount, hsvThreshold);

                }
            }

            if (GUILayout.Button("Fill"))
            {
                if (selectAreaType == SELECT_AREA_TYPES.RECT)
                    FillOnTexture(ref texture, targetColor, dragPos1, dragPos2, hsvThreshold);
                else if (selectAreaType == SELECT_AREA_TYPES.FLEXIBLE)
                    FillOnTexture2(ref texture, targetColor, flexibleSelectAreaPoints, pointCount, hsvThreshold);
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

            /*
            var count = 0;

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
            }

            /*
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
            cs.Dispatch(kernel, uvs.Count(), 1, 1);

            ComputeBuffer.CopyCount(deleteVertexIndexBuffer, countBuffer, 0);
            countBuffer.GetData(itemCount);
            var size = itemCount[0];
            var deleteVertexIndexs = new int[size];
            deleteVertexIndexBuffer.GetData(deleteVertexIndexs);

            uvBuffer.Release();
            deleteVertexIndexBuffer.Release();
            countBuffer.Release();

            deleteIndexList = deleteVertexIndexs.ToList();

            sw.Stop();
            var gpuSpeed = sw.ElapsedMilliseconds;
            
            UnityEngine.Debug.LogFormat("cpu:{0}, gpu:{1}", cpuSpeed, gpuSpeed);

            */

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

            var count = 0;

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

        private void FillOnTexture2(ref Texture2D texture, Color targetColor, Vector4[] points, int pointNum, HSV hsvThreshold)
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

            var pointBuffer = new ComputeBuffer(POINT_MAX_NUM, Marshal.SizeOf(typeof(Vector4)));
            pointBuffer.SetData(points);
            computeShader.SetBuffer(fill2KernelId, "Points", pointBuffer);

            computeShader.SetInt("PointNum", pointNum);

            computeShader.Dispatch(fill2KernelId, texture.width / 32, texture.height / 32, 1);

            pointBuffer.Release();

            
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
            fillKernelId = computeShader.FindKernel("CSFill");
            fill2KernelId = computeShader.FindKernel("CSFill2");

            selectAreaComputeShader = Instantiate(Resources.Load<ComputeShader>("selectArea")) as ComputeShader;
            selectAreaDrawsKernelId = selectAreaComputeShader.FindKernel("SelectAreaDraw");
        }

        private void InitComputeBuffer(Texture2D texture)
        {
            if (buffer != null) buffer.Release();
            buffer = new ComputeBuffer(texture.width * texture.height, sizeof(int));
            computeShader.SetBuffer(penKernelId, "Result", buffer);
            computeShader.SetBuffer(eraserKernelId, "Result", buffer);
            computeShader.SetBuffer(fillKernelId, "Result", buffer);
            computeShader.SetBuffer(fill2KernelId, "Result", buffer);
        }

        private void SetupComputeShader(ref Texture2D texture, ref RenderTexture previewTexture)
        {
            InitComputeBuffer(texture);

            computeShader.SetTexture(penKernelId, "Tex", texture);
            computeShader.SetTexture(eraserKernelId, "Tex", texture);
            computeShader.SetTexture(fillKernelId, "Tex", texture);
            computeShader.SetTexture(fill2KernelId, "Tex", texture);
            computeShader.SetInt("Width", texture.width);
            computeShader.SetInt("Height", texture.height);

            computeShader.SetTexture(penKernelId, "PreviewTex", previewTexture);
            computeShader.SetTexture(eraserKernelId, "PreviewTex", previewTexture);
            computeShader.SetTexture(fillKernelId, "PreviewTex", previewTexture);
            computeShader.SetTexture(fill2KernelId, "PreviewTex", previewTexture);
        }

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

        /// <summary>
        /// 点を反時計回り順にソート
        /// </summary>
        /// <param name="points">反時計または時計回りにソートされている点群</param>
        /// <param name="pointNum"></param>
        /// <returns>ソートをおこなったらtrue</returns>
        // Surveyor’s Area Formula
        // Surveyor’s Area FormulaE
        // https://web.archive.org/web/20121107190918/http://www.maa.org/pubs/Calc_articles/ma063.pdf
        private bool SortCounterclockwise(ref Vector4[] points, int pointNum)
        {
            // 適当に3点取ってきて反時計回りか調べる
            Vector4 p0, p1, p2;
            p0 = points[0];
            p1 = points[pointNum / 3];
            p2 = points[pointNum / 3 * 2];

            bool isCounterclockwise = ((p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y) >= 0);

            if (!isCounterclockwise)
            {
                var reversePoints = points.Take(pointNum).Reverse().ToArray();
                Array.Copy(reversePoints, points, pointNum);

                return true;
            }

            return false;

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

        // 範囲選択をするための準備
        private void SelectAreaSetting (SELECT_AREA_TYPES type)
        {
            if (type == SELECT_AREA_TYPES.DRAW)
            {
                if (selectAreaRT != null) selectAreaRT.Release();
                selectAreaRT = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                selectAreaRT.enableRandomWrite = true;
                selectAreaRT.Create();

                if (isDrawBuffer != null) isDrawBuffer.Release();
                selectAreaComputeShader.SetTexture(selectAreaDrawsKernelId, "DrawTex", selectAreaRT);
                isDrawBuffer = new ComputeBuffer(selectAreaRT.width * selectAreaRT.height, sizeof(int));
                var isDraw = new int[selectAreaRT.width * selectAreaRT.height];
                isDrawBuffer.SetData(isDraw);
                selectAreaComputeShader.SetBuffer(selectAreaDrawsKernelId, "isDraw", isDrawBuffer);
                selectAreaComputeShader.SetInt("Width", texture.width);

                editMat.SetTexture("_SelectTex", selectAreaRT);
            }
        }

        // 範囲選択用の線を引く
        private void SelectAreaWithDrawing(Vector2 pos)
        {
            var posArray = new int[2 * sizeof(int)];
            posArray[0 * sizeof(int)] = (int)pos.x;
            posArray[1 * sizeof(int)] = (int)pos.y;
            selectAreaComputeShader.SetInts("Pos", posArray);

            selectAreaComputeShader.Dispatch(selectAreaDrawsKernelId, selectAreaRT.width / 32, selectAreaRT.height / 32, 1);

            
            Repaint();
        }

        /// <summary>
        /// 点群の凸包を計算する
        /// Graham’s scan
        /// http://www-ikn.ist.hokudai.ac.jp/~k-sekine/slides/convexhull.pdf
        /// </summary>
        /// <param name="isDrawBuffer"></param>
        /// <returns></returns>
        private Vector4[] EstimateAreaWithDrawResult(ComputeBuffer isDrawBuffer)
        {
            int width = selectAreaRT.width;

            int[] isDraw = new int[selectAreaRT.width * selectAreaRT.height];
            isDrawBuffer.GetData(isDraw);

            // 画像上に描かれた線を構成する点のuv座標位置のリスト
            var drawPos
                = isDraw
                    .Select((value, index) => new { Value = value, Pos = new Vector2(index % width, index / width) })
                    .Where(v => v.Value == 1 && (v.Pos.x >= 0 && v.Pos.x < selectAreaRT.width) && (v.Pos.y >= 0 && v.Pos.y < selectAreaRT.height))
                    .Select(v => ConvertTexturePosToUVPos(texture, v.Pos))
                    .ToList();

            // xが最大でyが最小な点を基点にする
            var maxX = drawPos.Max(v => v.x);
            var minY = drawPos.Where(v => v.x == maxX).Min(v => v.y);
            var originPointIndex = drawPos.Select((value, index) => new { Value = value, Index = index }).Where(v => v.Value.x == maxX && v.Value.y == minY).First().Index;

            // 基点を原点と見たときの偏角順に並べる
            var pointIndexSortedByDeclination
                = drawPos
                    .Select((value, index) => new { Value = value, Index = index })
                    .Where(v => v.Index != originPointIndex)
                    .OrderBy(v => Mathf.Atan((v.Value - drawPos[originPointIndex]).y / (v.Value - drawPos[originPointIndex]).x))
                    .Select(v => v.Index)
                    .ToArray();

            var pointIndexs = new List<int>();
            pointIndexs.Add(originPointIndex);

            var startIndex = 2;
            var d = 0.01f;

            pointIndexs.Add(pointIndexSortedByDeclination[0]);
            pointIndexs.Add(pointIndexSortedByDeclination[1]);

            // 各点に対して凸包多角形の頂点か調べる
            // 最後は基点を新しい点として処理をおこなう
            for (int i = startIndex; i <= pointIndexSortedByDeclination.Length; i++)
            {
                int nextPointIndex;

                if (i < pointIndexSortedByDeclination.Length)
                    nextPointIndex = pointIndexSortedByDeclination[i];
                else
                    nextPointIndex = originPointIndex;

                var p2 = drawPos[nextPointIndex];

                while (pointIndexs.Count >= 2)
                {
                    var p1 = drawPos[pointIndexs[pointIndexs.Count - 1]];
                    var p0 = drawPos[pointIndexs[pointIndexs.Count - 2]];

                    // p0, p1, p2が左回りならp2を追加
                    // 右回りならp1を除外する
                    var outer = Vector3.Normalize(Vector3.Cross(p0 - p1, p2 - p1)).z;

                    if (outer < 0)
                    {
                        if (Vector2.Distance(p1, p2) > d && i < pointIndexSortedByDeclination.Length)
                            pointIndexs.Add(nextPointIndex);

                        break;
                    }
                    else
                    {
                        pointIndexs.RemoveAt(pointIndexs.Count - 1);
                    }
                }
            }

            return pointIndexs
                        .Select(i => new Vector4(drawPos[i].x, drawPos[i].y, 0, 0)).ToArray();

        }

        /// <summary>
        /// 点群の凹包を計算する（k近傍法）
        /// </summary>
        /// <param name="isDrawBuffer"></param>
        /// <returns></returns>
        private Vector4[] EstimateAreaWithDrawResult2(ComputeBuffer isDrawBuffer)
        {
            int width = selectAreaRT.width;

            int[] isDraw = new int[selectAreaRT.width * selectAreaRT.height];
            isDrawBuffer.GetData(isDraw);

            var drawPos
                = isDraw
                    .Select((value, index) => new { Value = value, Pos = new Vector2(index % width, index / width) })
                    .Where(v => v.Value == 1 && (v.Pos.x >= 0 && v.Pos.x < selectAreaRT.width) && (v.Pos.y >= 0 && v.Pos.y < selectAreaRT.height))
                    .Select(v => ConvertTexturePosToUVPos(texture, v.Pos))
                    .ToList();

            // yが最小な点を基点にする
            var originPointIndex = drawPos.Select((value, index) => new { Value = value, Index = index }).OrderBy(v => v.Value.y).First().Index;

            var pointIndexs = new List<int>();
            pointIndexs.Add(originPointIndex);

            var k = 4;

            for (int i = 0; i < drawPos.Count; i++)
            {
                if (i == originPointIndex) continue;

                var p1 = drawPos[pointIndexs.Count - 1];

                // p1に近いk点を取得
                var kNearPoints
                            = drawPos
                                .Select((value, index) => new { Value = value, Index = index })
                                .Where(v => v.Index != originPointIndex)
                                .OrderBy(v => Vector2.Distance(v.Value, p1))
                                .Where(v => Vector2.Distance(v.Value, p1) >= 0.05f)
                                .Take(k)
                                .ToList();

                if (kNearPoints.Count() <= 0) continue;

                int nextPointIndex;
                if (pointIndexs.Count > 2)
                {
                    var p0 = drawPos[pointIndexs.Count - 2];

                    // k点のうち最も右にある点を選ぶ
                    nextPointIndex
                                = kNearPoints
                                    .OrderByDescending(v => Vector3.Dot(Vector3.Normalize(p1 - p0), Vector3.Normalize(v.Value - p1)))
                                    .Select(v => v.Index)
                                    .First();
                }
                else
                {
                    // 最も右にある点を選ぶ
                    nextPointIndex
                                = kNearPoints
                                    .OrderBy(v => Mathf.Atan((v.Value - p1).y / (v.Value - p1).x))
                                    .Select(v => v.Index)
                                    .First();
                }


                pointIndexs.Add(nextPointIndex);

            }

            var resultPoints = pointIndexs
                        .Select(i => new Vector4(drawPos[i].x, drawPos[i].y, 0, 0)).ToArray();

            return resultPoints;

            //var originPointVec4 = new Vector4(drawPos[originPointIndex].x, drawPos[originPointIndex].y, 0, 0);

            // 基点を原点と見たときの偏角順に並べる
            //var orderedPoints = resultPoints
                                    //.OrderBy(v => Mathf.Atan((v - originPointVec4).y / (v - originPointVec4).x))
                                    //.ToArray();

        }

        /// <summary>
        /// 点群の凹包を計算する（直線近似）
        /// </summary>
        /// <param name="isDrawBuffer"></param>
        /// <returns></returns>
        private Vector4[] EstimateAreaWithDrawResult3(ComputeBuffer isDrawBuffer)
        {
            int width = selectAreaRT.width;

            int[] isDraw = new int[selectAreaRT.width * selectAreaRT.height];
            isDrawBuffer.GetData(isDraw);

            // 画像上に描かれた線を構成する点のuv座標位置のリスト
            var drawPos
                = isDraw
                    .Select((value, index) => new { Value = value, Pos = new Vector2(index % width, index / width) })
                    .Where(v => v.Value == 1 && (v.Pos.x >= 0 && v.Pos.x < selectAreaRT.width) && (v.Pos.y >= 0 && v.Pos.y < selectAreaRT.height))
                    .Select(v => ConvertTexturePosToUVPos(texture, v.Pos))
                    .ToList();

            // xが最小な点を基点とする
            var minX = drawPos.Min(v => v.x);
            var originPointIndex = drawPos.Select((value, index) => new { Value = value, Index = index }).Where(v => v.Value.x == minX).First().Index;

            // 基点を原点と見たときの偏角順に並べる
            /*var pointIndexSortedByDeclination
                = drawPos
                    .Select((value, index) => new { Value = value, Index = index })
                    .OrderBy(v => Mathf.Atan((v.Value-drawPos[originPointIndex]).y / (v.Value- drawPos[originPointIndex]).x))
                    .Select(v => v.Index)
                    .ToArray();
            */

            var pointIndexSortedByDeclinationList = new List<int>();
            var upArea = new List<int>();
            var lowArea = new List<int>();

            for (int i = 0; i < width; i++)
            {
                var enterPoints = drawPos.Where(v => v.x * width == i).OrderBy(v => v.y).ToArray();
                //enterP
            }

            var pointIndexSortedByDeclination = pointIndexSortedByDeclinationList.ToArray();

            // 距離が十分に近いものは除外する
            /*
            var pointIndexSortedByDeclinationList = pointIndexSortedByDeclination.ToList();
            for (int i = pointIndexSortedByDeclinationList.Count-1; i > 0; i--)
            {
                if (Vector2.Distance(drawPos[pointIndexSortedByDeclinationList[i]], drawPos[pointIndexSortedByDeclinationList[i - 1]]) <= 0.01f)
                    pointIndexSortedByDeclinationList.RemoveAt(i);
            }
            pointIndexSortedByDeclination = pointIndexSortedByDeclinationList.ToArray();
            */

            var pointIndexs = new List<int>();
            pointIndexs.Add(originPointIndex);
            pointIndexs.Add(pointIndexSortedByDeclination[0]);

            float t = 0.6f;

            int nextPointIndex;
            // リストに入った2点p0, p1の線分01と新しい点p2とp1からできる線分12が
            // ある程度まっすぐならp1を除去する
            for (int i = 1; i <= pointIndexSortedByDeclination.Length; i++)
            {

                if (i < pointIndexSortedByDeclination.Length)
                    nextPointIndex = pointIndexSortedByDeclination[i];
                else
                    nextPointIndex = originPointIndex;

                var p2 = drawPos[nextPointIndex];

                var p1 = drawPos[pointIndexs[pointIndexs.Count - 1]];
                var p0 = drawPos[pointIndexs[pointIndexs.Count - 2]];

                // ある程度まっすぐならp1を除去
                if (Vector3.Dot(Vector3.Normalize(p1-p0), Vector3.Normalize(p2-p1)) > t)
                    pointIndexs.RemoveAt(pointIndexs.Count - 1);

                if (i != pointIndexSortedByDeclination.Length)
                    pointIndexs.Add(nextPointIndex);
            }

            return pointIndexs
                        .Select(i => new Vector4(drawPos[i].x, drawPos[i].y, 0, 0)).ToArray();

        }

        private Vector4[] EstimateAreaWithDrawResult0(ComputeBuffer isDrawBuffer)
        {
            int width = selectAreaRT.width;

            int[] isDraw = new int[selectAreaRT.width * selectAreaRT.height];
            isDrawBuffer.GetData(isDraw);

            // 画像上に描かれた線を構成する点のuv座標位置のリスト
            var drawPos
                = isDraw
                    .Select((value, index) => new { Value = value, Pos = new Vector2(index % width, index / width) })
                    .Where(v => v.Value == 1 && (v.Pos.x >= 0 && v.Pos.x < selectAreaRT.width) && (v.Pos.y >= 0 && v.Pos.y < selectAreaRT.height))
                    .Select(v => ConvertTexturePosToUVPos(texture, v.Pos))
                    .Select(v => new Vector4(v.x, v.y, 0, 0))
                    .ToList();

            // xが最小な点を基点とする
            var minX = drawPos.Min(v => v.x);
            var originPointIndex = drawPos.Select((value, index) => new { Value = value, Index = index }).Where(v => v.Value.x == minX).First().Index;

            var pointIndexSortedByDeclinationList = new List<int>();

            var upAreaIndexs = new List<int>(); // 上用のリスト
            var lowAreaIndexs = new List<int>(); // 下用のリスト

            var areaList = new List<List<int>>();

            upAreaIndexs.Add(originPointIndex);

            areaList.Add(upAreaIndexs);
            areaList.Add(lowAreaIndexs);

            float e = 0.1f;

            for (int i = 0; i < width; i++)
            {
                // 同じx座標にある点のインデックスを取得する(y座標で降順ソート)
                var enterPointIndexs 
                    = drawPos
                        .Select((value, index) => new { Value = value, Index = index })
                        .Where(v => v.Value.x * width == i)
                        .OrderByDescending(v => v.Value.y)
                        .Select(v => v.Index)
                        .ToArray();

                // 各頂点をどのエリアのリストに入れるか決める
                for (int j = 0; j < enterPointIndexs.Count(); j++)
                {
                    // 初めて入れる場合は最初の点の上下で決める
                    if (areaList[0].Count <= 1 || areaList[1].Count <= 0)
                    {
                        if (drawPos[originPointIndex].y >= drawPos[enterPointIndexs[j]].y)
                            areaList[0].Add(enterPointIndexs[j]);
                        else
                            areaList[1].Add(enterPointIndexs[j]);

                        continue;
                    }

                    var minDistAreaIndex = -1;
                    float minDistance = width;

                    // 一番近くのリストに入れる
                    for (int a = 0; a < areaList.Count; a++)
                    {
                        float dist;
                        int areaIndex = a;
                        int calcIndex = a;

                        if (areaList[a].Count <= 0)
                        {
                            // リストが空なのが上方向だったら下方向の値で上下を計算する
                            if (a % 2 == 0)
                            {
                                calcIndex = a + 1;

                                if (drawPos[areaList[calcIndex].Last()].y < drawPos[enterPointIndexs[j]].y)
                                {
                                    areaIndex = a + 1;
                                }
                            }
                            else
                            {
                                calcIndex = a - 1;

                                if (drawPos[areaList[calcIndex].Last()].y >= drawPos[enterPointIndexs[j]].y)
                                {
                                    areaIndex = a - 1;
                                }
                            }
                        }

                        dist = Vector4.Distance(drawPos[areaList[calcIndex].Last()], drawPos[enterPointIndexs[j]]);

                        if (minDistance > dist)
                        {
                            minDistAreaIndex = areaIndex;
                            minDistance = dist;
                        }
                    }

                    // どのリストの最後の頂点よりもeより距離があったら
                    // 凸の頂点であるとして処理する
                    if (minDistance > e)
                    {
                        var newUpAreaIndexs = new List<int>();
                        var newLowAreaIndexs = new List<int>();

                        areaList.Add(newUpAreaIndexs);
                        areaList.Add(newLowAreaIndexs);

                        minDistAreaIndex = areaList.Count - 1;
                    }

                    areaList[minDistAreaIndex].Add(enterPointIndexs[j]);
                }
            }

            pointIndexSortedByDeclinationList.AddRange(areaList[0]);

            bool isReverse = true;

            for (int i = areaList.Count-1; i >= 1; i--)
            {
                if (isReverse)
                    areaList[i].Reverse();

                pointIndexSortedByDeclinationList.AddRange(areaList[i]);

                isReverse = !isReverse;
            }

            var pointIndexSortedByDeclination = pointIndexSortedByDeclinationList.ToArray();

            drawPos = pointIndexSortedByDeclination.Select(i => drawPos[i]).ToList();

            var points = drawPos.ToArray();

            AdjustmentAreaPoints(ref points);

            return points;
        }

        /// <summary>
        /// 範囲選択用の図形の点を減らす
        /// </summary>
        /// <param name="points"></param>
        private void AdjustmentAreaPoints(ref Vector4[] points)
        {
            if (points.Length <= 2) return;

            var d = 0.01f;

            var angle = 10;
            var r = 1f - (angle / 180f);

            var pointList = points.ToList();

            for (int i = pointList.Count - 2; i >= 1; i--)
            {
                var p2 = pointList[i + 1];
                var p1 = pointList[i];
                var p0 = pointList[i - 1];

                // 3点が直線に並んでいたら真ん中の点を削除
                if (Mathf.Abs(Vector4.Dot(Vector4.Normalize(p2 - p1), Vector4.Normalize(p1 - p0))) > r)
                    pointList.RemoveAt(i);
            }

            for (int i = pointList.Count - 1; i >= 1; i--)
            {
                var p0 = pointList[i];
                var p1 = pointList[i-1];

                // 3点が直線に並んでいたら真ん中の点を削除
                if (Vector4.Distance(p0, p1) < d)
                    pointList.RemoveAt(i);
            }

            points = pointList.ToArray();
        }

        #endregion

    }
#endif
}
