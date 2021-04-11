using Gatosyocora.MeshDeleterWithTexture.Models;
using Gatosyocora.MeshDeleterWithTexture.Utilities;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Views
{
    public class CanvasView : Editor, IDisposable
    {
        private static Material editMat;
        public Texture2D editTexture;
        public RenderTexture previewTexture;
        public Material previewMaterial;

        private bool isDrawing = false;
        private Vector2Int textureSize;

        private MaterialInfo materialInfo;

        public DrawType DrawType { get; set; }

        private Color _penColor;
        public Color PenColor
        {
            get => _penColor;
            set 
            {
                _penColor = value;
                canvasModel.SetPen(_penSize, value);
            }
        }

        private int _penSize;
        public int PenSize
        {
            get => _penSize;
            set
            {
                _penSize = value;
                editMat.SetFloat("_PenSize", value / (float)textureSize.x);
                canvasModel.SetPen(value, _penColor);
            }
        }

        private Vector4 _scrollOffset;
        public Vector4 ScrollOffset
        {
            get => _scrollOffset;
            private set
            {
                editMat.SetVector("_Offset", value);
                _scrollOffset = value;
            }
        }
        private float _zoomScale;
        public float ZoomScale 
        {
            get => _zoomScale;
            set {
                editMat.SetFloat("_TextureScale", value);
                _zoomScale = value;
            }
        }

        private CanvasModel canvasModel;

        public UndoCanvas undo;
        public UVMapCanvas uvMap;
        public DeleteMaskCanvas deleteMask;

        public void OnEnable()
        {
            editMat = Resources.Load<Material>("TextureEditMat");
        }

        public CanvasView()
        {
            undo = new UndoCanvas();
            uvMap = new UVMapCanvas(ref editMat);
            canvasModel = new CanvasModel();

            DrawType = DrawType.PEN;
            PenColor = Color.black;
        }

        public void Initialize(MaterialInfo materialInfo, Renderer renderer)
        {
            this.materialInfo = materialInfo;
            textureSize = new Vector2Int(materialInfo.Texture.width, materialInfo.Texture.height);

            editMat.SetFloat("_ApplyGammaCorrection", Convert.ToInt32(PlayerSettings.colorSpace == ColorSpace.Linear));
            editMat.SetInt("_PointNum", 0);

            editMat.SetVector("_StartPos", new Vector4(0, 0, 0, 0));
            editMat.SetVector("_EndPos", new Vector4(textureSize.x - 1, textureSize.y - 1, 0, 0));

            editMat.SetTexture("_SelectTex", null);

            PenSize = 20;

            InitializeDrawArea(materialInfo, renderer);
        }

        public void Render()
        {
            if (textureSize == null) return;

            var width = EditorGUIUtility.currentViewWidth * 0.6f;
            var height = width * textureSize.y / textureSize.x;
            EventType mouseEventType = 0;
            Rect rect = new Rect(0, 0, 0, 0);
            var delta = GatoGUILayout.MiniMonitor(previewTexture, width, height, ref rect, ref mouseEventType, true);

            if (rect.Contains(Event.current.mousePosition))
            {
                // テクスチャの拡大縮小機能
                if (mouseEventType == EventType.ScrollWheel)
                {
                    ZoomScale += Mathf.Sign(delta.y) * 0.1f;

                    if (ZoomScale > 1) ZoomScale = 1;
                    else if (ZoomScale < 0.1f) ZoomScale = 0.1f;

                    // 縮小ではOffsetも中心に戻していく
                    if (Mathf.Sign(delta.y) > 0)
                    {
                        if (ZoomScale < 1)
                            ScrollOffset *= ZoomScale;
                        else
                            ScrollOffset = Vector4.zero;
                    }
                }
                // テクスチャの表示箇所を移動する機能
                else if (Event.current.button == 1 &&
                    mouseEventType == EventType.MouseDrag)
                {
                    if (delta.x != 0)
                    {
                        _scrollOffset.x -= delta.x / rect.width;

                        if (_scrollOffset.x > 1 - ZoomScale)
                            _scrollOffset.x = 1 - ZoomScale;
                        else if (_scrollOffset.x < -(1 - ZoomScale))
                            _scrollOffset.x = -(1 - ZoomScale);
                    }

                    if (delta.y != 0)
                    {
                        _scrollOffset.y += delta.y / rect.height;

                        if (_scrollOffset.y > 1 - ZoomScale)
                            _scrollOffset.y = 1 - ZoomScale;
                        else if (_scrollOffset.y < -(1 - ZoomScale))
                            _scrollOffset.y = -(1 - ZoomScale);
                    }

                    editMat.SetVector("_Offset", _scrollOffset);
                }


                var pos = ConvertWindowPosToTexturePos(textureSize, Event.current.mousePosition, rect, ZoomScale, ScrollOffset);

                if (DrawType == DrawType.PEN || DrawType == DrawType.ERASER)
                {
                    var uvPos = ConvertTexturePosToUVPos(textureSize, pos);
                    editMat.SetVector("_CurrentPos", new Vector4(uvPos.x, uvPos.y, 0, 0));

                    if (Event.current.type == EventType.MouseDown &&
                        Event.current.button == 0 &&
                        !isDrawing)
                    {
                        undo.RegisterUndoTexture(previewTexture, canvasModel.buffer);
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
                        if (DrawType == DrawType.PEN)
                            DrawOnTexture(pos);
                        else
                            ClearOnTexture(pos);
                    }
                }
            }
        }

        /// <summary>
        /// DrawAreaを初期化
        /// </summary>
        /// <param name="index"></param>
        /// <param name="mesh"></param>
        public void InitializeDrawArea(MaterialInfo materialInfo, Renderer renderer)
        {
            if (materialInfo.Texture != null)
            {
                editTexture = TextureUtility.GenerateTextureToEditting(materialInfo.Texture);

                ClearAllDrawing();

                var mesh = RendererUtility.GetMesh(renderer);
                if (mesh != null)
                {
                    var uvMapTex = uvMap.GenerateUVMap(mesh, materialInfo, materialInfo.Texture);
                    uvMap.SetUVMapTexture(uvMapTex);
                }

                // TODO: _MainTexが存在しないマテリアルは違うやつに入れないといけない
                var materials = renderer.sharedMaterials;
                previewMaterial = new Material(materials[materialInfo.MaterialSlotIndices[0]])
                {
                    name = "_preview",
                    mainTexture = previewTexture,
                };
                materials[materialInfo.MaterialSlotIndices[0]] = previewMaterial;
                renderer.sharedMaterials = materials;
            }
            ResetScrollOffsetAndZoomScale();
        }

        /// <summary>
        /// 描画エリアをリセットする
        /// </summary>
        public void ClearAllDrawing()
        {
            previewTexture = TextureUtility.CopyTexture2DToRenderTexture(materialInfo.Texture, textureSize, PlayerSettings.colorSpace == ColorSpace.Linear);
            canvasModel.Initialize(ref editTexture, ref previewTexture);
            deleteMask = new DeleteMaskCanvas(ref canvasModel.buffer, materialInfo.Texture, ref previewTexture);
        }

        /// <summary>
        /// ペン
        /// </summary>
        /// <param name="pos"></param>
        private void DrawOnTexture(Vector2 pos) => canvasModel.Mark(pos);

        /// <summary>
        /// 消しゴム
        /// </summary>
        /// <param name="pos"></param>
        private void ClearOnTexture(Vector2 pos) => canvasModel.UnMark(pos);

        /// <summary>
        /// ScrollOffsetとZoomScaleをリセットする
        /// </summary>
        public void ResetScrollOffsetAndZoomScale()
        {
            ScrollOffset = Vector4.zero;
            ZoomScale = 1;
        }

        private Vector2 ConvertWindowPosToTexturePos(Vector2Int textureSize, Vector2 windowPos, Rect rect, float zoomScale, Vector4 scrollOffset)
        {
            float raito = textureSize.x / rect.width;

            // Textureの場所に変換
            var texX = (int)((windowPos.x - rect.position.x) * raito);
            var texY = textureSize.y - (int)((windowPos.y - rect.position.y) * raito);

            // ScaleとOffsetによって変化しているので戻す
            var x = texX / 2 * (1 + zoomScale + scrollOffset.x);
            var y = texY / 2 * (1 + zoomScale + scrollOffset.y);

            return new Vector2(x, y);
        }

        private Vector2 ConvertTexturePosToUVPos(Vector2Int textureSize, Vector2 texturePos) => texturePos / textureSize;

        /// <summary>
        /// 塗られている範囲を反転させる
        /// </summary>
        public void InverseFillArea()
        {
            var height = textureSize.y;
            var width = textureSize.x;
            var maskTexture = new Texture2D(width, height);

            var deletePos = new int[width * height];
            canvasModel.buffer.GetData(deletePos);
            deletePos = deletePos.Select(x => Mathf.Abs(x - 1)).ToArray();
            canvasModel.buffer.SetData(deletePos);

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
            Graphics.Blit(materialInfo.Texture, previewTexture, negaposiMat);
        }

        /// <summary>
        /// 削除する場所のデータを取得
        /// </summary>
        /// <returns>削除する場所</returns>
        public int[] GetDeleteData()
        {
            var deletePos = new int[textureSize.x * textureSize.y];
            canvasModel.buffer.GetData(deletePos);
            return deletePos;
        }

        public void Dispose()
        {
            canvasModel.Dispose();
        }
    }
}