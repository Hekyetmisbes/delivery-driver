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
            if (string.IsNullOrEmpty(key)) return key;
            
            EnsureLoaded();

            if (tables == null || tables.Count == 0)
            {
                BuildFallbackTable();
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

            foreach (var locale in SupportedLocales)
            {
                if (TryGetValue(locale, key, out localizedValue)) return localizedValue;
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
                    if (data == null)
                    {
                        Debug.LogError("[LocalizationTable] JsonUtility.FromJson returned null. JSON might be malformed.");
                        BuildFallbackTable();
                        return;
                    }

                    tables = new Dictionary<string, Dictionary<string, string>>();

                    if (data.tr != null && data.tr.Length > 0)
                    {
                        tables[TurkishLocale] = BuildLocaleTable(data.tr);
                    }

                    if (data.en != null && data.en.Length > 0)
                    {
                        tables[EnglishLocale] = BuildLocaleTable(data.en);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LocalizationTable] Exception parsing strings.json: {e.Message}");
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
                    { "confirm_return_to_menu", "Ana menüye dönmek istediğinize emin misiniz?" },
                    { "confirm_return_to_menu_title", "Ana Menüye Dön" },
                    { "confirm", "Onayla" },
                    { "cancel", "İptal" },
                    { "border_warning", "SINIRDAN ÇIKIYORSUNUZ" },
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
                    { "fleet_title", "FİLO YÖNETİMİ" },
                    { "fleet_tab_vehicles", "ARAÇLARIM" },
                    { "fleet_tab_gallery", "GALERİ" },
                    { "fleet_tab_industry", "SANAYİ" },
                    { "fleet_owned_vehicles", "SAHİP OLUNAN ARAÇLAR" },
                    { "fleet_gallery_title", "ARAÇ GALERİSİ" },
                    { "fleet_industry_title", "SANAYİ VE MODİFİYE" },
                    { "fleet_coming_soon", "PEK YAKINDA..." },
                    { "fleet_use_vehicle", "ARACI KULLAN" },
                    { "fleet_buy_vehicle", "SATIN AL" },
                    { "fleet_insufficient_funds", "YETERSİZ BAKİYE" },
                    { "fleet_price_label", "FİYAT:" },
                    { "fleet_owned_status", "(SAHİBİSİN)" },
                    { "fleet_locked_status", "(KİLİTLİ)" },
                    { "fleet_purchased_status", "SATIN ALINDI" },
                    { "fleet_interaction_hint", "[E] Filoyu Yönet" },
                    { "vehicle_van_display", "Kamyonet (Minivan)" },
                    { "vehicle_truck_display", "Ağır Vasıta Tır" },
                    { "fleet_tab_upgrade", "GELİŞTİRME" },
                    { "upgrade_engine_title", "Motor Gücü" },
                    { "upgrade_brake_title", "Fren Gücü" },
                    { "upgrade_handling_title", "Yol Tutuşu" },
                    { "upgrade_level_label", "Seviye: {0}" },
                    { "upgrade_max_level", "MAX" },
                    { "upgrade_button_buy", "GELİŞTİR (${0})" },
                    { "phone_back_menu", "< Menü" },
                    { "phone_home_title", "Kurye Portalı" },
                    { "phone_shift_active", "Vardiya: Aktif | Şehir Teslimatları" },
                    { "phone_app_delivery_title", "Kurye Express" },
                    { "phone_app_delivery_sub", "Teslimat & İlanlar" },
                    { "phone_app_bank_title", "QuickPay Banka" },
                    { "phone_app_bank_sub", "{0} TL" },
                    { "phone_app_garage_title", "Garajım" },
                    { "phone_app_garage_sub", "Aktif Kurye Vanı" },
                    { "phone_app_messages_title", "Müşteri Yorumları" },
                    { "phone_app_messages_sub", "{0:F1} / 5.0 Puan" },
                    { "phone_toast_hint", "Açmak için [TAB] tuşuna bas" },
                    { "phone_toast_offer_title", "Yeni Teslimat Teklifi" },
                    { "delivery_app_header", "Kurye Express - İlanlar" },
                    { "delivery_badge_standard", "STANDART" },
                    { "delivery_badge_fragile", "KIRILABİLİR" },
                    { "delivery_badge_urgent", "ACİL KARGO" },
                    { "delivery_badge_multistop", "ÇOKLU TESLİMAT" },
                    { "delivery_offer_route_city", "Rota: Şehir İçi -> Teslimat Noktası" },
                    { "delivery_reward_label", "Ödül: {0}" },
                    { "delivery_btn_accept", "KABUL ET" },
                    { "delivery_btn_reject", "REDDET" },
                    { "delivery_active_header", "AKTİF TESLİMAT SÜRÜYOR" },
                    { "delivery_active_target", "Hedef: {0}" },
                    { "delivery_active_area_default", "Teslimat Bölgesi" },
                    { "delivery_status_carrying", "Kargo Aracınızda - Adrese Ulaştırın" },
                    { "delivery_status_pickup", "Yükleme Noktasına Gidin (Kutuyu Alın)" },
                    { "delivery_active_reward_notice", "Teslimat Tamamlanınca Bakiye Yüklenecek" },
                    { "delivery_empty_state", "Yeni kargo ilanları taranıyor...\n\nŞehirde gezerken yeni teslimat teklifleri telefonunuza bildirim olarak gelecektir." },
                    { "bank_app_header", "QuickPay Mobil Banka" },
                    { "bank_card_chip", "DEBIT CARD  )))" },
                    { "bank_card_balance_lbl", "Mevcut Bakiye" },
                    { "bank_card_account_type", "**** 4821   |   KURYE HESABI" },
                    { "bank_stat_career_earnings", "Kariyer Toplam Kazancı:" },
                    { "bank_stat_completed_jobs", "Tamamlanan İşler:" },
                    { "bank_stat_courier_level", "Kurye Seviyesi:" },
                    { "bank_stat_level_val", "Seviye {0}" },
                    { "bank_stat_delivery_count", "{0} Teslimat" },
                    { "bank_stat_next_level_prog", "Sonraki Seviyeye İlerleme:" },
                    { "garage_app_header", "Garajım & Araç Durumu" },
                    { "garage_vehicle_name", "Standart Kurye Vanı" },
                    { "garage_vehicle_status", "[+] Aktif & Hazır Durumda" },
                    { "garage_stat_total_driving", "Toplam Sürüş:" },
                    { "garage_stat_speed_bonuses", "Hız Bonusları:" },
                    { "garage_stat_speed_bonus_val", "{0} Kez" },
                    { "garage_stat_s_ranks", "S-Rank Başarıları:" },
                    { "garage_stat_s_rank_val", "{0} Teslimat" },
                    { "garage_tuning_tip", "[!] Para biriktirerek yeni araçlar satın alabilirsiniz!" },
                    { "messages_app_header", "Müşteri Yorumları & Puan" },
                    { "rating_tier_vip", "VIP / Mükemmel Kurye" },
                    { "rating_tier_senior", "Kıdemli Kurye" },
                    { "rating_tier_standard", "Standart Kurye" },
                    { "rating_tier_developing", "Gelişmekte Olan Kurye" },
                    { "rating_tier_risk", "Riskli Kurye (Düşük Bütçeli)" },
                    { "rating_bonus_positive", "+%{0} Kargo Primi" },
                    { "rating_bonus_negative", "%{0} Kesinti" },
                    { "toast_new_review", "Yeni Müşteri Değerlendirmesi" },
                    { "toast_customer_complaint", "Müşteri Şikayeti" },
                    { "messages_empty_state", "Henüz müşteri değerlendirmesi bulunmuyor.\n\nMüşteriler her 3 teslimatta bir kurye puanı ve değerlendirme yorumu bırakır.\n\nİlerleme: {0}/3 Teslimat Tamamlandı" },
                    { "messages_next_review_counter", "Sonraki değerlendirmeye kalan: {0} teslimat" },
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
                    { "confirm_return_to_menu", "Are you sure you want to return to the main menu?" },
                    { "confirm_return_to_menu_title", "Return to Menu" },
                    { "confirm", "Confirm" },
                    { "cancel", "Cancel" },
                    { "border_warning", "YOU ARE LEAVING THE BOUNDARY" },
                    { "balance_label", "Balance" },
                    { "distance_label", "Distance" },
                    { "eta_label", "ETA" },
                    { "go_to", "Go to:" },
                    { "pick_up_cargo", "Pick up cargo" },
                    { "deliver_to", "Deliver to:" },
                    { "deliver_cargo", "Deliver cargo" },
                    { "delivery_complete", "DELIVERY COMPLETE" },
                    { "delivery_failed", "DELIVERY FAILED" },
                    { "new_mission", "New Mission Offer" },
                    { "mission_description", "A new delivery mission has arrived on your phone." },
                    { "accept", "Accept" },
                    { "reject", "Reject" },
                    { "reward_label", "Reward" },
                    { "accessibility", "ACCESSIBILITY" },
                    { "color_blind_mode", "Color Blind Mode" },
                    { "color_blind_none", "None" },
                    { "color_blind_protanopia", "Protanopia" },
                    { "color_blind_deuteranopia", "Deuteranopia" },
                    { "color_blind_tritanopia", "Tritanopia" },
                    { "text_scale", "Text Scale" },
                    { "high_contrast", "High Contrast" },
                    { "tutorial", "Tutorial" },
                    { "next", "Next" },
                    { "skip", "Skip" },
                    { "step_of", "Step {0}/{1}" },
                    { "continue_space", "SPACE to continue, ESC to skip" },
                    { "restart_tutorial", "Restart Tutorial" },
                    { "loading", "Loading..." },
                    { "tip_rain", "Tip: Drive carefully in rain for bonus rewards!" },
                    { "tip_drift", "Tip: You can earn extra points by drifting!" },
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
                    { "fleet_title", "FLEET MANAGEMENT" },
                    { "fleet_tab_vehicles", "MY VEHICLES" },
                    { "fleet_tab_gallery", "GALLERY" },
                    { "fleet_tab_industry", "INDUSTRY" },
                    { "fleet_owned_vehicles", "OWNED VEHICLES" },
                    { "fleet_gallery_title", "VEHICLE GALLERY" },
                    { "fleet_industry_title", "INDUSTRY & CUSTOM" },
                    { "fleet_coming_soon", "COMING SOON..." },
                    { "fleet_use_vehicle", "DRIVE VEHICLE" },
                    { "fleet_buy_vehicle", "BUY NOW" },
                    { "fleet_insufficient_funds", "INSUFFICIENT FUNDS" },
                    { "fleet_price_label", "PRICE:" },
                    { "fleet_owned_status", "(OWNED)" },
                    { "fleet_locked_status", "(LOCKED)" },
                    { "fleet_purchased_status", "PURCHASED" },
                    { "fleet_interaction_hint", "[E] Manage Fleet" },
                    { "vehicle_van_display", "Van (Minivan)" },
                    { "vehicle_truck_display", "Heavy Duty Truck" },
                    { "fleet_tab_upgrade", "UPGRADE" },
                    { "upgrade_engine_title", "Engine Power" },
                    { "upgrade_brake_title", "Braking Force" },
                    { "upgrade_handling_title", "Handling" },
                    { "upgrade_level_label", "Level: {0}" },
                    { "upgrade_max_level", "MAX" },
                    { "upgrade_button_buy", "UPGRADE (${0})" },
                    { "phone_back_menu", "< Menu" },
                    { "phone_home_title", "Courier Portal" },
                    { "phone_shift_active", "Shift: Active | City Deliveries" },
                    { "phone_app_delivery_title", "Courier Express" },
                    { "phone_app_delivery_sub", "Deliveries & Offers" },
                    { "phone_app_bank_title", "QuickPay Bank" },
                    { "phone_app_bank_sub", "${0}" },
                    { "phone_app_garage_title", "My Garage" },
                    { "phone_app_garage_sub", "Active Courier Van" },
                    { "phone_app_messages_title", "Customer Reviews" },
                    { "phone_app_messages_sub", "{0:F1} / 5.0 Rating" },
                    { "phone_toast_hint", "Press [TAB] to open" },
                    { "phone_toast_offer_title", "New Delivery Offer" },
                    { "delivery_app_header", "Courier Express - Offers" },
                    { "delivery_badge_standard", "STANDARD" },
                    { "delivery_badge_fragile", "FRAGILE" },
                    { "delivery_badge_urgent", "URGENT" },
                    { "delivery_badge_multistop", "MULTI-STOP" },
                    { "delivery_offer_route_city", "Route: City Center -> Destination" },
                    { "delivery_reward_label", "Reward: {0}" },
                    { "delivery_btn_accept", "ACCEPT" },
                    { "delivery_btn_reject", "REJECT" },
                    { "delivery_active_header", "ACTIVE DELIVERY IN PROGRESS" },
                    { "delivery_active_target", "Destination: {0}" },
                    { "delivery_active_area_default", "Delivery Area" },
                    { "delivery_status_carrying", "Cargo in Vehicle - Deliver to Address" },
                    { "delivery_status_pickup", "Go to Loading Point (Pick Up Cargo)" },
                    { "delivery_active_reward_notice", "Reward will be credited on delivery" },
                    { "delivery_empty_state", "Scanning for new cargo offers...\n\nNew delivery offers will arrive as phone notifications while driving in the city." },
                    { "bank_app_header", "QuickPay Mobile Bank" },
                    { "bank_card_chip", "DEBIT CARD  )))" },
                    { "bank_card_balance_lbl", "Current Balance" },
                    { "bank_card_account_type", "**** 4821   |   COURIER ACCOUNT" },
                    { "bank_stat_career_earnings", "Total Career Earnings:" },
                    { "bank_stat_completed_jobs", "Completed Deliveries:" },
                    { "bank_stat_courier_level", "Courier Level:" },
                    { "bank_stat_level_val", "Level {0}" },
                    { "bank_stat_delivery_count", "{0} Deliveries" },
                    { "bank_stat_next_level_prog", "Progress to Next Level:" },
                    { "garage_app_header", "My Garage & Vehicle Status" },
                    { "garage_vehicle_name", "Standard Courier Van" },
                    { "garage_vehicle_status", "[+] Active & Ready" },
                    { "garage_stat_total_driving", "Total Driving:" },
                    { "garage_stat_speed_bonuses", "Speed Bonuses:" },
                    { "garage_stat_speed_bonus_val", "{0} Times" },
                    { "garage_stat_s_ranks", "S-Rank Deliveries:" },
                    { "garage_stat_s_rank_val", "{0} Deliveries" },
                    { "garage_tuning_tip", "[!] Save money to purchase new vehicles!" },
                    { "messages_app_header", "Customer Reviews & Rating" },
                    { "rating_tier_vip", "VIP / Excellent Courier" },
                    { "rating_tier_senior", "Senior Courier" },
                    { "rating_tier_standard", "Standard Courier" },
                    { "rating_tier_developing", "Developing Courier" },
                    { "rating_tier_risk", "At-Risk Courier (Budget)" },
                    { "rating_bonus_positive", "+{0}% Cargo Bonus" },
                    { "rating_bonus_negative", "{0}% Penalty" },
                    { "toast_new_review", "New Customer Review" },
                    { "toast_customer_complaint", "Customer Complaint" },
                    { "messages_empty_state", "No customer reviews yet.\n\nCustomers leave ratings and reviews every 3 deliveries.\n\nProgress: {0}/3 Deliveries Completed" },
                    { "messages_next_review_counter", "Next review in: {0} deliveries" },
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
