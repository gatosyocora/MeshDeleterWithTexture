using UnityEngine;
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

// MeshDeleterWithTexture v0.6.3

namespace Gatosyocora.MeshDeleterWithTexture
{
#if UNITY_EDITOR
    public class MeshDeleterWithTexture : EditorWindow
    {
        private readonly string[] deleteMaskTextureExtensions = {".png", ".jpg", ".jpeg" };

        private CanvasView canvasView;

        private MeshDeleterWithTextureModel model;

        private LocalizedText localizedText;
        private Language selectLanguage = Language.EN;

        private string[] drawTypeTexts;

        [MenuItem("GatoTool/MeshDeleter with Texture")]
        private static void Open()
        {
            GetWindow<MeshDeleterWithTexture>(nameof(MeshDeleterWithTexture));
        }

        private void OnEnable()
        {
            canvasView = CreateInstance<CanvasView>();
            model = new MeshDeleterWithTextureModel();
            ChangeLanguage(selectLanguage);
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

            using (new EditorGUILayout.HorizontalScope())
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var newRenderer = EditorGUILayout.ObjectField(LocalizedText.Data.rendererLabelText, model.renderer, typeof(Renderer), true) as Renderer;
                    if (check.changed) model.OnChangeRenderer(canvasView, newRenderer);
                }

