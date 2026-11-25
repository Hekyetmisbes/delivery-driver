# Kullanıcı tarafından sağlanan Unity script envanteri → UI konsept haritası

Bu harita gerçek kaynak dosyaları değil, gönderilen **sınıf adı ve açıklama envanterini** esas alır.

| Ekran | Öngörülen Unity script/sistem | SVG |
|---|---|---|
| Ana Menü | `MainMenuRuntimeUI` | `01_Screens/SVG/01_Menus/main_menu.svg` |
| Duraklatma | `PauseMenuUI;PauseMenuRuntimeView;PauseMenuRuntimeViewBuilder` | `01_Screens/SVG/01_Menus/pause_menu.svg` |
| Genel Ayarlar | `SettingsMenuUI` | `01_Screens/SVG/01_Menus/settings_general.svg` |
| Grafik Ayarları | `SettingsMenuUI` | `01_Screens/SVG/01_Menus/settings_graphics.svg` |
| Ses Ayarları | `SettingsMenuUI` | `01_Screens/SVG/01_Menus/settings_audio.svg` |
| Tuş Atamaları | `SettingsMenuUI` | `01_Screens/SVG/01_Menus/settings_controls.svg` |
| Oynanış Ayarları | `SettingsMenuUI` | `01_Screens/SVG/01_Menus/settings_gameplay.svg` |
| Erişilebilirlik | `AccessibilityManager` | `01_Screens/SVG/01_Menus/settings_accessibility.svg` |
| Kayıt Yükleme | `SaveLoadUI` | `01_Screens/SVG/01_Menus/save_load.svg` |
| Yükleme | `LoadingScreenUI;SceneTransitionManager` | `01_Screens/SVG/01_Menus/loading_screen.svg` |
| Yapımcılar | `CreditsSceneRuntimeUI` | `01_Screens/SVG/01_Menus/credits.svg` |
| Ana Sürüş HUD | `GameplayHUD;BalanceHudUI;FuelHudUI;SpeedometerUI` | `01_Screens/SVG/02_Vehicle/gameplay_hud.svg` |
| Hız Göstergesi | `SpeedometerUI` | `01_Screens/SVG/02_Vehicle/speedometer_detail.svg` |
| Geri Görüş | `ReverseCameraHUD` | `01_Screens/SVG/02_Vehicle/reverse_camera.svg` |
| Düşük Yakıt Uyarısı | `FuelHudUI` | `01_Screens/SVG/02_Vehicle/fuel_low_warning.svg` |
| Yakıt İkmali | `FuelStationUI` | `01_Screens/SVG/02_Vehicle/fuel_station.svg` |
| Çarpışma Hasarı | `DamageIndicatorHUD` | `01_Screens/SVG/02_Vehicle/damage_indicator.svg` |
| Harita Sınırı | `BorderRespawnGuard` | `01_Screens/SVG/02_Vehicle/world_boundary.svg` |
| Güvenli Konuma Dönüş | `BorderRespawnGuard` | `01_Screens/SVG/02_Vehicle/respawn_transition.svg` |
| Mini Harita | `MinimapUI` | `01_Screens/SVG/03_Navigation/minimap_route.svg` |
| Pusula | `CompassUI` | `01_Screens/SVG/03_Navigation/compass_navigation.svg` |
| Tam Harita (öneri) | `NavigationService` | `01_Screens/SVG/03_Navigation/full_navigation_map.svg` |
| Aktif Görev | `ActiveQuestUI;DeliveryUI` | `01_Screens/SVG/04_Quests/active_quest.svg` |
| Görev Teklifi | `PhoneMissionUI` | `01_Screens/SVG/04_Quests/mission_offer.svg` |
| Görev Panosu | `QuestListUI;QuestEntryUI` | `01_Screens/SVG/04_Quests/quest_board.svg` |
| Görev Başarısı | `QuestCompleteUI` | `01_Screens/SVG/04_Quests/quest_success.svg` |
| Görev Başarısızlığı | `QuestCompleteUI` | `01_Screens/SVG/04_Quests/quest_failure.svg` |
| Görev İstatistikleri | `QuestStatisticsUI;SimpleBarChart` | `01_Screens/SVG/04_Quests/quest_statistics.svg` |
| Öğretici | `TutorialUI` | `01_Screens/SVG/04_Quests/tutorial_highlight.svg` |
| Şirket Kurulumu | `CompanyPageUI` | `01_Screens/SVG/05_Company/new_company_setup.svg` |
| Araçlarım | `FleetManagementUI` | `01_Screens/SVG/05_Company/fleet_my_vehicles.svg` |
| Araç Galerisi | `FleetManagementUI` | `01_Screens/SVG/05_Company/fleet_gallery.svg` |
| Araç Yükseltme | `FleetManagementUI` | `01_Screens/SVG/05_Company/fleet_upgrades.svg` |
| Şirket Profili | `CompanyPageUI;AutoSignTextSetter` | `01_Screens/SVG/05_Company/company_profile.svg` |
| Telefon Ana Ekranı | `SmartphoneUI;SmartphoneIconRegistry` | `01_Screens/SVG/06_Smartphone/smartphone_home.svg` |
| Kurye Express | `SmartphoneDeliveryApp` | `01_Screens/SVG/06_Smartphone/smartphone_deliveries.svg` |
| QuickPay | `SmartphoneBankApp` | `01_Screens/SVG/06_Smartphone/smartphone_bank.svg` |
| Garajım & Filo | `SmartphoneGarageApp` | `01_Screens/SVG/06_Smartphone/smartphone_garage.svg` |
| Mesajlar | `SmartphoneMessagesApp` | `01_Screens/SVG/06_Smartphone/smartphone_messages.svg` |
| Telefon Bildirimleri | `SmartphoneNotificationToast` | `01_Screens/SVG/06_Smartphone/smartphone_notification.svg` |
| Bakiye Değişimi | `BalanceHudUI` | `01_Screens/SVG/07_Progression/balance_reward.svg` |
| Seviye Atlama | `ProgressionUI` | `01_Screens/SVG/07_Progression/level_up.svg` |
| Yetenek Ağacı | `ProgressionSkillTreeUI` | `01_Screens/SVG/07_Progression/skill_tree.svg` |
| Başarım | `AchievementNotificationUI` | `01_Screens/SVG/07_Progression/achievement_unlocked.svg` |
| Bildirim Kuyruğu | `NotificationQueue` | `01_Screens/SVG/08_World_System/notification_center.svg` |
| Onay Diyaloğu | `ConfirmationDialog` | `01_Screens/SVG/08_World_System/confirmation_dialog.svg` |
| Bilgi İpucu | `TooltipUI` | `01_Screens/SVG/08_World_System/tooltip_state.svg` |
| Mahalle Adı | `NeighborhoodUI` | `01_Screens/SVG/08_World_System/neighborhood_banner.svg` |
| Dünya Teslimat İşareti | `DeliveryIndicator` | `01_Screens/SVG/08_World_System/delivery_world_marker.svg` |
| Sinematik HUD | `CinematicCaptureHUD` | `01_Screens/SVG/08_World_System/cinematic_capture.svg` |
| Renk Körlüğü Kontrolü | `AccessibilityManager` | `01_Screens/SVG/08_World_System/accessibility_colorblind.svg` |
| TR/EN Yerelleştirme | `LocalizationTable` | `01_Screens/SVG/08_World_System/localization_preview.svg` |

## Görsel olmaktan ziyade koordinatör/servis rolündeki scriptler

`QuestUIManager`, `QuestUISetup`, `GlobalUiCoordinator`, `UIThemeConstants`, `UIButtonEnhancer`, `UIAnimationHelper`, `UIAudioFeedback`, `RuntimeUiSkinLoader`, `RuntimeOptionSelector`, `RuntimeColorMaterialHelper`, `SmartphoneIconRegistry`, `LocalizationTable`, `AccessibilityManager` ve `SceneTransitionManager` gibi sınıflar, tek başına ayrı bir ekran yerine **tasarımın işlevsel entegrasyon tabakalarıdır**. `Localizations`, `NotificationQueue`, `TooltipUI` ve temalar için kitte görsel durumlar bulunur; olay bağlama projeye aittir.
