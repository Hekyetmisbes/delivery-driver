using System.IO;
using DeliveryDriver.Cinematic;
using UnityEditor;
using UnityEngine;

namespace DeliveryDriver.Editor.Cinematic
{
    /// <summary>
    /// Unity Editor içi Sinematik Çekim, Kamera Açıları ve Yönetmen Kontrol Penceresi.
    /// Üst menüden: Delivery Driver > Cinematic Capture & Director
    /// </summary>
    public class CinematicCaptureWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private bool showAnglesFold = true;

        [MenuItem("Delivery Driver/Cinematic Capture & Director", false, 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<CinematicCaptureWindow>("Cinematic Director");
            window.minSize = new Vector2(380, 480);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(8);
            DrawHeader();
            EditorGUILayout.Space(8);

            DrawRecordingSection();
            EditorGUILayout.Space(8);

            DrawFormatAndDirectorSection();
            EditorGUILayout.Space(8);

            DrawCameraAnglesSection();
            EditorGUILayout.Space(8);

            DrawShortcutsHelp();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("🎬 Delivery Driver - Cinematic Capture & Director", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Trailer (16:9) ve Instagram Reels (9:16) video kayıt stüdyosu.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawRecordingSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Video Kaydı (Unity Recorder)", EditorStyles.boldLabel);

            bool isRecording = CinematicRecorderController.IsRecording;

            if (isRecording)
            {
                GUI.color = new Color(1f, 0.3f, 0.3f);
                float time = CinematicCaptureHUD.CurrentRecordingTime;
                int mins = (int)(time / 60f);
                int secs = (int)(time % 60f);

                if (GUILayout.Button($"■ KAYDI DURDUR [{mins:00}:{secs:00}] (F9)", GUILayout.Height(36)))
                {
                    CinematicRecorderController.HandleStopRecording();
                }
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.3f, 0.85f, 1f);
                if (GUILayout.Button("● KAYDI BAŞLAT (F9)", GUILayout.Height(36)))
                {
                    if (EditorApplication.isPlaying)
                    {
                        CinematicRecorderController.HandleStartRecording();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Uyarı", "Kayıt alabilmek için lütfen önce Play Mode'u (Oyun Modunu) başlatın.", "Tamam");
                    }
                }
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📂 Kayıtlar Klasörünü Aç"))
            {
                CinematicRecorderController.OpenRecordingsFolder();
            }

            if (!string.IsNullOrEmpty(CinematicRecorderController.LastSavedFilePath))
            {
                if (GUILayout.Button("📋 Son Video Yolunu Kopyala"))
                {
                    EditorGUIUtility.systemCopyBuffer = CinematicRecorderController.LastSavedFilePath + ".mp4";
                    Debug.Log($"[CinematicCaptureWindow] Video yolu panoya kopyalandı: {EditorGUIUtility.systemCopyBuffer}");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawFormatAndDirectorSection()
        {
            CinematicCameraController camController = CinematicCameraController.Instance;
            CinematicDirector director = CinematicDirector.Instance;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Biçim ve Otomatik Yönetmen", EditorStyles.boldLabel);

            if (camController != null)
            {
                // Format seçimi
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Video Formatı:");

                bool isTrailer = camController.CurrentFormat == CinematicVideoFormat.Trailer_16_9;
                bool isReels = camController.CurrentFormat == CinematicVideoFormat.Reels_9_16;

                if (GUILayout.Toggle(isTrailer, "Trailer (16:9)", EditorStyles.toolbarButton))
                {
                    if (!isTrailer) camController.SetVideoFormat(CinematicVideoFormat.Trailer_16_9);
                }
                if (GUILayout.Toggle(isReels, "Reels (9:16)", EditorStyles.toolbarButton))
                {
                    if (!isReels) camController.SetVideoFormat(CinematicVideoFormat.Reels_9_16);
                }
                EditorGUILayout.EndHorizontal();

                // Sinematik Mod Toggle
                bool cinActive = EditorGUILayout.Toggle("Sinematik Mod Aktif", camController.IsCinematicActive);
                if (cinActive != camController.IsCinematicActive)
                {
                    camController.SetCinematicMode(cinActive);
                }

                // Slow Motion
                bool slowMo = EditorGUILayout.Toggle("Ağır Çekim (Slow-Mo)", camController.IsSlowMotionActive);
                if (slowMo != camController.IsSlowMotionActive)
                {
                    camController.SetSlowMotion(slowMo);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Oyun çalıştırıldığında (Play Mode) kamera kontrolleri burada aktifleşecektir.", MessageType.Info);
            }

            EditorGUILayout.Space(4);

            if (director != null)
            {
                bool dirActive = EditorGUILayout.Toggle("Otomatik Yönetmen (F7)", director.IsDirectorActive);
                if (dirActive != director.IsDirectorActive)
                {
                    director.SetDirector(dirActive);
                }

                if (director.IsDirectorActive)
                {
                    EditorGUILayout.LabelField($"Sonraki Sahne Geçişi:", $"{director.TimeUntilNextCut:F1} saniye");
                    if (GUILayout.Button("Hemen Kesim Yap (Next Cut)"))
                    {
                        director.PerformDirectorCut();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCameraAnglesSection()
        {
            CinematicCameraController camController = CinematicCameraController.Instance;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showAnglesFold = EditorGUILayout.Foldout(showAnglesFold, "Kamera Açıları Seçimi", true);

            if (showAnglesFold)
            {
                string[] angleNames = new string[]
                {
                    "1. Standart Takip (Gameplay Chase)",
                    "2. Kaput ve Tampon (Hood & Bumper)",
                    "3. Tekerlek & Jant (Wheel Drift)",
                    "4. Kapı & Ayna Profili (Door Profile)",
                    "5. Alçak Egzoz (Low Rear Exhaust)",
                    "6. Drone & Helikopter (Drone / Heli)",
                    "7. Yol Kenarı Geçiş (Roadside Flyby)"
                };

                for (int i = 0; i < angleNames.Length; i++)
                {
                    bool isCurrent = camController != null && (int)camController.CurrentAngle == i && camController.IsCinematicActive;
                    GUI.backgroundColor = isCurrent ? new Color(0.4f, 1f, 0.4f) : Color.white;

                    if (GUILayout.Button(angleNames[i], GUILayout.Height(24)))
                    {
                        if (camController != null)
                        {
                            if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsDirectorActive)
                            {
                                CinematicDirector.Instance.SetDirector(false);
                            }
                            camController.SetAngle((CinematicCameraAngle)i, instantCut: false);
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShortcutsHelp()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Klavye Kısayolları", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1 - 7:", "Kamera Açıları arasında geçiş yap");
            EditorGUILayout.LabelField("F7:", "Otomatik Yönetmen Modunu Aç / Kapat");
            EditorGUILayout.LabelField("F8:", "Formatı Değiştir (Trailer 16:9 <-> Reels 9:16)");
            EditorGUILayout.LabelField("F9:", "60 FPS MP4 Video Kaydını Başlat / Bitir");
            EditorGUILayout.LabelField("F10:", "Ağır Çekim (Slow-Motion 0.25x) Aç / Kapat");
            EditorGUILayout.LabelField("H:", "Ekrandaki Bilgi Panelini Gizle / Göster");
            EditorGUILayout.EndVertical();
        }
    }
}
