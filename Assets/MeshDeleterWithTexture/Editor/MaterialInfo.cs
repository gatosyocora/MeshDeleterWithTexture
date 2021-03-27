using Gatosyocora.MeshDeleterWithTexture.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture
{
    /// <summary>
    /// 同じマテリアルが設定されたサブメッシュがどれかを管理するためのクラス
    /// </summary>
    public class MaterialInfo
    {
        public Texture2D Texture { get; private set; }
        public List<int> MaterialSlotIndices { get; private set; }
        public string Name { get; private set; }

        public MaterialInfo(Material mat, int slotIndex)
        {
            MaterialSlotIndices = new List<int>();
            AddSlotIndex(slotIndex);
            Name = mat.name;
            Texture = RendererUtility.GetMainTexture(mat);
        }

        public void AddSlotIndex(int index)
        {
            MaterialSlotIndices.Add(index);
        }
    }
}