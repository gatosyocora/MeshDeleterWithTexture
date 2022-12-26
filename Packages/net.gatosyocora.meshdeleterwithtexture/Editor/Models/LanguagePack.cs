using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    [CreateAssetMenu(menuName = "MeshDeleterWithTexture/LanguagePack")]
    public class LanguagePack : ScriptableObject
    {
        public Language language = Language.EN;

        public string rendererLabelText = "Renderer";

        public string scaleLabelText = "Scale";
        public string resetButtonText = "Reset";

        public string importDeleteMaskButtonText = "Import DeleteMask";
        public string exportDeleteMaskButtonText = "Export DeleteMask";
        public string dragAndDropDeleteMaskTextureAreaText = "Drag & Drop DeleteMaskTexture";

        public string uvMapLineColorLabelText = "UVMap LineColor";
        public string exportUvMapButtonText = "Export UVMap";

        public string textureLabelText = "Texture (Material)";

        public string toolsTitleText = "Tools";
        public string drawTypeLabelText = "DrawType";
        public string penToolNameText = "PEN";
        public string eraserToolNameText = "ERASER";
        public string selectToolNameText = "SELECT";
        public string penColorLabelText = "PenColor";
        public string colorBlackButtonText = "Black";
        public string colorRedButtonText = "R";
        public string colorGreenButtonText = "G";
        public string colorBlueButtonText = "B";
        public string penEraserSizeLabelText = "Pen/Eraser size";
        public string inverseSelectAreaButtonText = "Inverse SelectArea";
        public string applySelectAreaButtonText = "Apply SelectArea";
        public string inverseFillAreaButtonText = "Inverse FillArea";
        public string clearAllDrawingButtonText = "Clear All Drawing";
        public string undoDrawingButtonText = "Undo Drawing";

        public string modelInformationTitleText = "Model Information";
        public string triangleCountLabelText = "Triangle Count";

        public string outputMeshTitleText = "Output Mesh";
        public string saveFolderLabelText = "SaveFolder";
        public string selectFolderButtonText = "Select Folder";
        public string outputFileNameLabelText = "Name";

        public string revertMeshToPrefabButtonText = "Revert Mesh to Prefab";
        public string revertMeshToPreviouslyButtonText = "Revert Mesh to previously";

        public string deleteMeshButtonText = "Delete Mesh";

        public string androidNotSupportMessageText = "Can't use with BuildTarget 'Android'.\nPlease switch BuildTarget to PC";

        public string errorDialogTitleText = "Error has occurred";
        public string notFoundVerticesExceptionDialogMessageText = "Not found vertices to delete.";
        public string errorDialogOkText = "OK";

        public string helpButtonText = "Help";
    }
}
