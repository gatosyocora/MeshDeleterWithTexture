using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class LocalizedText
    {
        public Dictionary<Language, LanguagePack> Languages { get; private set; } 

        public LocalizedText()
        {
            var packs = Resources.FindObjectsOfTypeAll<LanguagePack>();
            Languages = packs.ToDictionary(pack => pack.language);
        }
    }
}
