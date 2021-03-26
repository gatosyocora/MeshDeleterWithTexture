using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gatosyocora.MeshDeleterWithTexture.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeshDeleterWithTexture.Tests
{
    public class RendererUtilityTest
    {
        private static readonly string modelPath = "Assets/MeshDeleterWithTexture/Tests/Model/";

        public class Model
        {
            public Model(string name, string path, string modelDataPath, string rendererName, int triangleCount, string[] textureNames)
            {
                this.name = name;
                this.path = path;
                this.modelDataPath = modelDataPath;
                this.textureNames = textureNames;
                this.rendererMeshTriangleCount = triangleCount;
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                renderer = instance.transform.Find(rendererName).GetComponent<SkinnedMeshRenderer>();
            }

            public string name;
            public string path;
            public string modelDataPath;
            public int rendererMeshTriangleCount;
            public GameObject prefab, instance;
            public SkinnedMeshRenderer renderer;
            public string[] textureNames;
        }

        public List<Model> models = new List<Model>();

        public void Setup()
        {
            models.Add(
                new Model(
                    "Shapell",
                    modelPath + "Shapell/shapell.prefab",
                    modelPath + "Shapell/data",
                    "Body", 27873,
                    new string[]
                    {
                        "shapell_hair",
                        "shapell_blazer",
                        "shapell_clothes",
                        "shapell_body",
                        "shapell_faceex01"
                    }));
            models.Add(
                new Model(
                    "Yuuko",
                    "Assets/幽狐さん/yuuko_san.prefab",
                    "Assets/幽狐さん",
                    "Body", 18066,
                    new string[]
                    {
                        "body",
                        "hair",
                        "facepart",
                        "Costume"
                    }));
        }

        [Test]
        public void GetMesh()
        {
            Setup();
            foreach (var model in models)
            {
                var mesh = RendererUtility.GetMesh(model.renderer);
                Assert.IsNotNull(mesh);
            }
        }
        [Test]
        public void GetMeshPath()
        {
            Setup();
            foreach (var model in models)
            {
                var mesh = RendererUtility.GetMesh(model.renderer);
                var path = RendererUtility.GetMeshPath(mesh);
                Assert.AreEqual(path, model.modelDataPath);
            }
        }
        [Test]
        public void GetMeshTriangleCount()
        {
            Setup();
            foreach (var model in models)
            {
                var mesh = RendererUtility.GetMesh(model.renderer);
                var triangleCount = RendererUtility.GetMeshTriangleCount(mesh);
                Assert.AreEqual(triangleCount, model.rendererMeshTriangleCount);
            }
        }
        [Test]
        public void GetMainTextures()
        {
            Setup();
            foreach (var model in models)
            {
                var textures = RendererUtility.GetMainTextures(model.renderer);
                Assert.AreEqual(textures.Length, model.textureNames.Length);
                for (int i = 0; i < textures.Length; i++)
                {
                    Assert.AreEqual(textures[i].name, model.textureNames[i]);
                }
            }
        }
        [Test]
        public void GetTextureNames()
        {
            Setup();
            foreach (var model in models)
            {
                var textures = RendererUtility.GetMainTextures(model.renderer);
                var textureNames = RendererUtility.GetTextureNames(textures);
                Assert.AreEqual(textureNames.Length, model.textureNames.Length);
                for (int i = 0; i < textures.Length; i++)
                {
                    Assert.AreEqual(textureNames[i], model.textureNames[i]);
                }
            }
        }
        [Test]
        public void SetMesh()
        {
            var mesh = new Mesh
            {
                name = "mesh2",
                vertices = new Vector3[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(1, 0, 0)
                },
                triangles = new int[]{0, 1, 2}
            };

            Setup();
            foreach (var model in models)
            {
                RendererUtility.SetMesh(model.renderer, mesh);
                Assert.AreEqual(model.renderer.sharedMesh, mesh);
            }
        }
        [Test]
        public void RevertMeshToPrefab()
        {

        }
        [Test]
        public void ResetMaterialTextures()
        {

        }
    }
}
