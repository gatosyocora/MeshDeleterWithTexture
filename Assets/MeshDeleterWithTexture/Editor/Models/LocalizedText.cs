using System.Linq;
using UnityEngine;
using UnityEditor;
using System;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class LocalizedText
    {
        public static LanguagePack Data;

        public static Language selectedLanguage;

        private const string LOCAL_DATA_KEY = "mesh_deleter_language";

        public static void Initialize()
        {
            selectedLanguage = LoadLanguage();
            SetLanguage(selectedLanguage);
        }

        public static void SetLanguage(Language language)
        {
            var packs = Resources.FindObjectsOfTypeAll<LanguagePack>();
            Data = packs.Single(pack => pack.language == language);
            selectedLanguage = language;
            SaveLanguage();
        }

        private static Language LoadLanguage()
        {
                return Language.EN;
        }

        private static void SaveLanguage()
        {
        }
    }
}