                EditorGUILayout.Space();

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    selectLanguage = (Language)EditorGUILayout.EnumPopup(selectLanguage, GUILayout.Width(50));
                    if (check.changed) OnLanguagePopupChanged(selectLanguage);
                }
            }

            EditorGUILayout.Space(20);

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
                        var zoomScale = EditorGUILayout.Slider(LocalizedText.Data.scaleLabelText, canvasView.ZoomScale, 0.1f, 1.0f);
                        if (check.changed) canvasView.ZoomScale = zoomScale;

                        if (GUILayout.Button(LocalizedText.Data.resetButtonText))
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
                canvasView.UndoPreviewTexture();
            }
        }

        private void ToolGUI()
        {
            using (new EditorGUI.DisabledGroupScope(!model.HasTexture()))
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(LocalizedText.Data.importDeleteMaskButtonText))
                    {
                        canvasView.deleteMask.ImportDeleteMaskTexture();
                    }
                    if (GUILayout.Button(LocalizedText.Data.exportDeleteMaskButtonText))
                    {
                        canvasView.deleteMask.ExportDeleteMaskTexture();
                        model.SetPreviewTextureToMaterial(ref canvasView.previewTexture);

                        canvasView.uvMap.SetUVMapTexture(model.renderer, model.currentMaterialInfo);
                    }
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var path = GatoGUILayout.DragAndDropableArea(LocalizedText.Data.dragAndDropDeleteMaskTextureAreaText, deleteMaskTextureExtensions);
                    if (check.changed) canvasView.deleteMask.ApplyDeleteMaskTextureToBuffer(path);
                }

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var uvMapLineColor = EditorGUILayout.ColorField(LocalizedText.Data.uvMapLineColorLabelText, canvasView.uvMap.uvMapLineColor);
                        if (check.changed) canvasView.uvMap.SetUVMapLineColor(uvMapLineColor);
                    }

                    if (GUILayout.Button(LocalizedText.Data.exportUvMapButtonText))
                    {
                        canvasView.uvMap.ExportUVMapTexture();
                    }
                }

                GUILayout.Space(10);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    if (model.HasMaterials())
                        model.materialInfoIndex = EditorGUILayout.Popup(LocalizedText.Data.textureLabelText, model.materialInfoIndex, model.textureNames);

                    if (check.changed) model.OnChangeMaterial(canvasView);
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField(LocalizedText.Data.toolsTitleText, EditorStyles.boldLabel);

                EditorGUILayout.LabelField(LocalizedText.Data.drawTypeLabelText);
                using (var check = new EditorGUI.ChangeCheckScope())
                using (new EditorGUILayout.HorizontalScope())
                {
                    var drawType = (DrawType)GUILayout.Toolbar((int)canvasView.DrawType, drawTypeTexts);
                    if (check.changed) canvasView.DrawType = drawType;
                }

                EditorGUILayout.Space();

                PenEraserGUI();

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(LocalizedText.Data.inverseFillAreaButtonText))
                    {
                        canvasView.RegisterUndoTexture();
                        canvasView.InverseFillArea();
                    }

                    if (GUILayout.Button(LocalizedText.Data.clearAllDrawingButtonText))
                    {
                        canvasView.RegisterUndoTexture();

                        canvasView.ClearAllDrawing();

                        model.SetPreviewTextureToMaterial(ref canvasView.previewTexture);
                    }

                    using (new EditorGUI.DisabledGroupScope(!canvasView.undo.canUndo()))
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(LocalizedText.Data.undoDrawingButtonText))
                        {
                            canvasView.UndoPreviewTexture();
                        }
                    }

                }

                GUILayout.Space(20);

                EditorGUILayout.LabelField(LocalizedText.Data.modelInformationTitleText, EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField(LocalizedText.Data.triangleCountLabelText, model.triangleCount + "");
                }

                GUILayout.Space(20);

                OutputMeshGUI();

                GUILayout.Space(50);

                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledGroupScope(model.renderer == null || !PrefabUtility.IsPartOfAnyPrefab(model.renderer)))
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(LocalizedText.Data.revertMeshToPrefabButtonText))
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
                        if (GUILayout.Button(LocalizedText.Data.revertMeshToPreviouslyButtonText))
                        {
                            model.RevertMeshToPreviously(canvasView);
                        }
                    }
                }

                EditorGUILayout.Space();

                if (GUILayout.Button(LocalizedText.Data.deleteMeshButtonText))
                {
                    model.OnDeleteMeshButtonClicked(canvasView);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void PenEraserGUI()
        {
            using (new EditorGUI.DisabledGroupScope(!model.HasTexture()))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(LocalizedText.Data.penColorLabelText);

                if (GUILayout.Button(LocalizedText.Data.colorBlackButtonText))
                {
                    canvasView.PenColor = Color.black;
                }
                if (GUILayout.Button(LocalizedText.Data.colorRedButtonText))
                {
                    canvasView.PenColor = Color.red;
                }
                if (GUILayout.Button(LocalizedText.Data.colorGreenButtonText))
                {
                    canvasView.PenColor = Color.green;
                }
                if (GUILayout.Button(LocalizedText.Data.colorBlueButtonText))
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
                                LocalizedText.Data.penEraserSizeLabelText,
                                canvasView.PenSize,
                                1,
                                !model.HasTexture() ? 100 : model.Texture.width / 20);

                if (check.changed)
                {
                    canvasView.PenSize = penSize;
                }
            }
        }

        private void OutputMeshGUI()
        {
            EditorGUILayout.LabelField(LocalizedText.Data.outputMeshTitleText, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(LocalizedText.Data.saveFolderLabelText, model.saveFolder);

                    if (GUILayout.Button(LocalizedText.Data.selectFolderButtonText, GUILayout.Width(100)))
                    {
                        model.SelectFolder();
                    }
                }

                model.meshName = EditorGUILayout.TextField(LocalizedText.Data.outputFileNameLabelText, model.meshName);
            }
        }

        private void DrawNotSupportBuildTarget()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(LocalizedText.Data.androidNotSupportMessageText);
                GUILayout.FlexibleSpace();
            }
            GUILayout.FlexibleSpace();
        }

        private void OnLanguagePopupChanged(Language language)
        {
            ChangeLanguage(language);
        }

        private void ChangeLanguage(Language language)
        {
            selectLanguage = language;
            LocalizedText.SetLanguage(selectLanguage);
            drawTypeTexts = new string[] {
                LocalizedText.Data.penToolNameText,
                LocalizedText.Data.eraserToolNameText
            };
        }
    }
#endif
}
