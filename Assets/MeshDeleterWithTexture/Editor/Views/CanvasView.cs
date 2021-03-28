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

        private bool isDrawing = false;
        private Vector2Int textureSize;

        private MaterialInfo materialInfo;

        public DrawType drawType { get; private set; }

        public Color penColor { get; private set; } = Color.black;
        public int penSize { get; private set; } = 20;

        private Vector4 textureOffset;
        public float zoomScale { get; private set; }

        private CanvasModel canvasModel;

        public UndoCanvas undo;
        public UVMapCanvas uvMap;

        public void OnEnable()
        {
            editMat = Resources.Load<Material>("TextureEditMat");
        }

        public CanvasView()
        {
            undo = new UndoCanvas();
            uvMap = new UVMapCanvas(ref editMat);
            canvasModel = new CanvasModel(undo, uvMap);

            drawType = DrawType.PEN;
        }

        public void Initialize(MaterialInfo materialInfo)
        {
            this.materialInfo = materialInfo;
            textureSize = new Vector2Int(materialInfo.Texture.width, materialInfo.Texture.height);
            textureOffset = Vector4.zero;
            zoomScale = 1;

            editMat.SetFloat("_ApplyGammaCorrection", Convert.ToInt32(PlayerSettings.colorSpace == ColorSpace.Linear));
            editMat.SetInt("_PointNum", 0);

            canvasModel.InitComputeShader();
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

                    editMat.SetFloat("_TextureScale", zoomScale);
                }
                // テクスチャの表示箇所を移動する機能
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
                }


                var pos = ConvertWindowPosToTexturePos(textureSize, Event.current.mousePosition, rect, zoomScale, textureOffset);

                if (drawType == DrawType.PEN || drawType == DrawType.ERASER)
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
                        if (drawType == DrawType.PEN)
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
        public void InitializeDrawingArea(MaterialInfo materialInfo, Renderer renderer)
        {
            if (materialInfo.Texture != null)
            {
                editTexture = GenerateTextureToEditting(materialInfo.Texture);

                DrawTypeSetting(drawType);
                ResetDrawArea();
                canvasModel.SetupComputeShader(ref editTexture, ref previewTexture);

                var mesh = RendererUtility.GetMesh(renderer);
                if (mesh != null)
                {
                    var uvMapTex = uvMap.GenerateUVMap(mesh, materialInfo, materialInfo.Texture);
                    uvMap.SetUVMapTexture(uvMapTex);
                }

                // TODO: _MainTexが存在しないマテリアルは違うやつに入れないといけない
                renderer.sharedMaterials[materialInfo.MaterialSlotIndices[0]].mainTexture = previewTexture;
            }
            ResetDrawAreaOffsetAndZoom();
        }

        public void DrawTypeSetting(DrawType drawType)
        {
            this.drawType = drawType;
            if (drawType == DrawType.PEN)
            {
                SetupDrawing(penSize, penColor, texture);
            }
        }

        public void ResetDrawArea()
        {
            previewTexture = CopyTexture2DToRenderTexture(materialInfo.Texture, textureSize, PlayerSettings.colorSpace == ColorSpace.Linear);
            canvasModel.SetupComputeShader(ref editTexture, ref previewTexture);

            editMat.SetVector("_StartPos", new Vector4(0, 0, 0, 0));
            editMat.SetVector("_EndPos", new Vector4(textureSize.x - 1, textureSize.y - 1, 0, 0));

            editMat.SetTexture("_SelectTex", null);
        }

        private RenderTexture CopyTexture2DToRenderTexture(Texture2D texture, Vector2Int textureSize, bool isLinearColorSpace = false)
        {
            RenderTexture renderTexture;

            if (isLinearColorSpace)
                renderTexture = new RenderTexture(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            else
                renderTexture = new RenderTexture(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32);

            renderTexture.enableRandomWrite = true;
            renderTexture.anisoLevel = texture.anisoLevel;
            renderTexture.mipMapBias = texture.mipMapBias;
            renderTexture.filterMode = texture.filterMode;
            renderTexture.wrapMode = texture.wrapMode;
            renderTexture.wrapModeU = texture.wrapModeU;
            renderTexture.wrapModeV = texture.wrapModeV;
            renderTexture.wrapModeW = texture.wrapModeW;
            renderTexture.Create();

            Graphics.Blit(texture, renderTexture);
            return renderTexture;
        }


        /// <summary>
        /// 読み込んだ後の設定をおこなう
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        private Texture2D GenerateTextureToEditting(Texture2D originTexture)
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

        private void SetupDrawing(int penSize, Color penColor, Texture2D texture)
        {
            canvasModel.SetPen(penSize, penColor);
            editMat.SetFloat("_PenSize", penSize / (float)texture.width);
        }

        /// <summary>
        /// ペン
        /// </summary>
        /// <param name="pos"></param>
        private void DrawOnTexture(Vector2 pos)
        {
            canvasModel.Draw(pos, texture);
        }

        /// <summary>
        /// 消しゴム
        /// </summary>
        /// <param name="pos"></param>
        private void ClearOnTexture(Vector2 pos)
        {
            canvasModel.Clear(pos, texture);
        }

        public void SetPenColor(Color penColor)
        {
            this.penColor = penColor;
            SetupDrawing(penSize, penColor, texture);
        }

        public void SetPenSize(int penSize)
        {
            this.penSize = penSize;
            SetupDrawing(penSize, penColor, texture);
        }

        public void ApplyTextureZoomScale(float scale)
        {
            zoomScale = scale;
            editMat.SetFloat("_TextureScale", scale);
        }

        public void ResetDrawAreaOffsetAndZoom()
        {
            textureOffset = Vector4.zero;
            editMat.SetVector("_Offset", textureOffset);
            zoomScale = 1;
            ApplyTextureZoomScale(zoomScale);
        }

        /// <summary>
        /// マスク画像を読み込む
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="deletePos"></param>
        /// <returns></returns>
        public bool ImportDeleteMaskTexture()
        {
            // 画像ファイルを取得(png, jpg)
            var path = EditorUtility.OpenFilePanelWithFilters("Select delete mask texture", "Assets", new string[] { "Image files", "png,jpg,jpeg" });

            if (string.IsNullOrEmpty(path)) return false;

            return ApplyDeleteMaskTexturetToBuffer(path);
        }

        /// <summary>
        /// マスク画像を書き出す
        /// </summary>
        /// <param name="deletePos"></param>
        public void ExportDeleteMaskTexture()
        {
            var height = textureSize.y;
            var width = textureSize.x;
            var maskTexture = new Texture2D(width, height);

            var deletePos = new int[width * height];
            canvasModel.buffer.GetData(deletePos);

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
                        materialInfo.Texture.name + ".png",
                        "png");

            if (path.Length > 0)
                File.WriteAllBytes(path, png);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public bool ApplyDeleteMaskTexturetToBuffer(string maskTexturePath)
        {
            var fileStream = new FileStream(maskTexturePath, FileMode.Open, FileAccess.Read);
            var bin = new BinaryReader(fileStream);
            var binaryData = bin.ReadBytes((int)bin.BaseStream.Length);
            bin.Close();

            var maskTexture = new Texture2D(0, 0);
            maskTexture.LoadImage(binaryData);

            if (maskTexture == null || textureSize.x != maskTexture.width || textureSize.y != maskTexture.height) return false;

            var deletePos = new int[maskTexture.width * maskTexture.height];
            canvasModel.buffer.GetData(deletePos);

            for (int j = 0; j < maskTexture.height; j++)
            {
                for (int i = 0; i < maskTexture.width; i++)
                {
                    var col = maskTexture.GetPixel(i, j);
                    var isDelete = (col == UnityEngine.Color.black) ? 1 : 0;
                    deletePos[j * maskTexture.width + i] = isDelete;
                }
            }

            canvasModel.buffer.SetData(deletePos);

            Material negaposiMat = new Material(Shader.Find("Gato/NegaPosi"));
            negaposiMat.SetTexture("_MaskTex", maskTexture);
            negaposiMat.SetFloat("_Inverse", 0);
            Graphics.Blit(materialInfo.Texture, previewTexture, negaposiMat);

            return true;
        }

        private Vector2 ConvertWindowPosToTexturePos(Vector2Int textureSize, Vector2 windowPos, Rect rect, float zoomScale, Vector4 textureOffset)
        {
            float raito = textureSize.x / rect.width;

            var texX = (int)((windowPos.x - rect.position.x) * raito);
            var texY = textureSize.y - (int)((windowPos.y - rect.position.y) * raito);

            return ScaleOffset(textureSize, new Vector2(texX, texY), zoomScale, textureOffset);
        }

        private Vector2 ScaleOffset(Vector2Int textureSize, Vector2 pos, float zoomScale, Vector4 textureOffset)
        {
            var x = (textureSize.x / 2 * (1 - zoomScale) + textureOffset.x * textureSize.x / 2) + pos.x * zoomScale;
            var y = (textureSize.y / 2 * (1 - zoomScale) + textureOffset.y * textureSize.y / 2) + pos.y * zoomScale;
            return new Vector2(x, y);
        }

        private Vector2 ConvertTexturePosToUVPos(Vector2Int textureSize, Vector2 texturePos)
        {
            return new Vector2(texturePos.x / (float)textureSize.x, texturePos.y / (float)textureSize.y);
        }

        public void InverseSiroKuro()
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

        public int[] GetDeleteData(int width, int height)
        {
            var deletePos = new int[width * height];
            canvasModel.buffer.GetData(deletePos);
            return deletePos;
        }

        public void Dispose()
        {
            canvasModel.Dispose();
        }
    }
}