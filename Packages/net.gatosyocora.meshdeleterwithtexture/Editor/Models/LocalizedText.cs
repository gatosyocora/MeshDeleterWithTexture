using System.Linq;
using UnityEngine;
using UnityEditor;
using System;

namespace Gatosyocora.MeshDeleterWithTexture.Models
{
    public class LocalizedText
    {
        public LanguagePack Data { get; private set; }

        public Language SelectedLanguage { get; private set; }

        private const string LOCAL_DATA_KEY = "mesh_deleter_language";

        public LocalizedText()
        {
            SelectedLanguage = LoadLanguage();
            SetLanguage(SelectedLanguage);
        }

        public void SetLanguage(Language language)
        {
            var packs = AssetRepository.LoadLanguagePacks();
            Data = packs.Single(pack => pack.language == language);
            SelectedLanguage = language;
            SaveLanguage();
        }

        private Language LoadLanguage()
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

        private void SaveLanguage()
        {
            EditorUserSettings.SetConfigValue(LOCAL_DATA_KEY, SelectedLanguage.ToString());
        }
    }
}
