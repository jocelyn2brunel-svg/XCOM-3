using System;
using System.Collections.Generic;

namespace XCOM_3
{
    public enum SupportedLanguage
    {
        French,
        English
    }

    public static class LocalizationManager
    {
        private static readonly Dictionary<SupportedLanguage, Dictionary<string, string>> _translations =
            new Dictionary<SupportedLanguage, Dictionary<string, string>>
            {
                [SupportedLanguage.French] = new Dictionary<string, string>
                {
                    ["menu.new_game"] = "Nouvelle partie",
                    ["menu.continue"] = "Continuer",
                    ["menu.map_editor"] = "Éditeur de carte",
                    ["menu.character_creation"] = "Création de personnage",
                    ["menu.encyclopedia"] = "Encyclopédie",
                    ["menu.body_editor"] = "Éditeur du corps",
                    ["menu.options"] = "Options",
                    ["menu.quit"] = "Quitter",

                    ["options.title"] = "Options",
                    ["options.music_volume_plus"] = "Volume musique +",
                    ["options.music_volume_minus"] = "Volume musique -",
                    ["options.back"] = "Retour",
                    ["options.shooter_camera"] = "Caméra proche au tir",
                    ["options.ui_color"] = "Couleur UI (RGB)",
                    ["options.language"] = "Langue",
                    ["options.language_value_french"] = "Français",
                    ["options.language_value_english"] = "Anglais",
                    ["options.language_button"] = "Langue : {0}",
                },
                [SupportedLanguage.English] = new Dictionary<string, string>
                {
                    ["menu.new_game"] = "New Game",
                    ["menu.continue"] = "Continue",
                    ["menu.map_editor"] = "Map Editor",
                    ["menu.character_creation"] = "Character Creation",
                    ["menu.encyclopedia"] = "Encyclopedia",
                    ["menu.body_editor"] = "Body Editor",
                    ["menu.options"] = "Options",
                    ["menu.quit"] = "Quit",

                    ["options.title"] = "Options",
                    ["options.music_volume_plus"] = "Music Volume +",
                    ["options.music_volume_minus"] = "Music Volume -",
                    ["options.back"] = "Back",
                    ["options.shooter_camera"] = "Shooter close camera",
                    ["options.ui_color"] = "UI Color (RGB)",
                    ["options.language"] = "Language",
                    ["options.language_value_french"] = "French",
                    ["options.language_value_english"] = "English",
                    ["options.language_button"] = "Language: {0}",
                }
            };

        public static event Action<SupportedLanguage> OnLanguageChanged;

        public static SupportedLanguage CurrentLanguage { get; private set; } = SupportedLanguage.French;

        public static void SetLanguage(SupportedLanguage language)
        {
            if (language == CurrentLanguage)
            {
                return;
            }

            CurrentLanguage = language;
            OnLanguageChanged?.Invoke(language);
        }

        public static void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == SupportedLanguage.French
                ? SupportedLanguage.English
                : SupportedLanguage.French);
        }

        public static string Get(string key)
        {
            if (_translations.TryGetValue(CurrentLanguage, out Dictionary<string, string> currentLanguageTranslations)
                && currentLanguageTranslations.TryGetValue(key, out string currentLanguageValue))
            {
                return currentLanguageValue;
            }

            if (_translations.TryGetValue(SupportedLanguage.English, out Dictionary<string, string> englishTranslations)
                && englishTranslations.TryGetValue(key, out string englishValue))
            {
                return englishValue;
            }

            return key;
        }

        public static string GetLanguageDisplayName(SupportedLanguage language)
        {
            return language == SupportedLanguage.French
                ? Get("options.language_value_french")
                : Get("options.language_value_english");
        }

        public static string GetFormatted(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }
    }
}
