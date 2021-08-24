using System.Linq;
using UnityEngine;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class LocalizedText
    {
        public static LanguagePack Data;

        public static void SetLanguage(Language language)
        {
            var packs = Resources.FindObjectsOfTypeAll<LanguagePack>();
            Data = packs.Single(pack => pack.language == language);
        }
    }
}
