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

// MeshDeleterWithTexture v0.7.1

namespace Gatosyocora.MeshDeleterWithTexture
{
#if UNITY_EDITOR
    public class MeshDeleterWithTexture : EditorWindow
    {
        private const float CANVAS_SIZE_RAITO = 0.6f;

        private CanvasView canvasView;
        private ToolView toolView;

        private MeshDeleterWithTextureModel model;

        private LocalizedText localizedText;

        [MenuItem("GatoTool/MeshDeleter with Texture")]
        private static void Open()
        {
            GetWindow<MeshDeleterWithTexture>(nameof(MeshDeleterWithTexture));
        }

        private void OnEnable()
        {
            canvasView = CreateInstance<CanvasView>();
            toolView = CreateInstance<ToolView>();
            model = new MeshDeleterWithTextureModel();
            localizedText = new LocalizedText();
            ChangeLanguage(localizedText.SelectedLanguage);
        }

        private void OnDisable()
        {
            model.Dispose();

            canvasView.Dispose();
            toolView.Dispose();

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
                    var newRenderer = EditorGUILayout.ObjectField(localizedText.Data.rendererLabelText, model.renderer, typeof(Renderer), true) as Renderer;
                    if (check.changed) model.OnChangeRenderer(canvasView, newRenderer);
                }

                EditorGUILayout.Space();

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var selectLanguage = (Language)EditorGUILayout.EnumPopup(localizedText.SelectedLanguage, GUILayout.Width(50));
                    if (check.changed) OnLanguagePopupChanged(selectLanguage);
                }
            }

            EditorGUILayout.Space(20);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    if (model.HasTexture()) canvasView.Render(CANVAS_SIZE_RAITO);
                    else DrawDummyCanvasView(CANVAS_SIZE_RAITO);

                    using (new EditorGUILayout.HorizontalScope())
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var zoomScale = EditorGUILayout.Slider(localizedText.Data.scaleLabelText, canvasView.ZoomScale, 0.1f, 1.0f);
                        if (check.changed) canvasView.ZoomScale = zoomScale;

                        if (GUILayout.Button(localizedText.Data.resetButtonText))
                        {
                            canvasView.ResetScrollOffsetAndZoomScale();
                        }
                    }
                }

                toolView.Render(model, localizedText, canvasView);
            }

            if (Event.current.type == EventType.KeyDown && 
                Event.current.keyCode == KeyCode.Z)
            {
                canvasView.UndoPreviewTexture();
            }
        }

        private void DrawDummyCanvasView(float canvasSizeRaito)
        {
            GUI.Box(
                GUILayoutUtility.GetRect(
                    EditorGUIUtility.currentViewWidth * canvasSizeRaito,
                    EditorGUIUtility.currentViewWidth * canvasSizeRaito
                ),
                string.Empty
            );
        }

        private void DrawNotSupportBuildTarget()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(localizedText.Data.androidNotSupportMessageText);
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
            localizedText.SetLanguage(language);
            toolView.OnChangeLanguage(localizedText);
        }
    }
#endif
}
