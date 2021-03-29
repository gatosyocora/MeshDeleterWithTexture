﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using Gatosyocora.MeshDeleterWithTexture.Utilities;
using Gatosyocora.MeshDeleterWithTexture.Views;
using Gatosyocora.MeshDeleterWithTexture.Models;

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
        private readonly string[] deleteMaskTextureExtensions = {".png", ".jpg", ".jpeg" };

        private CanvasView canvasView;

        private MeshDeleterWithTextureModel model;

        [MenuItem("GatoTool/MeshDeleter with Texture")]
        private static void Open()
        {
            GetWindow<MeshDeleterWithTexture>(nameof(MeshDeleterWithTexture));
        }

        private void OnEnable()
        {
            canvasView = new CanvasView();
            model = new MeshDeleterWithTextureModel();
        }

        private void OnDisable()
        {
            model.Dispose();

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
                model.renderer = EditorGUILayout.ObjectField("Renderer", model.renderer, typeof(Renderer), true) as Renderer;
                if (check.changed) model.ChangeRenderer(canvasView);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    if (model.HasTexture()) canvasView.Render();
                    else GUI.Box(
                            GUILayoutUtility.GetRect(
                                EditorGUIUtility.currentViewWidth * 0.6f,
                                EditorGUIUtility.currentViewWidth * 0.6f),
                            "");

                    using (new EditorGUILayout.HorizontalScope())
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var zoomScale = EditorGUILayout.Slider("Scale", canvasView.ZoomScale, 0.1f, 1.0f);
                        if (check.changed) canvasView.ZoomScale = zoomScale;

                        if (GUILayout.Button("Reset"))
                        {
                            canvasView.ResetScrollOffsetAndZoomScale();
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
            using (new EditorGUI.DisabledGroupScope(!model.HasTexture()))
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import DeleteMask"))
                    {
                        canvasView.deleteMask.ImportDeleteMaskTexture();
                    }
                    if (GUILayout.Button("Export DeleteMask"))
                    {
                        canvasView.deleteMask.ExportDeleteMaskTexture();
                        model.SetPreviewTextureToMaterial(ref canvasView.previewTexture);

                        var mesh = RendererUtility.GetMesh(model.renderer);
                        var uvMapTex = canvasView.uvMap.GenerateUVMap(mesh, model.currentMaterialInfo, model.texture);
                        canvasView.uvMap.SetUVMapTexture(uvMapTex);
                    }
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var path = GatoGUILayout.DragAndDropableArea("Drag & Drop DeleteMaskTexture", deleteMaskTextureExtensions);
                    if (check.changed) canvasView.deleteMask.ApplyDeleteMaskTextureToBuffer(path);
                }

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var uvMapLineColor = EditorGUILayout.ColorField("UVMap LineColor", canvasView.uvMap.uvMapLineColor);
                        if (check.changed) canvasView.uvMap.SetUVMapLineColor(uvMapLineColor);
                    }

                    if (GUILayout.Button("Export UVMap"))
                    {
                        canvasView.uvMap.ExportUVMapTexture();
                    }
                }

                GUILayout.Space(10);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    if (model.HasTextures())
                        model.materialInfoIndex = EditorGUILayout.Popup("Texture (Material)", model.materialInfoIndex, model.textureNames);

                    if (check.changed) model.ChangeTexture(canvasView);
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("DrawType");
                using (var check = new EditorGUI.ChangeCheckScope())
                using (new EditorGUILayout.HorizontalScope())
                {
                    var drawType = (DrawType)GUILayout.Toolbar((int)canvasView.drawType, Enum.GetNames(typeof(DrawType)));
                    if (check.changed) canvasView.DrawTypeSetting(drawType);
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
                        canvasView.InverseFillArea();
                    }

                    if (GUILayout.Button("Clear All Drawing"))
                    {
                        // TODO: Undo機能を一時的に閉じる
                        // canvasView.undo.RegisterUndoTexture(previewTexture, buffer);

                        canvasView.ClearAllDrawing();

                        model.SetPreviewTextureToMaterial(ref canvasView.previewTexture);
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
                    EditorGUILayout.LabelField("Triangle Count", model.triangleCount + "");
                }

                GUILayout.Space(20);

                OutputMeshGUI();

                GUILayout.Space(50);

                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledGroupScope(model.renderer == null || !PrefabUtility.IsPartOfAnyPrefab(model.renderer)))
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Revert Mesh to Prefab"))
                    {
                        model.RevertMeshToPrefab(canvasView);
                    }
                }

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledGroupScope(!model.HasPreviousMesh()))
                    {
                        if (GUILayout.Button("Revert Mesh to previously"))
                        {
                            model.RevertMeshToPreviously(canvasView);
                        }
                    }
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("Delete Mesh"))
                {
                    model.DeleteMesh(canvasView);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void PenEraserGUI()
        {
            using (new EditorGUI.DisabledGroupScope(!model.HasTexture()))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("PenColor");

                if (GUILayout.Button("Black"))
                {
                    canvasView.SetPenColor(Color.black);
                }
                if (GUILayout.Button("R"))
                {
                    canvasView.SetPenColor(Color.red);
                }
                if (GUILayout.Button("G"))
                {
                    canvasView.SetPenColor(Color.green);
                }
                if (GUILayout.Button("B"))
                {
                    canvasView.SetPenColor(Color.blue);
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var penColor = EditorGUILayout.ColorField(canvasView.penColor);
                    if (check.changed)
                    {
                        canvasView.SetPenColor(penColor);
                    }
                }
            }

            EditorGUILayout.Space();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var penSize = EditorGUILayout.IntSlider(
                                "Pen/Eraser size",
                                canvasView.penSize,
                                1,
                                !model.HasTexture() ? 100 : model.texture.width / 20);

                if (check.changed)
                {
                    canvasView.SetPenSize(penSize);
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
                    EditorGUILayout.LabelField("SaveFolder", model.saveFolder);

                    if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
                    {
                        model.SelectFolder();
                    }
                }

                model.meshName = EditorGUILayout.TextField("Name", model.meshName);
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
    }
#endif
}
