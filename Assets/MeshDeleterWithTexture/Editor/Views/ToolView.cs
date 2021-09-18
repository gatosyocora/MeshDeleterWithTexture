using Gatosyocora.MeshDeleterWithTexture.Models;
using System;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Views
{
    public class ToolView : Editor, IDisposable
    {
        private readonly string[] deleteMaskTextureExtensions = { ".png", ".jpg", ".jpeg" };

        private string[] drawTypeTexts;

        public void Render(MeshDeleterWithTextureModel model, LocalizedText localizedText, CanvasView canvasView)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GatoGUILayout.DisabledButton(
                        localizedText.Data.importDeleteMaskButtonText,
                        () => canvasView.deleteMask.ImportDeleteMaskTexture(),
                        !model.HasTexture()
                    );

                    GatoGUILayout.DisabledButton(
                        localizedText.Data.exportDeleteMaskButtonText,
                        () => {
                            canvasView.deleteMask.ExportDeleteMaskTexture();
                            model.SetPreviewTextureToMaterial(ref canvasView.previewTexture);

                            canvasView.uvMap.SetUVMapTexture(model.renderer, model.currentMaterialInfo);
                        },
                        !model.HasTexture()
                    );
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var path = GatoGUILayout.DragAndDropableArea(localizedText.Data.dragAndDropDeleteMaskTextureAreaText, deleteMaskTextureExtensions);
                    if (check.changed) canvasView.deleteMask.ApplyDeleteMaskTextureToBuffer(path);
                }

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var uvMapLineColor = EditorGUILayout.ColorField(localizedText.Data.uvMapLineColorLabelText, canvasView.uvMap.uvMapLineColor);
                        if (check.changed) canvasView.uvMap.SetUVMapLineColor(uvMapLineColor);
                    }

                    if (GUILayout.Button(localizedText.Data.exportUvMapButtonText))
                    {
                        canvasView.uvMap.ExportUVMapTexture();
                    }
                }

                GUILayout.Space(10);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    if (model.HasMaterials())
                        model.materialInfoIndex = EditorGUILayout.Popup(localizedText.Data.textureLabelText, model.materialInfoIndex, model.textureNames);

                    if (check.changed) model.OnChangeMaterial(canvasView);
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField(localizedText.Data.toolsTitleText, EditorStyles.boldLabel);

                EditorGUILayout.LabelField(localizedText.Data.drawTypeLabelText);
                using (var check = new EditorGUI.ChangeCheckScope())
                using (new EditorGUILayout.HorizontalScope())
                {
                    var drawType = (DrawType)GUILayout.Toolbar((int)canvasView.DrawType, drawTypeTexts);
                    if (check.changed) canvasView.DrawType = drawType;
                }

                EditorGUILayout.Space();

                PenEraserGUI(model, localizedText, canvasView);

                EditorGUILayout.Space();

                using (new GatoGUILayout.RightAlignedScope())
                {
                    if (GUILayout.Button(localizedText.Data.inverseFillAreaButtonText))
                    {
                        canvasView.RegisterUndoTexture();
                        canvasView.InverseFillArea();
                    }

                    if (GUILayout.Button(localizedText.Data.clearAllDrawingButtonText))
                    {
                        canvasView.RegisterUndoTexture();

                        canvasView.ClearAllDrawing();

                        model.SetPreviewTextureToMaterial(ref canvasView.previewTexture);
                    }

                    using (new EditorGUI.DisabledGroupScope(!canvasView.undo.canUndo()))
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(localizedText.Data.undoDrawingButtonText))
                        {
                            canvasView.UndoPreviewTexture();
                        }
                    }
                }

                GUILayout.Space(20);

                EditorGUILayout.LabelField(localizedText.Data.modelInformationTitleText, EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField(localizedText.Data.triangleCountLabelText, model.triangleCount + "");
                }

                GUILayout.Space(20);

                OutputMeshGUI(model, localizedText);

                GUILayout.Space(50);

                using (new GatoGUILayout.RightAlignedScope())
                using (new EditorGUI.DisabledGroupScope(model.renderer == null || !PrefabUtility.IsPartOfAnyPrefab(model.renderer)))
                {
                    if (GUILayout.Button(localizedText.Data.revertMeshToPrefabButtonText))
                    {
                        model.RevertMeshToPrefab(canvasView);
                    }
                }

                GUILayout.Space(10f);

                using (new GatoGUILayout.RightAlignedScope())
                using (new EditorGUI.DisabledGroupScope(!model.HasPreviousMesh()))
                {
                    if (GUILayout.Button(localizedText.Data.revertMeshToPreviouslyButtonText))
                    {
                        model.RevertMeshToPreviously(canvasView);
                    }
                }

                EditorGUILayout.Space();

                if (GUILayout.Button(localizedText.Data.deleteMeshButtonText))
                {
                    try
                    {
                        model.OnDeleteMeshButtonClicked(canvasView);
                    }
                    catch (NotFoundVerticesException e)
                    {
                        EditorUtility.DisplayDialog(
                            localizedText.Data.errorDialogTitleText,
                            localizedText.Data.notFoundVerticesExceptionDialogMessageText,
                            localizedText.Data.errorDialogOkText
                        );
                        Debug.LogError(e.Message);
                    }
                    GUIUtility.ExitGUI();
                }
            }
        }

        public void OnChangeLanguage(LocalizedText localizedText)
        {
            drawTypeTexts = new string[] {
                localizedText.Data.penToolNameText,
                localizedText.Data.eraserToolNameText
            };
        }

        public void Dispose() { }

        private void PenEraserGUI(MeshDeleterWithTextureModel model, LocalizedText localizedText, CanvasView canvasView)
        {
            using (new EditorGUI.DisabledGroupScope(!model.HasTexture()))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(localizedText.Data.penColorLabelText);

                if (GUILayout.Button(localizedText.Data.colorBlackButtonText))
                {
                    canvasView.PenColor = Color.black;
                }
                if (GUILayout.Button(localizedText.Data.colorRedButtonText))
                {
                    canvasView.PenColor = Color.red;
                }
                if (GUILayout.Button(localizedText.Data.colorGreenButtonText))
                {
                    canvasView.PenColor = Color.green;
                }
                if (GUILayout.Button(localizedText.Data.colorBlueButtonText))
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
                                localizedText.Data.penEraserSizeLabelText,
                                canvasView.PenSize,
                                1,
                                !model.HasTexture() ? 100 : model.Texture.width / 20);

                if (check.changed)
                {
                    canvasView.PenSize = penSize;
                }
            }
        }

        private void OutputMeshGUI(MeshDeleterWithTextureModel model, LocalizedText localizedText)
        {
            EditorGUILayout.LabelField(localizedText.Data.outputMeshTitleText, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(localizedText.Data.saveFolderLabelText, model.saveFolder);

                    if (GUILayout.Button(localizedText.Data.selectFolderButtonText, GUILayout.Width(100)))
                    {
                        model.SelectFolder();
                    }
                }

                model.meshName = EditorGUILayout.TextField(localizedText.Data.outputFileNameLabelText, model.meshName);
            }
        }
    }
}
