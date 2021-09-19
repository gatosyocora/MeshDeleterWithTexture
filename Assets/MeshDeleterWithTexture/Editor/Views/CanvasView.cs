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

        private const float MAX_ZOOM_SCALE = 1;
        private const float MIN_ZOOM_SCALE = 0.1f;
        private const float ZOOM_STEP = 0.1f;

        private const int LEFT_BUTTON = 0;
        private const int RIGHT_BUTTON = 1;

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

        private Vector2 _scrollOffset;
        public Vector2 ScrollOffset
        {
            get => _scrollOffset;
            private set
            {
                editMat.SetVector("_Offset", new Vector4(value.x, value.y, 0, 0));
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
        public SelectAreaCanvas selectArea;

        public void OnEnable()
        {
            editMat = AssetRepository.LoadTextureEditMaterial();
            canvasModel = CreateInstance<CanvasModel>();
            undo = new UndoCanvas();
            uvMap = new UVMapCanvas(ref editMat);
            selectArea = new SelectAreaCanvas(ref editMat);

            DrawType = DrawType.PEN;
            PenColor = Color.black;
        }

        public void Initialize(MaterialInfo materialInfo, Renderer renderer)
        {
            editMat.SetFloat("_ApplyGammaCorrection", Convert.ToInt32(PlayerSettings.colorSpace == ColorSpace.Linear));
            editMat.SetInt("_PointNum", 0);

            editMat.SetVector("_StartPos", new Vector4(0, 0, 0, 0));
            editMat.SetVector("_EndPos", new Vector4(0, 0, 0, 0));

            editMat.SetTexture("_SelectTex", null);

            InitializeDrawArea(materialInfo, renderer);

            PenSize = 20;
        }

        public void Render(bool hasTexture, float canvasSizeRaito)
        {
            if (!hasTexture)
            {
                DrawDummyCanvasView(canvasSizeRaito);
                return;
            }

            if (textureSize == null) return;

            var width = EditorGUIUtility.currentViewWidth * canvasSizeRaito;
            var height = width * textureSize.y / textureSize.x;
            EventType mouseEventType = 0;
            Rect rect = new Rect(0, 0, 0, 0);
            var delta = GatoGUILayout.MiniMonitor(previewTexture, width, height, ref rect, ref mouseEventType, editMat);

            if (rect.Contains(Event.current.mousePosition))
            {
                // テクスチャの拡大縮小機能
                if (mouseEventType == EventType.ScrollWheel)
                {
                    var (off, scale) = UpdateByZoomScale(ScrollOffset, ZoomScale, delta);
                    ScrollOffset = off;
                    ZoomScale = scale;
                }
                // テクスチャの表示箇所を移動する機能
                else if (Event.current.button == RIGHT_BUTTON &&
                    mouseEventType == EventType.MouseDrag)
                {
                    ScrollOffset = UpdateScrollOffset(ScrollOffset, delta, rect.size, ZoomScale);
                }


                var pos = ConvertWindowPosToTexturePos(textureSize, Event.current.mousePosition, rect, ZoomScale, ScrollOffset);

                if (DrawType == DrawType.PEN || DrawType == DrawType.ERASER || DrawType == DrawType.SELECT)
                {
                    var uvPos = ConvertTexturePosToUVPos(textureSize, pos);
                    editMat.SetVector("_CurrentPos", new Vector4(uvPos.x, uvPos.y, 0, 0));

                    if (Event.current.type == EventType.MouseDown &&
                        Event.current.button == LEFT_BUTTON &&
                        !isDrawing)
                    {
                        RegisterUndoTexture();
                        isDrawing = true;
                    }
                    else if (Event.current.type == EventType.MouseUp &&
                        Event.current.button == LEFT_BUTTON &&
                        isDrawing)
                    {
                        isDrawing = false;
                    }

                    if (isDrawing)
                    {
                        if (DrawType == DrawType.PEN)
                            DrawOnTexture(pos);
                        else if (DrawType == DrawType.ERASER)
                            ClearOnTexture(pos);
                        else
                            selectArea.AddSelectAreaPoint(pos);
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
            this.materialInfo = materialInfo;

            if (materialInfo.Texture != null)
            {
                editTexture = TextureUtility.GenerateTextureToEditting(materialInfo.Texture);
                textureSize = new Vector2Int(materialInfo.Texture.width, materialInfo.Texture.height);

                ClearAllDrawing(materialInfo);

                uvMap.SetUVMapTexture(renderer, materialInfo);

                selectArea.SetSelectAreaTexture(renderer, materialInfo);

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

        public void InitializeDrawArea()
        {
            this.materialInfo = null;
            editTexture = null;
            textureSize = Vector2Int.zero;
            uvMap.SetUVMapTexture(null, null);
            ResetScrollOffsetAndZoomScale();
        }

        /// <summary>
        /// 描画エリアをリセットする
        /// </summary>
        public void ClearAllDrawing(MaterialInfo materialInfo)
        {
            previewTexture = TextureUtility.CopyTexture2DToRenderTexture(materialInfo.Texture, textureSize, PlayerSettings.colorSpace == ColorSpace.Linear);
            canvasModel.Initialize(ref editTexture, ref previewTexture);
            deleteMask = new DeleteMaskCanvas(ref canvasModel.buffer, materialInfo.Texture, ref previewTexture);
        }

        public void ClearAllDrawing()
        {
            ClearAllDrawing(materialInfo);
        }

        /// <summary>
        /// ペン
        /// </summary>
        /// <param name="pos"></param>
        private void DrawOnTexture(Vector2Int pos) => canvasModel.Mark(pos);

        /// <summary>
        /// 消しゴム
        /// </summary>
        /// <param name="pos"></param>
        private void ClearOnTexture(Vector2Int pos) => canvasModel.UnMark(pos);

        /// <summary>
        /// ScrollOffsetとZoomScaleをリセットする
        /// </summary>
        public void ResetScrollOffsetAndZoomScale()
        {
            ScrollOffset = Vector2.zero;
            ZoomScale = 1;
        }

        // Textureのuv座標的にどの範囲が表示されているかを元に補正している
        private Vector2Int ConvertWindowPosToTexturePos(Vector2Int textureSize, Vector2 windowPos, Rect rect, float zoomScale, Vector2 scrollOffset)
        {
            var invZoomScale = 1 - zoomScale;

            float raito = textureSize.x / rect.width;

            // 正規化されたCanvasのポジションに変換
            var normalizedCanvasPosX = ((windowPos.x - rect.position.x) * raito) / textureSize.x;
            var normalizedCanvasPosY = (textureSize.y - (windowPos.y - rect.position.y) * raito) / textureSize.y;

            // ScrollOffsetを[-1, 1]の範囲にしたもの(中心からどれぐらいずれているか)
            var normalizedOffset = zoomScale < 1 ? new Vector2(
                Mathf.InverseLerp(-(invZoomScale), invZoomScale, scrollOffset.x) * 2f - 1f,
                Mathf.InverseLerp(-(invZoomScale), invZoomScale, scrollOffset.y) * 2f - 1f 
            ) : Vector2.zero;

            // テクスチャのuv座標的な最小値と最大値
            // zoomScaleが0.5でoffsetが0,0ならuv座標的に[0.25, 0.75]の範囲が表示されている
            // zoomScale = uv座標の表示範囲幅
            // offsetXがマイナス（左の方を表示している）のとき、左の未表示範囲 < 右の未表示範囲
            // offsetXがプラス（右の方を表示している）のとき、左の未表示範囲 > 右の未表示範囲
            var minCanvasPosX = 0.5f - zoomScale / 2f + normalizedOffset.x * (invZoomScale / 2f);
            var maxCanvasPosX = 0.5f + zoomScale / 2f + normalizedOffset.x * (invZoomScale / 2f);
            var minCanvasPosY = 0.5f - zoomScale / 2f + normalizedOffset.y * (invZoomScale / 2f);
            var maxCanvasPosY = 0.5f + zoomScale / 2f + normalizedOffset.y * (invZoomScale / 2f);

            // ScaleとOffsetによって変化しているので戻す
            var x = (int)(Mathf.Lerp(minCanvasPosX, maxCanvasPosX, normalizedCanvasPosX) * textureSize.x);
            var y = (int)(Mathf.Lerp(minCanvasPosY, maxCanvasPosY, normalizedCanvasPosY) * textureSize.y);

            return new Vector2Int(x, y);
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
        public bool[] GetDeleteData()
        {
            var deletePos = new int[textureSize.x * textureSize.y];
            canvasModel.buffer.GetData(deletePos);
            return deletePos.Select(v => v == 1).ToArray();
        }

        public void RegisterUndoTexture() => undo.RegisterUndoTexture(previewTexture, canvasModel.buffer);

        public void UndoPreviewTexture() => undo.UndoPreviewTexture(ref previewTexture, ref canvasModel.buffer);

        public void Dispose()
        {
            canvasModel.Dispose();
        }

        private static (Vector2, float) UpdateByZoomScale(Vector2 scrollOffset, float zoomScale, Vector2 delta)
        {
            zoomScale = Mathf.Clamp(
                zoomScale + Mathf.Sign(delta.y) * ZOOM_STEP,
                MIN_ZOOM_SCALE,
                MAX_ZOOM_SCALE
            );

            // 縮小ではOffsetも中心に戻していく
            if (Mathf.Sign(delta.y) > 0)
            {
                if (zoomScale < MAX_ZOOM_SCALE)
                    scrollOffset *= zoomScale;
                else
                    scrollOffset = Vector2.zero;
            }

            return (scrollOffset, zoomScale);
        }

        private static Vector2 UpdateScrollOffset(Vector2 scrollOffset, Vector2 delta, Vector2 rectSize, float zoomScale)
        {
            var inverseZoomScale = 1 - zoomScale;

            if (delta.x != 0)
            {
                scrollOffset.x = Mathf.Clamp(
                    scrollOffset.x - delta.x / rectSize.x,
                    -inverseZoomScale,
                    inverseZoomScale
                );
            }

            if (delta.y != 0)
            {
                scrollOffset.y = Mathf.Clamp(
                    scrollOffset.y + delta.y / rectSize.y,
                    -inverseZoomScale,
                    inverseZoomScale
                );
            }

            return scrollOffset;
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
    }
}