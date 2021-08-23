using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/*
 * Copyright (c) 2019 gatosyocora
 * Released under the MIT license.
 * see LICENSE.txt
 */

namespace Gatosyocora.MeshDeleterWithTexture.Utilities
{
    public static class RendererUtility
    {
        /// <summary>
        /// Meshを取得する
        /// </summary>
        /// <param name="renderer"></param>
        /// <returns></returns>
        public static Mesh GetMesh(Renderer renderer)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
            }
            else if (renderer is MeshRenderer meshRenderer)
            {
                mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            }
            return mesh;
        }

        /// <summary>
        /// Meshを設定する
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="mesh"></param>
        public static void SetMesh(Renderer renderer, Mesh mesh)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                skinnedMeshRenderer.sharedMesh = mesh;
            }
            else if (renderer is MeshRenderer)
            {
                renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Meshのポリゴン数を取得する
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static int GetMeshTriangleCount(Mesh mesh)
            => mesh.triangles.Length / 3;

        /// <summary>
        /// テクスチャを取得する
        /// </summary>
        /// <param name="renderer"></param>
        /// <returns></returns>
        public static Texture2D[] GetMainTextures(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            var textures = new Texture2D[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                textures[i] = GetMainTexture(materials[i]);
            }
            return textures;
        }

        /// <summary>
        /// MainTexを取得する
        /// _MainTexプロパティがなければ一番最初のテクスチャを取得する
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public static Texture2D GetMainTexture(Material mat)
        {
            var mainTex = mat.mainTexture as Texture2D;

            // シェーダーに_MainTexが含まれていない時, nullになるため
            // Materialの一番始めに設定されているTextureを取得する
            if (mainTex == null)
            {
                foreach (var textureName in mat.GetTexturePropertyNames())
                {
                    mainTex = mat.GetTexture(textureName) as Texture2D;
                    if (mainTex != null) break;
                }
            }

            // この時点でmainTexがnullならmaterialにテクスチャが設定されていない
            return mainTex;
        }

        /// <summary>
        /// mesh保存先のパスを取得する
        /// </summary>
        /// <param name="Mesh"></param>
        /// <returns></returns>
        public static string GetMeshPath(Mesh mesh)
            => Path.GetDirectoryName(AssetDatabase.GetAssetPath(mesh)).Replace("\\", "/");

        // TODO: メッシュ単位で戻ってしまう（サブメッシュ単位で戻したい）
        /// <summary>
        /// Rendererに設定されたMeshとMaterialsをPrefabの状態に戻す
        /// </summary>
        /// <param name="renderer"></param>
        public static void RevertMeshToPrefab(Renderer renderer)
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(renderer)) return;

            RevertMeshToPrefab(renderer, InteractionMode.UserAction);
            RevertMaterialsToPrefab(renderer, InteractionMode.UserAction);
        }

        public static void RevertMeshToPrefab(Renderer renderer, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            if (renderer is SkinnedMeshRenderer)
            {
                RevertPropertyToPrefab(renderer, "m_Mesh", interactionMode);
            }
            else if (renderer is MeshRenderer)
            {
                RevertPropertyToPrefab(renderer.GetComponent<MeshFilter>(), "m_Mesh", interactionMode);
            }
        }

        public static void RevertMaterialsToPrefab(Renderer renderer, InteractionMode interactionMode = InteractionMode.AutomatedAction)
            => RevertPropertyToPrefab(renderer, "m_Materials", interactionMode);

        private static void RevertPropertyToPrefab(Component component, string propertyName, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            if (component == null) return;

            var serializedObject = new SerializedObject(component);
            serializedObject.Update();
            var serializedProperty = serializedObject.FindProperty(propertyName);
            PrefabUtility.RevertPropertyOverride(serializedProperty, interactionMode);
            serializedObject.ApplyModifiedProperties();
        }


        public static string[] GetTextureNames(Texture2D[] textures)
        {
            var textureNames = textures.Select(x => x.name).ToArray();
            var processedItems = new List<string>();

            for (int texIndex = 0; texIndex < textureNames.Length; texIndex++)
            {
                var sameNameCount =
                    processedItems
                    .Where(x => x == textureNames[texIndex])
                    .Count();

                processedItems.Add(textureNames[texIndex]);

                if (sameNameCount > 0)
                    textureNames[texIndex] += "_" + sameNameCount;
            }

            return textureNames;
        }

        public static Material[] GetMaterials(Renderer renderer) => renderer.sharedMaterials.ToArray();

        public static MaterialInfo[] GetMaterialInfos(Renderer renderer)
        {
            var mats = renderer.sharedMaterials;
            var matInfos = new List<MaterialInfo>();
            var processedList = new List<string>();

            for (int matIndex = 0; matIndex < mats.Length; matIndex++)
            {
                if (!processedList.Contains(mats[matIndex].name))
                {
                    matInfos.Add(new MaterialInfo(mats[matIndex], matIndex));
                    processedList.Add(mats[matIndex].name);
                }
                else
                {
                    var infoIndex = processedList.IndexOf(mats[matIndex].name);
                    matInfos[infoIndex].AddSlotIndex(matIndex);
                }
            }

            return matInfos.ToArray();
        }

        public static void SetMaterials(Renderer renderer, Material[] materials)
        {
            if (renderer == null) return;

            if (renderer.sharedMaterials.Length != materials.Length)
            {
                throw new Exception("renderer.sharedMaterials.Length is not equal to materials.Length");
            }

            renderer.sharedMaterials = materials;
        }
    }
}