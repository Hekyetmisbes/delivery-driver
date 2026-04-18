using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.UI
{
    public static class LocalizationTable
    {
        public const string TurkishLocale = "tr";
        public const string EnglishLocale = "en";

        private static readonly string[] SupportedLocales = { TurkishLocale, EnglishLocale };

        private static Dictionary<string, Dictionary<string, string>> tables;
        private static string currentLocale = TurkishLocale;
        private static bool loaded;
        private static bool subscribedToSettings;

        public static event Action OnLocaleChanged;

        public static string CurrentLocale => currentLocale;
        public static IReadOnlyList<string> LocaleCodes => SupportedLocales;

        public static string Get(string key)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            if (TryGetValue(currentLocale, key, out string localizedValue))
            {
                return localizedValue;
            }

            if (!string.Equals(currentLocale, EnglishLocale, StringComparison.OrdinalIgnoreCase) &&
                TryGetValue(EnglishLocale, key, out localizedValue))
            {
                return localizedValue;
            }

            return key;
        }

        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0)
            {
                return template;
            }

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public static void SetLocale(string locale)
        {
            EnsureLoaded();

            string normalizedLocale = NormalizeLocale(locale);
            bool localeChanged = !string.Equals(currentLocale, normalizedLocale, StringComparison.OrdinalIgnoreCase);
            currentLocale = normalizedLocale;

            if (Quest.GameSettings.Instance != null &&
                !string.Equals(Quest.GameSettings.Instance.Language, normalizedLocale, StringComparison.OrdinalIgnoreCase))
            {
                Quest.GameSettings.Instance.SetLanguage(normalizedLocale);
                Quest.GameSettings.Instance.SaveSettings();
            }

            if (localeChanged)
            {
                OnLocaleChanged?.Invoke();
            }
        }

        public static string GetLocaleDisplayName(string locale)
        {
            return NormalizeLocale(locale) == TurkishLocale
                ? Get("language_turkish")
                : Get("language_english");
        }

        public static int GetLocaleIndex(string locale)
        {
            string normalizedLocale = NormalizeLocale(locale);
            for (int i = 0; i < SupportedLocales.Length; i++)
            {
                if (string.Equals(SupportedLocales[i], normalizedLocale, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        public static string GetLocaleByIndex(int index)
        {
            if (index < 0 || index >= SupportedLocales.Length)
            {
                return TurkishLocale;
            }

            return SupportedLocales[index];
        }

        public static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            EnsureSettingsSubscription();

            TextAsset jsonAsset = Resources.Load<TextAsset>("Localization/strings");
            if (jsonAsset != null)
            {
                try
                {
                    LocalizationData data = JsonUtility.FromJson<LocalizationData>(jsonAsset.text);
                    tables = new Dictionary<string, Dictionary<string, string>>();

                    if (data.tr != null)
                    {
                        tables[TurkishLocale] = BuildLocaleTable(data.tr);
                    }

                    if (data.en != null)
                    {
                        tables[EnglishLocale] = BuildLocaleTable(data.en);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LocalizationTable] Failed to parse strings.json: {e.Message}");
                    BuildFallbackTable();
                }
            }
            else
            {
                BuildFallbackTable();
            }

            currentLocale = NormalizeLocale(Quest.GameSettings.Instance != null
                ? Quest.GameSettings.Instance.Language
                : currentLocale);
        }

        private static Dictionary<string, string> BuildLocaleTable(LocalizationEntry[] entries)
        {
            Dictionary<string, string> localeTable = new Dictionary<string, string>();
            for (int i = 0; i < entries.Length; i++)
            {
                localeTable[entries[i].key] = entries[i].value;
            }

            return localeTable;
        }

        private static void BuildFallbackTable()
        {
            tables = new Dictionary<string, Dictionary<string, string>>
            {
                [TurkishLocale] = new Dictionary<string, string>
                {
                    { "play", "Oyna" },
                    { "settings", "Ayarlar" },
                    { "credits", "Jenerik" },
                    { "quit", "Çıkış" },
                    { "back", "Geri" },
                    { "settings_title", "AYARLAR" },
                    { "audio", "SES" },
                    { "graphics", "GRAFİK" },
                    { "master_volume", "Ana Ses" },
                    { "music_volume", "Müzik" },
                    { "sfx_volume", "Efekt" },
                    { "quality", "Kalite Seviyesi" },
                    { "quality_low", "Düşük" },
                    { "quality_medium", "Orta" },
                    { "quality_high", "Yüksek" },
                    { "fullscreen", "Tam Ekran" },
                    { "fps_limit", "FPS Sınırı" },
                    { "fps_unlimited", "Sınırsız" },
                    { "resolution", "Çözünürlük" },
                    { "speed_unit", "Hız Birimi" },
                    { "language", "Dil" },
                    { "language_turkish", "Türkçe" },
                    { "language_english", "İngilizce" },
                    { "difficulty_match_player_level", "Oyuncu Seviyesine Göre" },
                    { "difficulty_easy", "Kolay" },
                    { "difficulty_medium", "Orta" },
                    { "difficulty_hard", "Zor" },
                    { "difficulty_expert", "Uzman" },
                    { "paused_title", "OYUN DURAKLATILDI" },
                    { "resume", "Devam Et" },
                    { "quit_to_menu", "Ana Menüye Dön" },
                    { "quit_game", "Oyundan Çık" },
                    { "confirm_quit", "Çıkmak istediğinize emin misiniz?" },
                    { "confirm_quit_title", "Çıkış Onayı" },
                    { "confirm", "Onayla" },
                    { "cancel", "İptal" },
                    { "balance_label", "Bakiye" },
                    { "distance_label", "Mesafe" },
                    { "eta_label", "ETA" },
                    { "go_to", "Git:" },
                    { "pick_up_cargo", "Kargoyu al" },
                    { "deliver_to", "Teslim et:" },
                    { "deliver_cargo", "Kargoyu teslim et" },
                    { "delivery_complete", "TESLİMAT TAMAMLANDI" },
                    { "delivery_failed", "TESLİMAT BAŞARISIZ" },
                    { "new_mission", "Yeni Görev Teklifi" },
                    { "mission_description", "Telefonuna yeni bir teslimat görevi geldi." },
                    { "accept", "Kabul Et" },
                    { "reject", "Reddet" },
                    { "reward_label", "Ödül" },
                    { "accessibility", "ERİŞİLEBİLİRLİK" },
                    { "color_blind_mode", "Renk Körlüğü Modu" },
                    { "color_blind_none", "Yok" },
                    { "color_blind_protanopia", "Protanopi" },
                    { "color_blind_deuteranopia", "Deuteranopi" },
                    { "color_blind_tritanopia", "Tritanopi" },
                    { "text_scale", "Metin Boyutu" },
                    { "high_contrast", "Yüksek Kontrast" },
                    { "tutorial", "Eğitim" },
                    { "next", "Sonraki" },
                    { "skip", "Atla" },
                    { "step_of", "Adım {0}/{1}" },
                    { "continue_space", "Devam için SPACE, atlamak için ESC" },
                    { "restart_tutorial", "Eğitimi Tekrar Göster" },
                    { "loading", "Yükleniyor..." },
                    { "tip_rain", "İpucu: Yağmurlu havada dikkatli sürün, bonus kazanın!" },
                    { "tip_drift", "İpucu: Drift yaparak ekstra puan kazanabilirsiniz!" },
                    { "tip_brake", "İpucu: Sert fren yapmaktan kaçının, ceza alınır." },
                    { "tip_fragile", "İpucu: Kırılgan kargoyu dikkatli taşıyın!" },
                    { "tip_shortcut", "İpucu: Ara sokakları kullanarak zamandan tasarruf edin." },
                    { "time", "Süre" },
                    { "stops", "Duraklar" },
                    { "cargo_condition", "Kargo durumu" },
                    { "collisions", "Çarpışma" },
                    { "hard_brakes", "Sert fren" },
                    { "drift_score", "Drift puanı" },
                    { "reward", "Ödül" },
                    { "penalty", "Ceza" },
                    { "drift_bonus", "Drift bonusu" },
                    { "reason", "Sebep" },
                    { "credits_game_by", "Oyunu geliştiren" },
                    { "credits_used_assets", "Kullanılan Varlıklar" },
                    { "credits_thanks", "Delivery Driver oynadığın için teşekkürler." },
                    { "gear_r", "R" },
                    { "gear_n", "N" },
                },
                [EnglishLocale] = new Dictionary<string, string>
                {
                    { "play", "Play" },
                    { "settings", "Settings" },
                    { "credits", "Credits" },
                    { "quit", "Quit" },
                    { "back", "Back" },
                    { "settings_title", "SETTINGS" },
                    { "audio", "AUDIO" },
                    { "graphics", "GRAPHICS" },
                    { "master_volume", "Master Volume" },
                    { "music_volume", "Music" },
                    { "sfx_volume", "Effects" },
                    { "quality", "Quality" },
                    { "quality_low", "Low" },
                    { "quality_medium", "Medium" },
                    { "quality_high", "High" },
                    { "fullscreen", "Fullscreen" },
                    { "fps_limit", "FPS Limit" },
                    { "fps_unlimited", "Unlimited" },
                    { "resolution", "Resolution" },
                    { "speed_unit", "Speed Unit" },
                    { "language", "Language" },
                    { "language_turkish", "Turkish" },
                    { "language_english", "English" },
                    { "difficulty_match_player_level", "Match Player Level" },
                    { "difficulty_easy", "Easy" },
                    { "difficulty_medium", "Medium" },
                    { "difficulty_hard", "Hard" },
                    { "difficulty_expert", "Expert" },
                    { "paused_title", "GAME PAUSED" },
                    { "resume", "Resume" },
                    { "quit_to_menu", "Return to Menu" },
                    { "quit_game", "Quit Game" },
                    { "confirm_quit", "Are you sure you want to quit?" },
                    { "confirm_quit_title", "Confirm Quit" },
                    { "confirm", "Confirm" },
                    { "cancel", "Cancel" },
                    { "balance_label", "Balance" },
                    { "distance_label", "Distance" },
                    { "eta_label", "ETA" },
                    { "go_to", "Go to" },
                    { "pick_up_cargo", "Pick up cargo" },
                    { "deliver_to", "Deliver to" },
                    { "deliver_cargo", "Deliver cargo" },
                    { "delivery_complete", "DELIVERY COMPLETE" },
                    { "delivery_failed", "DELIVERY FAILED" },
                    { "new_mission", "New Mission Offer" },
                    { "mission_description", "A new delivery mission has arrived." },
                    { "accept", "Accept" },
                    { "reject", "Reject" },
                    { "reward_label", "Reward" },
                    { "accessibility", "ACCESSIBILITY" },
                    { "color_blind_mode", "Color Blind Mode" },
                    { "color_blind_none", "None" },
                    { "color_blind_protanopia", "Protanopia" },
                    { "color_blind_deuteranopia", "Deuteranopia" },
                    { "color_blind_tritanopia", "Tritanopia" },
                    { "text_scale", "Text Size" },
                    { "high_contrast", "High Contrast" },
                    { "tutorial", "Tutorial" },
                    { "next", "Next" },
                    { "skip", "Skip" },
                    { "step_of", "Step {0}/{1}" },
                    { "continue_space", "SPACE to continue, ESC to skip" },
                    { "restart_tutorial", "Show Tutorial Again" },
                    { "loading", "Loading..." },
                    { "tip_rain", "Tip: Drive carefully in rain for bonus rewards!" },
                    { "tip_drift", "Tip: Drift to earn extra points!" },
                    { "tip_brake", "Tip: Avoid hard braking to prevent penalties." },
                    { "tip_fragile", "Tip: Handle fragile cargo with care!" },
                    { "tip_shortcut", "Tip: Use side streets to save time." },
                    { "time", "Time" },
                    { "stops", "Stops" },
                    { "cargo_condition", "Cargo condition" },
                    { "collisions", "Collisions" },
                    { "hard_brakes", "Hard brakes" },
                    { "drift_score", "Drift score" },
                    { "reward", "Reward" },
                    { "penalty", "Penalty" },
                    { "drift_bonus", "Drift bonus" },
                    { "reason", "Reason" },
                    { "credits_game_by", "Game by" },
                    { "credits_used_assets", "Used Assets" },
                    { "credits_thanks", "Thanks for playing Delivery Driver." },
                    { "gear_r", "R" },
                    { "gear_n", "N" },
                }
            };
        }

        private static void EnsureSettingsSubscription()
        {
            if (subscribedToSettings)
            {
                return;
            }

            Quest.GameSettings.OnLanguageChanged += HandleGameSettingsLanguageChanged;
            subscribedToSettings = true;
        }

        private static void HandleGameSettingsLanguageChanged()
        {
            string normalizedLocale = NormalizeLocale(Quest.GameSettings.Instance != null
                ? Quest.GameSettings.Instance.Language
                : currentLocale);

            if (string.Equals(currentLocale, normalizedLocale, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            currentLocale = normalizedLocale;
            OnLocaleChanged?.Invoke();
        }

        private static bool TryGetValue(string locale, string key, out string value)
        {
            value = null;

            if (tables == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (!tables.TryGetValue(locale, out Dictionary<string, string> table))
            {
                return false;
            }

            return table.TryGetValue(key, out value);
        }

        private static string NormalizeLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                return TurkishLocale;
            }

            return string.Equals(locale.Trim(), EnglishLocale, StringComparison.OrdinalIgnoreCase)
                ? EnglishLocale
                : TurkishLocale;
        }

        [Serializable]
        private class LocalizationData
        {
            public LocalizationEntry[] tr = Array.Empty<LocalizationEntry>();
            public LocalizationEntry[] en = Array.Empty<LocalizationEntry>();
        }

        [Serializable]
        private class LocalizationEntry
        {
            public string key = string.Empty;
            public string value = string.Empty;
        }
    }
}
