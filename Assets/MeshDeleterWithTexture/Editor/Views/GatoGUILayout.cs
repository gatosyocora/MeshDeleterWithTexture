using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using Gatosyocora.MeshDeleterWithTexture.Models;
using System;

namespace Gatosyocora.MeshDeleterWithTexture.Views
{
    public static class GatoGUILayout
    {
        public static Vector2 MiniMonitor(Texture texture, float width, float height, ref Rect rect, ref EventType type, Material material)
        {
            rect = GUILayoutUtility.GetRect(width, height, GUI.skin.box);

            Graphics.DrawTexture(rect, texture, material);

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

        public static string DragAndDropableArea(string text, string[] permissonExtensions, Action<string> onChanged)
        {
            EditorGUILayout.LabelField(
                text,
                GUI.skin.box,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)
            );
            var rect = GUILayoutUtility.GetLastRect();

            var e = Event.current;
            if ((e.type == EventType.DragPerform || e.type == EventType.DragUpdated) &&
                rect.Contains(e.mousePosition))
            {
                if (permissonExtensions.Contains(Path.GetExtension(DragAndDrop.paths.FirstOrDefault())))
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
            else if (e.type == EventType.DragExited && rect.Contains(e.mousePosition))
            {
                var path = DragAndDrop.paths.FirstOrDefault();
                if (!permissonExtensions.Contains(Path.GetExtension(path)))
                    return string.Empty;

                DragAndDrop.AcceptDrag();

                onChanged(path);

                return path;
            }

            return string.Empty;
        }

        public static T ObjectField<T>(string label, T value, bool allowSceneObjects = true) where T : UnityEngine.Object
        {
            return EditorGUILayout.ObjectField(label, value, typeof(T), allowSceneObjects) as T;
        }

        public static T ObjectField<T>(string label, T value, Action<T> onChanged, bool allowSceneObjects = true) where T : UnityEngine.Object
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var newValue = ObjectField(label, value, allowSceneObjects);
                if (check.changed)
                {
                    onChanged(newValue);
                }

                return newValue;
            }
        }

        public static T EnumPopup<T>(T value, Action<T> onChanged, params GUILayoutOption[] options) where T : Enum
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var newValue = (T)EditorGUILayout.EnumPopup(value, options);
                if (check.changed)
                {
                    onChanged(newValue);
                }

                return newValue;
            }
        }

        public static float Slider(string label, float value, float min, float max, Action<float> onChanged)
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var newValue = EditorGUILayout.Slider(label, value, min, max);
                if (check.changed)
                {
                    onChanged(newValue);
                }

                return newValue;
            }
        }

        public static int IntSlider(string label, int value, int min, int max, Action<int> onChanged)
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var newValue = EditorGUILayout.IntSlider(label, value, min, max);
                if (check.changed)
                {
                    onChanged(newValue);
                }

                return newValue;
            }
        }

        public static void Button(string text, Action onClicked, params GUILayoutOption[] options)
        {
            if (GUILayout.Button(text, options))
            {
                onClicked();
            }
        }

        public static void DisabledButton(string text, Action onClicked, bool disable, params GUILayoutOption[] options)
        {
            using (new EditorGUI.DisabledGroupScope(disable))
            {
                Button(text, onClicked, options);
            }
        }

        public class RightAlignedScope : GUI.Scope
        {
            EditorGUILayout.HorizontalScope horizontalScope;

            public RightAlignedScope()
            {
                horizontalScope = new EditorGUILayout.HorizontalScope();

                GUILayout.FlexibleSpace();
            }

            protected override void CloseScope()
            {
                horizontalScope.Dispose();
            }
        }
    }
}