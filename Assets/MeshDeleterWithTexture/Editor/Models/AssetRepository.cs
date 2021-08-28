using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public static class AssetRepository
    {
        public static Material LoadTextureEditMaterial()
            => new Material(Shader.Find("Unlit/TextureEdit"));

        public static ComputeShader LoadDrawComputeShader()
            => Resources.Load<ComputeShader>("colorchecker2");

        public static ComputeShader LoadCreateUVMapComputeShader()
            => Resources.Load<ComputeShader>("getUVMap");

        public static LanguagePack[] LoadLanguagePacks()
            => Resources.FindObjectsOfTypeAll<LanguagePack>();
    }
}
