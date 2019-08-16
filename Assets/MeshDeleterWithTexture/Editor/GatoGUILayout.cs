using UnityEngine;

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
    }
}