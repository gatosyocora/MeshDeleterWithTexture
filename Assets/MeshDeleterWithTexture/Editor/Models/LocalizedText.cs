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
            var languageString = EditorUserSettings.GetConfigValue(LOCAL_DATA_KEY);
            if (string.IsNullOrEmpty(languageString))
            {
                return Language.EN;
            }

            var obj = Enum.Parse(typeof(Language), languageString);
            if (obj != null)
            {
                return (Language)obj;
            } else
            {
                return Language.EN;
            }
        }

        private static void SaveLanguage()
        {
            EditorUserSettings.SetConfigValue(LOCAL_DATA_KEY, selectedLanguage.ToString());
        }
    }
}
