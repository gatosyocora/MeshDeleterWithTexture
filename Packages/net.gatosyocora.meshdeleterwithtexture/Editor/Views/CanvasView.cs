using Gatosyocora.MeshDeleterWithTexture.Models;
using Gatosyocora.MeshDeleterWithTexture.Utilities;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Views
{
    public class CanvasView : Editor, IDisposable
    {
        private const float MAX_ZOOM_SCALE = 1;
        private const float MIN_ZOOM_SCALE = 0.1f;
        private const float ZOOM_STEP = 0.1f;

        private const int LEFT_BUTTON = 0;
        private const int RIGHT_BUTTON = 1;

        private const int PADDING_SIZE = 6;

        private const string PREVIEW_MATERIAL_NAME = "_preview";

        private const string MAT_VARIABLE_APPLY_GAMMA_CORRECTION = "_ApplyGammaCorrection";
        private const string MAT_VARIABLE_POINT_NUM = "_PointNum";
        private const string MAT_VARIABLE_START_POS = "_StartPos";
        private const string MAT_VARIABLE_END_POS = "_EndPos";
        private const string MAT_VARIABLE_CURRENT_POS = "_CurrentPos";
        private const string MAT_VARIABLE_MAIN_TEX_SIZE = "_MainTex_Size";
        private const string MAT_VARIABLE_SELECT_AREA_PATTERN_TEX = "_SelectAreaPatternTex";
        private const string MAT_VARIABLE_SELECT_AREA_PATTERN_TEX_SIZE = "_SelectAreaPatternTex_Size";
        private const string MAT_VARIABLE_IS_ERASER = "_IsEraser";
        private const string MAT_VARIABLE_IS_STRAIGHT_MODE = "_IsStraightMode";
        private const string MAT_VARIABLE_PEN_SIZE = "_PenSize";
        private const string MAT_VARIABLE_OFFSET = "_Offset";
        private const string MAT_VARIABLE_TEXTURE_SCALE = "_TextureScale";

        private CanvasModel canvasModel;

        private Material editMat;
        private Texture2D editTexture;
        private Material previewMaterial;

        private bool isDrawing = false;
        private Vector2Int textureSize;

        private MaterialInfo materialInfo;

        private Vector2Int startPos;
        private bool isDrawingStraight = false;
        private StraightType straightType = StraightType.NONE;

        public RenderTexture previewTexture;

        private DrawType _drawType;
        public DrawType DrawType
        {
            get => _drawType;
            set
            {
                _drawType = value;
                OnDrawTypeChanged(value);
            }
        }

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
                UpdateCursorSize(value, textureSize);
                canvasModel.SetPen(value, _penColor);
                selectArea.ApplyPenSize(value);
            }
        }

        private Vector2 _scrollOffset;
        public Vector2 ScrollOffset
        {
            get => _scrollOffset;
            private set
            {
                editMat.SetVector(MAT_VARIABLE_OFFSET, new Vector4(value.x, value.y, 0, 0));
                _scrollOffset = value;
            }
        }

        private float _zoomScale;
        public float ZoomScale 
        {
            get => _zoomScale;
            set {
                editMat.SetFloat(MAT_VARIABLE_TEXTURE_SCALE, value);
                _zoomScale = value;
            }
        }

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
            editMat.SetFloat(MAT_VARIABLE_APPLY_GAMMA_CORRECTION, Convert.ToInt32(PlayerSettings.colorSpace == ColorSpace.Linear));
            editMat.SetInt(MAT_VARIABLE_POINT_NUM, 0);

            editMat.SetVector(MAT_VARIABLE_START_POS, new Vector4(0, 0, 0, 0));
            editMat.SetVector(MAT_VARIABLE_END_POS, new Vector4(0, 0, 0, 0));

            var patternTexture = AssetRepository.LoadSelectTextureAreaPatternTexture();
            editMat.SetTexture(MAT_VARIABLE_SELECT_AREA_PATTERN_TEX, patternTexture);
            editMat.SetFloat(MAT_VARIABLE_SELECT_AREA_PATTERN_TEX_SIZE, patternTexture.width);

            editMat.SetInt(MAT_VARIABLE_IS_ERASER, DrawType == DrawType.ERASER ? 1 : 0);

            editMat.SetInt(MAT_VARIABLE_IS_STRAIGHT_MODE, 0);
            startPos = Vector2Int.one * -1;

            InitializeDrawArea(materialInfo, renderer);

            PenSize = 20;
        }

        public void Render(bool hasTexture, float canvasSizeRaito)
        {
            var width = EditorGUIUtility.currentViewWidth * canvasSizeRaito - PADDING_SIZE;

            if (!hasTexture)
            {
                DrawDummyCanvasView(width);
                return;
            }

            if (textureSize == null) return;

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
                    pos = ApplyStraightModeIfNeeded(pos);

                    var uvPos = ConvertTexturePosToUVPos(textureSize, pos);
                    editMat.SetVector(MAT_VARIABLE_CURRENT_POS, new Vector4(uvPos.x, uvPos.y, 0, 0));

                    if (InputMouseLeftDown() && !isDrawing)
                    {
                        OnStartDrawing();
                    }
                    else if (InputMouseLeftUp() && isDrawing)
                    {
                        OnFinishDrawing();
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
                if (editTexture != null)
                {
                    DestroyImmediate(editTexture);
                }
                editTexture = TextureUtility.GenerateTextureToEditting(materialInfo.Texture);
                textureSize = new Vector2Int(materialInfo.Texture.width, materialInfo.Texture.height);

                editMat.SetVector(MAT_VARIABLE_MAIN_TEX_SIZE, new Vector4(textureSize.x, textureSize.y, 0, 0));

                ClearAllDrawing(materialInfo);

                uvMap.SetUVMapTexture(renderer, materialInfo);

                UpdateCursorSize(PenSize, textureSize);

                selectArea.SetSelectAreaTexture(renderer, materialInfo);
                selectArea.ApplyPenSize(PenSize);

                // TODO: _MainTexが存在しないマテリアルは違うやつに入れないといけない
                var materials = renderer.sharedMaterials;
                if (previewMaterial != null)
                {
                    DestroyImmediate(previewMaterial);
                }
                previewMaterial = new Material(materials[materialInfo.MaterialSlotIndices[0]])
                {
                    name = PREVIEW_MATERIAL_NAME,
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
            if (previewTexture != null)
            {
                previewTexture.Release();
            }

            previewTexture = TextureUtility.CopyTexture2DToRenderTexture(materialInfo.Texture, textureSize, PlayerSettings.colorSpace == ColorSpace.Linear);
            canvasModel.Initialize(ref editTexture, ref previewTexture);
            deleteMask = new DeleteMaskCanvas(ref canvasModel.buffer, materialInfo.Texture, ref previewTexture);
        }

        public void ClearAllDrawing()
        {
            ClearAllDrawing(materialInfo);
        }

        /// <summary>
        /// ScrollOffsetとZoomScaleをリセットする
        /// </summary>
        public void ResetScrollOffsetAndZoomScale()
        {
            ScrollOffset = Vector2.zero;
            ZoomScale = 1;
        }

        public void InverseFillArea() => canvasModel.InverseCanvasMarks();

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

        public void ApplySelectArea()
        {
            var selectAreaData = selectArea.GetFillArea();
            canvasModel.MarkArea(selectAreaData);
        }

        public void Dispose()
        {
            canvasModel.Dispose();
        }

        private void OnStartDrawing()
        {
            RegisterUndoTexture();
            isDrawing = true;

            if (DrawType == DrawType.SELECT)
            {
                selectArea.ClearSelectArea();
            }
        }

        private void OnFinishDrawing()
        {
            isDrawing = false;

            if (DrawType == DrawType.SELECT)
            {
                selectArea.AddLineEnd2Start();
                selectArea.FillSelectArea();
            }
            else
            {
                canvasModel.ResetLatestPos();
            }
        }

        private void OnDrawTypeChanged(DrawType drawType)
        {
            canvasModel.ResetLatestPos();

            editMat.SetInt(MAT_VARIABLE_IS_ERASER, drawType == DrawType.ERASER ? 1 : 0);

            switch (drawType)
            {
                case DrawType.PEN:
                case DrawType.ERASER:
                    selectArea.ClearSelectArea();
                    break;
                case DrawType.SELECT:
                    selectArea.ApplyPenSize(PenSize);
                    break;
                default:
                    break;
            }
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

        private Vector2Int ApplyStraightModeIfNeeded(Vector2Int pos)
        {
            if (InputKeyDownShift() && !isDrawingStraight)
            {
                isDrawingStraight = true;
                editMat.SetInt(MAT_VARIABLE_IS_STRAIGHT_MODE, 1);
            }
            else if (InputKeyUpShift() && isDrawingStraight)
            {
                isDrawingStraight = false;
                editMat.SetInt(MAT_VARIABLE_IS_STRAIGHT_MODE, 0);
            }

            if (isDrawing && isDrawingStraight)
            {
                if (startPos.x == -1) startPos = pos;

                var diffX = Mathf.Abs(pos.x - startPos.x);
                var diffY = Mathf.Abs(pos.y - startPos.y);

                if (straightType == StraightType.HORIZONTAL || diffX > diffY)
                {
                    pos.y = startPos.y;
                    straightType = StraightType.HORIZONTAL;
                }
                else if (straightType == StraightType.VERTICAL || diffX < diffY)
                {
                    pos.x = startPos.x;
                    straightType = StraightType.VERTICAL;
                }
            } 
            else
            {
                startPos = Vector2Int.one * -1;
                straightType = StraightType.NONE;
            }

            return pos;
        }

        private void UpdateCursorSize(int penSize, Vector2Int textureSize)
        {
            editMat.SetFloat(MAT_VARIABLE_PEN_SIZE, penSize / (float)textureSize.x);
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

        private void DrawDummyCanvasView(float width)
        {
            GUI.Box(GUILayoutUtility.GetRect(width, width, GUI.skin.box), string.Empty);
        }

        private bool InputMouseLeftDown() => Event.current.type == EventType.MouseDown && Event.current.button == LEFT_BUTTON;
        private bool InputMouseLeftUp() => Event.current.type == EventType.MouseUp && Event.current.button == LEFT_BUTTON;

        private bool InputKeyDownShift() => Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.LeftShift || Event.current.keyCode == KeyCode.RightShift);
        private bool InputKeyUpShift() => Event.current.type == EventType.KeyUp && (Event.current.keyCode == KeyCode.LeftShift || Event.current.keyCode == KeyCode.RightShift);
    }

    public enum StraightType
    {
        NONE, HORIZONTAL, VERTICAL
    }
}