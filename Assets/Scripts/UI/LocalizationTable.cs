using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.UI
{
    public static class LocalizationTable
    {
        private static Dictionary<string, Dictionary<string, string>> tables;
        private static string currentLocale = "tr";
        private static bool loaded;

        public static event Action OnLocaleChanged;

        public static string CurrentLocale => currentLocale;

        public static string Get(string key)
        {
            EnsureLoaded();

            if (tables == null || string.IsNullOrEmpty(key))
            {
                return key;
            }

            if (!tables.TryGetValue(currentLocale, out Dictionary<string, string> table))
            {
                return key;
            }

            return table.TryGetValue(key, out string value) ? value : key;
        }

        public static void SetLocale(string locale)
        {
            if (string.Equals(currentLocale, locale, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            currentLocale = locale;

            if (Quest.GameSettings.Instance != null)
            {
                Quest.GameSettings.Instance.SetLanguage(locale);
                Quest.GameSettings.Instance.SaveSettings();
            }

            OnLocaleChanged?.Invoke();
        }

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            TextAsset jsonAsset = Resources.Load<TextAsset>("Localization/strings");
            if (jsonAsset != null)
            {
                try
                {
                    LocalizationData data = JsonUtility.FromJson<LocalizationData>(jsonAsset.text);
                    tables = new Dictionary<string, Dictionary<string, string>>();

                    if (data.tr != null)
                    {
                        Dictionary<string, string> trTable = new Dictionary<string, string>();
                        for (int i = 0; i < data.tr.Length; i++)
                        {
                            trTable[data.tr[i].key] = data.tr[i].value;
                        }
                        tables["tr"] = trTable;
                    }

                    if (data.en != null)
                    {
                        Dictionary<string, string> enTable = new Dictionary<string, string>();
                        for (int i = 0; i < data.en.Length; i++)
                        {
                            enTable[data.en[i].key] = data.en[i].value;
                        }
                        tables["en"] = enTable;
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

            if (Quest.GameSettings.Instance != null)
            {
                currentLocale = Quest.GameSettings.Instance.Language;
            }
        }

        private static void BuildFallbackTable()
        {
            tables = new Dictionary<string, Dictionary<string, string>>();

            var tr = new Dictionary<string, string>
            {
                // Main Menu
                { "play", "Oyna" },
                { "settings", "Ayarlar" },
                { "credits", "Jenerik" },
                { "quit", "Cikis" },
                { "back", "Geri" },

                // Settings
                { "settings_title", "AYARLAR" },
                { "audio", "SES" },
                { "graphics", "GRAFIK" },
                { "master_volume", "Ana Ses" },
                { "music_volume", "Muzik" },
                { "sfx_volume", "Efekt" },
                { "quality", "Kalite Seviyesi" },
                { "quality_low", "Dusuk" },
                { "quality_medium", "Orta" },
                { "quality_high", "Yuksek" },
                { "fullscreen", "Tam Ekran" },
                { "fps_limit", "FPS Siniri" },
                { "fps_unlimited", "Sinirsiz" },
                { "resolution", "Cozunurluk" },
                { "speed_unit", "Hiz Birimi" },
                { "language", "Dil" },

                // Pause
                { "paused_title", "OYUN DURAKLATILDI" },
                { "resume", "Devam Et" },
                { "quit_to_menu", "Ana Menuye Don" },
                { "quit_game", "Oyundan Cik" },
                { "confirm_quit", "Cikmak istediginize emin misiniz?" },
                { "confirm_quit_title", "Cikis Onayı" },
                { "confirm", "Onayla" },
                { "cancel", "Iptal" },

                // HUD
                { "balance_label", "Bakiye" },
                { "distance_label", "Mesafe" },
                { "eta_label", "ETA" },

                // Quest
                { "go_to", "Git:" },
                { "pick_up_cargo", "Kargoyu al" },
                { "deliver_to", "Teslim et:" },
                { "deliver_cargo", "Kargoyu teslim et" },
                { "delivery_complete", "TESLIMAT TAMAMLANDI" },
                { "delivery_failed", "TESLIMAT BASARISIZ" },

                // Phone Mission
                { "new_mission", "Yeni Gorev Teklifi" },
                { "mission_description", "Telefonuna yeni bir teslimat gorevi geldi." },
                { "accept", "Kabul Et" },
                { "reject", "Reddet" },
                { "reward_label", "Odul" },

                // Accessibility
                { "accessibility", "ERISILEBILIRLIK" },
                { "color_blind_mode", "Renk Korlugu Modu" },
                { "color_blind_none", "Yok" },
                { "color_blind_protanopia", "Protanopi" },
                { "color_blind_deuteranopia", "Deuteranopi" },
                { "color_blind_tritanopia", "Tritanopi" },
                { "text_scale", "Metin Boyutu" },
                { "high_contrast", "Yuksek Kontrast" },

                // Tutorial
                { "tutorial", "Egitim" },
                { "next", "Sonraki" },
                { "skip", "Atla" },
                { "step_of", "Adim {0}/{1}" },
                { "continue_space", "Devam icin SPACE, atlamak icin ESC" },
                { "restart_tutorial", "Egitimi Tekrar Goster" },

                // Loading
                { "loading", "Yukleniyor..." },
                { "tip_rain", "Ipucu: Yagmurlu havada dikkatli surun, bonus kazanin!" },
                { "tip_drift", "Ipucu: Drift yaparak ekstra puan kazanabilirsiniz!" },
                { "tip_brake", "Ipucu: Sert fren yapmaktan kacinin, ceza alinir." },
                { "tip_fragile", "Ipucu: Kirilgan kargoyu dikkatli tasiyin!" },
                { "tip_shortcut", "Ipucu: Ara sokaklari kullanarak zamandan tasarruf edin." },

                // Stats
                { "time", "Sure" },
                { "stops", "Duraklar" },
                { "cargo_condition", "Kargo durumu" },
                { "collisions", "Carpisma" },
                { "hard_brakes", "Sert fren" },
                { "drift_score", "Drift puani" },
                { "reward", "Odul" },
                { "penalty", "Ceza" },
                { "drift_bonus", "Drift bonusu" },
                { "reason", "Sebep" },

                // Gear
                { "gear_r", "R" },
                { "gear_n", "N" },
            };

            var en = new Dictionary<string, string>
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
                { "gear_r", "R" },
                { "gear_n", "N" },
            };

            tables["tr"] = tr;
            tables["en"] = en;
        }

        [Serializable]
        private class LocalizationData
        {
            public LocalizationEntry[] tr;
            public LocalizationEntry[] en;
        }

        [Serializable]
        private class LocalizationEntry
        {
            public string key;
            public string value;
        }
    }
}
