using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

namespace Gatosyocora
{
    public static class GatoGUILayout
    {
        #region MiniMonitor Variable
        public static Material gammaMat = Resources.Load<Material>("TextureEditMat");
        #endregion

        public static Vector2 MiniMonitor(Texture texture, float width, float height, ref Rect rect, ref EventType type, bool isGammaCorrection)
        {
            rect = GUILayoutUtility.GetRect(width, height, GUI.skin.box);

            Graphics.DrawTexture(rect, texture, (isGammaCorrection) ? gammaMat : null);

            var e = Event.current;

            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag)
                {
                    type = e.type;
                    return e.delta;
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    type = e.type;
                    return e.delta;
                }
            }

            return Vector2.zero;
        }

        public static string DragAndDropableArea(string text, float width, float height, string permissonExtension)
        {
            var rect = GUILayoutUtility.GetRect(width, height);
            GUI.Label(rect, text, GUI.skin.box);
            var e = Event.current;
            if ((e.type == EventType.DragPerform || e.type == EventType.DragUpdated) &&
                rect.Contains(e.mousePosition))
            {
                if (Path.GetExtension(DragAndDrop.paths.FirstOrDefault()) == permissonExtension)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
            else if (e.type == EventType.DragExited && rect.Contains(e.mousePosition))
            {
                var path = DragAndDrop.paths.FirstOrDefault();
                if (Path.GetExtension(path) != permissonExtension)
                    return string.Empty;

                DragAndDrop.AcceptDrag();
                GUI.changed = true;
                return path;
            }

            return string.Empty;
        }
    }
}