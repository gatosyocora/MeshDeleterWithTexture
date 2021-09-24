using Gatosyocora.MeshDeleterWithTexture.Models;
using System;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Views
{
    public class ToolView : Editor, IDisposable
    {
        private readonly string[] deleteMaskTextureExtensions = { ".png", ".jpg", ".jpeg" };
        private const int PADDING_SIZE = 12;

        private string[] drawTypeTexts;

        public void Render(MeshDeleterWithTextureModel model, LocalizedText localizedText, CanvasView canvasView, float toolSizeRaito)
        {
            var width = EditorGUIUtility.currentViewWidth * toolSizeRaito - PADDING_SIZE;
            var widthOption = GUILayout.Width(width);

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope(widthOption))
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

                GatoGUILayout.DragAndDropableArea(
                    localizedText.Data.dragAndDropDeleteMaskTextureAreaText,
                    deleteMaskTextureExtensions,
                    path => canvasView.deleteMask.ApplyDeleteMaskTextureToBuffer(path),
                    widthOption,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)
                );

                GUILayout.Space(10f);

                using (new EditorGUILayout.HorizontalScope(widthOption))
                {
                    GatoGUILayout.ColorField(
                        localizedText.Data.uvMapLineColorLabelText,
                        canvasView.uvMap.uvMapLineColor,
                        uvMapLineColor => canvasView.uvMap.SetUVMapLineColor(uvMapLineColor)
                    );

                    GatoGUILayout.Button(
                        localizedText.Data.exportUvMapButtonText,
                        () => canvasView.uvMap.ExportUVMapTexture()
                    );
                }

                GUILayout.Space(10);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    if (model.HasMaterials())
                        model.materialInfoIndex = EditorGUILayout.Popup(localizedText.Data.textureLabelText, model.materialInfoIndex, model.textureNames, widthOption);

                    if (check.changed) model.OnChangeMaterial(canvasView);
                }

                EditorGUILayout.Space();

                using (new GatoGUILayout.TitleScope(localizedText.Data.toolsTitleText, widthOption))
                {
                    EditorGUILayout.LabelField(localizedText.Data.drawTypeLabelText, widthOption);

                    GatoGUILayout.Toolbar(
                        canvasView.DrawType,
                        drawTypeTexts,
                        drawType => { canvasView.DrawType = drawType; },
                        widthOption
                    );

                    EditorGUILayout.Space();

                    using (new EditorGUI.DisabledGroupScope(!model.HasTexture()))
                    {
                        PenColorChangeGUI(localizedText, canvasView, widthOption);
                    }

                    EditorGUILayout.Space();

                    GatoGUILayout.IntSlider(
                        localizedText.Data.penEraserSizeLabelText,
                        canvasView.PenSize,
                        1,
                        !model.HasTexture() ? 100 : model.Texture.width / 20,
                        penSize => canvasView.PenSize = penSize,
                        widthOption
                    );

                    EditorGUILayout.Space();

                    if (canvasView.DrawType == DrawType.SELECT)
                    {
                        using (new GatoGUILayout.RightAlignedScope(widthOption))
                        {
                            GatoGUILayout.Button(
                                localizedText.Data.inverseSelectAreaButtonText,
                                () => canvasView.selectArea.InverseSelectArea()
                            );

                            GatoGUILayout.Button(
                                localizedText.Data.applySelectAreaButtonText,
                                () => canvasView.ApplySelectArea()
                            );
                        }

                        EditorGUILayout.Space();
                    }

                    using (new GatoGUILayout.RightAlignedScope(widthOption))
                    {
                        GatoGUILayout.Button(
                            localizedText.Data.inverseFillAreaButtonText,
                            () =>
                            {
                                canvasView.RegisterUndoTexture();
                                canvasView.InverseFillArea();
                            }
                        );

                        GatoGUILayout.Button(
                            localizedText.Data.clearAllDrawingButtonText,
                            () =>
                            {
                                canvasView.RegisterUndoTexture();

                                canvasView.ClearAllDrawing();

                                model.SetPreviewTextureToMaterial(ref canvasView.previewTexture);
                            }
                        );

                        GUILayout.FlexibleSpace();

                        GatoGUILayout.DisabledButton(
                            localizedText.Data.undoDrawingButtonText,
                            () => canvasView.UndoPreviewTexture(),
                            !canvasView.undo.canUndo()
                        );
                    }
                }

                GUILayout.Space(20);

                using (new GatoGUILayout.TitleScope(localizedText.Data.modelInformationTitleText, widthOption))
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField(localizedText.Data.triangleCountLabelText, model.triangleCount.ToString(), widthOption);
                }

                GUILayout.Space(20);

                OutputMeshGUI(model, localizedText, widthOption);

                GUILayout.Space(50);

                using (new GatoGUILayout.RightAlignedScope(widthOption))
                {
                    GatoGUILayout.DisabledButton(
                        localizedText.Data.revertMeshToPrefabButtonText,
                        () => model.RevertMeshToPrefab(canvasView),
                        model.renderer == null || !PrefabUtility.IsPartOfAnyPrefab(model.renderer)
                    );

                    GatoGUILayout.DisabledButton(
                        localizedText.Data.revertMeshToPreviouslyButtonText,
                        () => model.RevertMeshToPreviously(canvasView),
                        !model.HasPreviousMesh()
                    );
                }

                EditorGUILayout.Space();

                GatoGUILayout.Button(
                    localizedText.Data.deleteMeshButtonText,
                    () =>
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
                    },
                    widthOption
                );
            }
        }

        public void OnChangeLanguage(LocalizedText localizedText)
        {
            drawTypeTexts = new string[] {
                localizedText.Data.penToolNameText,
                localizedText.Data.eraserToolNameText,
                localizedText.Data.selectToolNameText
            };
        }

        public void Dispose() { }

        private void PenColorChangeGUI(LocalizedText localizedText, CanvasView canvasView, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.HorizontalScope(options))
            {
                EditorGUILayout.LabelField(localizedText.Data.penColorLabelText, GUILayout.Width(80f));

                GatoGUILayout.Button(
                    localizedText.Data.colorBlackButtonText,
                    () => canvasView.PenColor = Color.black
                );
                GatoGUILayout.Button(
                    localizedText.Data.colorRedButtonText,
                    () => canvasView.PenColor = Color.red
                );
                GatoGUILayout.Button(
                    localizedText.Data.colorGreenButtonText,
                    () => canvasView.PenColor = Color.green
                );
                GatoGUILayout.Button(
                    localizedText.Data.colorBlueButtonText,
                    () => canvasView.PenColor = Color.blue
                );

                GatoGUILayout.ColorField(
                    canvasView.PenColor,
                    penColor => { canvasView.PenColor = penColor; }
                );
            }
        }

        private void OutputMeshGUI(MeshDeleterWithTextureModel model, LocalizedText localizedText, params GUILayoutOption[] options)
        {
            using (new GatoGUILayout.TitleScope(localizedText.Data.outputMeshTitleText, options))
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope(options))
                {
                    EditorGUILayout.LabelField(localizedText.Data.saveFolderLabelText, model.saveFolder);

                    GatoGUILayout.Button(
                        localizedText.Data.selectFolderButtonText,
                        () => model.SelectFolder(),
                        GUILayout.Width(100)
                    );
                }

                model.meshName = EditorGUILayout.TextField(localizedText.Data.outputFileNameLabelText, model.meshName, options);
            }
        }
    }
}
